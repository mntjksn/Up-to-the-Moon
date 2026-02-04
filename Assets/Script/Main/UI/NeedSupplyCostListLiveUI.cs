using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    NeedSupplyCostListLiveUI

    [역할]
    - “현재 업그레이드에 필요한 재료 목록”을 실시간으로 표시한다.
      (각 재료의 아이콘/필요 개수/현재 보유 개수, 그리고 업그레이드 완료 상태 표시 등)
    - step(업그레이드 단계)에 따라 UpgradeCostManager에서 비용 리스트를 가져와 row들을 구성한다.
    - SaveManager.OnResourceChanged 이벤트를 받아 “보유 개수(have)”만 가볍게 갱신한다.
    - autoDetectStep 옵션이 켜져 있으면,
      CharacterManager의 unlock 상태를 바탕으로 현재 step을 자동으로 계산하고 변동 시 UI를 재빌드한다.

    [설계 의도]
    1) 모바일 성능 최적화: Instantiate/Destroy 스파이크 방지
       - rows는 itemId -> rowUI 딕셔너리 캐시
       - rowPool(Stack)로 row UI를 재사용한다.
         RebuildToEmpty()에서 Destroy 대신 풀로 회수하여 다음 빌드 때 재사용한다.

    2) 이벤트 폭주 대응: Refresh 디바운스(한 프레임 1회)
       - OnResourceChanged가 짧은 시간에 여러 번 올 수 있으므로
         RequestRefresh()에서 refreshPending 플래그로 중복 요청을 합치고,
         RefreshEndOfFrame()에서 다음 프레임에 1번만 RefreshHaveCounts_Immediate()를 수행한다.

    3) step 자동 추적 + 최소 작업
       - AutoDetectRoutine()에서 일정 간격(autoRefreshInterval)으로
         "가장 최근에 unlock된 캐릭터"를 역순 탐색하여 step을 계산한다.
       - step이 바뀌면 SetStep(step)로 캐시/row 재빌드 수행
       - 업그레이드 완료 여부는 IsUpgradeCompleted_Cached()로 캐시 기반 판정하여 반복 탐색을 줄인다.

    4) 업그레이드 완료 체크 최적화(캐시)
       - currentStep에 대응하는 targetItemNum = currentStep - 1 을 구하고,
         해당 item_num을 가진 CharacterItem의 index를 cachedTargetIndex에 캐싱한다.
       - 이후 완료 여부는 cachedTargetIndex만 확인해 O(1)로 빠르게 판단한다.
       - step이 바뀌거나 캐시가 깨지면 CacheTargetIndex()로 재탐색한다.

    [주의/전제]
    - rowPrefab에는 SupplyCostRowLiveUI 컴포넌트가 있어야 한다.
    - UpgradeCostManager.GetCostsByStep(step)은 (itemId, count) 목록을 반환해야 한다.
    - ItemManager.GetItem(itemId)는 재료 아이콘(Sprite)을 얻기 위해 사용된다.
    - parent에 row들이 붙으며, parent가 null이면 자기 transform을 사용한다.
*/
public class NeedSupplyCostListLiveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform parent;     // row들이 붙을 부모(없으면 자기 자신)
    [SerializeField] private GameObject rowPrefab; // SupplyCostRowLiveUI가 붙은 프리팹
    [SerializeField] private bool hideWhenNoCost = true; // 비용이 없으면 리스트를 숨길지 여부

    [Header("Auto Step From Unlock")]
    [SerializeField] private bool autoDetectStep = true;       // unlock 상태로 step 자동 계산
    [SerializeField] private float autoRefreshInterval = 0.25f; // 자동 감지 폴링 간격(Realtime)

    private int currentStep = -1;         // 현재 표시 중인 step
    private int lastUnlockedIndex = -999; // 가장 최근 unlock된 캐릭터 index 캐시
    private bool lastUpgradeCompleted = false; // 업그레이드 완료 여부 캐시

    // itemId -> rowUI 캐시(표시 중인 row들)
    private readonly Dictionary<int, SupplyCostRowLiveUI> rows = new Dictionary<int, SupplyCostRowLiveUI>();

    // row 풀(Instantiate/Destroy 스파이크 방지)
    private readonly Stack<SupplyCostRowLiveUI> rowPool = new Stack<SupplyCostRowLiveUI>(16);

    // 업그레이드 완료 여부 판정 최적화용 캐시
    private int cachedTargetItemNum = int.MinValue; // currentStep-1 캐시
    private int cachedTargetIndex = -1;             // CharacterItem에서 찾은 index 캐시

    private Coroutine autoCo; // autoDetect 코루틴 핸들

    // 리소스 변경 이벤트 디바운스(1프레임 1회만 갱신)
    private bool refreshPending = false;
    private Coroutine refreshCo;

    private void Awake()
    {
        // parent가 지정되지 않으면 자기 자신을 사용
        if (parent == null) parent = transform;
    }

    private void OnEnable()
    {
        /*
            활성화 시 처리
            - SaveManager 자원 변경 이벤트 구독(보유 수량 갱신용)
            - autoDetectStep이면 자동 감지 코루틴 시작
            - 최초 1회 갱신 요청
        */
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged += RequestRefresh;

        if (autoDetectStep)
        {
            if (autoCo != null) StopCoroutine(autoCo);
            autoCo = StartCoroutine(AutoDetectRoutine());
        }

        RequestRefresh();
    }

    private void OnDisable()
    {
        // 이벤트/코루틴 정리
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged -= RequestRefresh;

        if (autoCo != null)
        {
            StopCoroutine(autoCo);
            autoCo = null;
        }

        if (refreshCo != null)
        {
            StopCoroutine(refreshCo);
            refreshCo = null;
        }

        refreshPending = false;
    }

    /*
        외부에서 step 지정
        - step이 바뀌면: 캐시 갱신 + row 재빌드 + 갱신 요청
        - 동일 step이면: 값만 다시 갱신 요청
    */
    public void SetStep(int step)
    {
        step = Mathf.Max(0, step);
        if (currentStep == step) { RequestRefresh(); return; }

        currentStep = step;

        // step 변경 시 업그레이드 판정 캐시 갱신
        CacheTargetIndex();

        // 현재 step의 업그레이드 완료 여부 캐시 업데이트
        lastUpgradeCompleted = IsUpgradeCompleted_Cached();

        // row 재구성(풀 재사용)
        RebuildRows();

        // 보유 수량 표시 갱신 예약
        RequestRefresh();
    }

    /*
        리스트 초기화(표시할 step이 없는 상태)
        - row를 비우고(풀로 회수)
        - hideWhenNoCost 옵션에 따라 오브젝트를 숨김/표시
    */
    public void Clear()
    {
        currentStep = -1;
        lastUnlockedIndex = -999;
        cachedTargetItemNum = int.MinValue;
        cachedTargetIndex = -1;

        RebuildToEmpty();

        if (hideWhenNoCost) gameObject.SetActive(false);
        else gameObject.SetActive(true);
    }

    // ─────────────────────────────
    // unlock 상태에서 step 자동 계산
    // ─────────────────────────────

    /*
        자동 감지 루틴
        - 관련 매니저 로드 완료까지 대기
        - 최초 force=true로 step를 강제 반영
        - 이후 일정 간격(autoRefreshInterval)으로 step 변화/완료 변화 감지
    */
    private IEnumerator AutoDetectRoutine()
    {
        while (CharacterManager.Instance == null || !CharacterManager.Instance.IsLoaded)
            yield return null;

        while (UpgradeCostManager.Instance == null || !UpgradeCostManager.Instance.IsLoaded)
            yield return null;

        while (ItemManager.Instance == null || !ItemManager.Instance.IsLoaded)
            yield return null;

        AutoUpdateStepIfChanged(force: true);

        var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, autoRefreshInterval));
        while (true)
        {
            AutoUpdateStepIfChanged(force: false);
            yield return wait;
        }
    }

    /*
        unlock 기반으로 현재 step을 계산/갱신
        - 가장 최근 unlock된 캐릭터를 역순 탐색하여 newestUnlocked를 찾는다.
        - newestUnlocked가 없으면 Clear()
        - step = item_num + 1 로 계산(현재 업그레이드/비용 단계)
        - step 또는 newestUnlocked가 바뀌면 SetStep(step)
        - 업그레이드 완료 여부 변화도 감지하여 RequestRefresh() 호출
    */
    private void AutoUpdateStepIfChanged(bool force)
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null || cm.CharacterItem.Count == 0)
        {
            if (force) Clear();
            return;
        }

        // newestUnlocked 찾기: 역순 탐색 + 첫 발견 시 break
        int newestUnlocked = -1;
        for (int i = cm.CharacterItem.Count - 1; i >= 0; i--)
        {
            var it = cm.CharacterItem[i];
            if (it != null && it.item_unlock)
            {
                newestUnlocked = i;
                break;
            }
        }

        // unlock된 것이 하나도 없으면 비우기
        if (newestUnlocked < 0)
        {
            if (force || lastUnlockedIndex != newestUnlocked)
            {
                lastUnlockedIndex = newestUnlocked;
                Clear();
            }
            return;
        }

        // 현재 step 계산(item_num + 1)
        int step = cm.CharacterItem[newestUnlocked].item_num + 1;

        // 변화 감지 시 step 반영
        if (force || lastUnlockedIndex != newestUnlocked || currentStep != step)
        {
            lastUnlockedIndex = newestUnlocked;
            SetStep(step);
        }

        // 완료 체크(캐시 기반)
        bool nowCompleted = IsUpgradeCompleted_Cached();
        if (force || nowCompleted != lastUpgradeCompleted)
        {
            lastUpgradeCompleted = nowCompleted;
            RequestRefresh();
        }
    }

    // ─────────────────────────────
    // UI 빌드/갱신
    // ─────────────────────────────

    /*
        row 재빌드
        - currentStep의 비용 리스트를 가져와 row들을 구성한다.
        - 비용이 없으면 hideWhenNoCost에 따라 표시/숨김
        - 기존 row는 Destroy 대신 풀로 회수하여 재사용한다.
    */
    private void RebuildRows()
    {
        var im = ItemManager.Instance;
        var ucm = UpgradeCostManager.Instance;

        if (rowPrefab == null || parent == null) return;
        if (im == null || !im.IsLoaded) return;
        if (ucm == null || !ucm.IsLoaded) return;

        // 기존 row 제거(풀로 회수)
        RebuildToEmpty();

        var costs = ucm.GetCostsByStep(currentStep);
        if (costs == null || costs.Count == 0)
        {
            // 비용이 없으면 옵션에 따라 숨김/표시
            gameObject.SetActive(!hideWhenNoCost);
            return;
        }

        gameObject.SetActive(true);

        // 비용 리스트 기반으로 row 구성
        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];

            var mat = im.GetItem(c.itemId);
            Sprite spr = (mat != null) ? mat.itemimg : null;

            // 풀에서 row 재사용
            var row = GetRowFromPool();
            if (row == null) continue;

            row.transform.SetParent(parent, false);
            row.gameObject.SetActive(true);

            // itemId/아이콘/필요수량 세팅
            row.Setup(c.itemId, spr, c.count);

            // itemId로 row 빠른 접근(보유수량 갱신용)
            rows[c.itemId] = row;
        }
    }

    /*
        풀에서 row 가져오기
        - 있으면 Pop()
        - 없으면 rowPrefab Instantiate 후 SupplyCostRowLiveUI 컴포넌트 확인
    */
    private SupplyCostRowLiveUI GetRowFromPool()
    {
        if (rowPool.Count > 0)
            return rowPool.Pop();

        var go = Instantiate(rowPrefab);
        var row = go.GetComponent<SupplyCostRowLiveUI>();
        if (row == null)
        {
            Debug.LogError("[NeedSupplyCostListLiveUI] rowPrefab에 SupplyCostRowLiveUI가 없음");
            Destroy(go);
            return null;
        }
        return row;
    }

    /*
        현재 row들을 비우기(풀로 회수)
        - rows 딕셔너리 클리어
        - parent 자식들을 돌며 SupplyCostRowLiveUI면 비활성화 후 풀에 Push
        - 다른 오브젝트가 섞였으면 안전하게 Destroy
    */
    private void RebuildToEmpty()
    {
        // Destroy 대신 풀로 회수(모바일 스파이크 원인 제거)
        rows.Clear();
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var t = parent.GetChild(i);
            var row = t.GetComponent<SupplyCostRowLiveUI>();
            if (row != null)
            {
                row.gameObject.SetActive(false);
                row.transform.SetParent(transform, false); // 임시 보관(리스트에서 빠지도록)
                rowPool.Push(row);
            }
            else
            {
                // 혹시 다른 오브젝트가 섞여있으면 기존대로
                Destroy(t.gameObject);
            }
        }
    }

    // ─────────────────────────────
    // Refresh 디바운스(이벤트 폭주 방지)
    // ─────────────────────────────

    /*
        Refresh 요청
        - 같은 프레임에 여러 번 불려도 1번만 실제 갱신되도록 디바운스
        - 코루틴으로 다음 프레임에 RefreshHaveCounts_Immediate() 실행
    */
    private void RequestRefresh()
    {
        if (!isActiveAndEnabled) return;

        if (refreshPending) return;
        refreshPending = true;

        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = StartCoroutine(RefreshEndOfFrame());
    }

    /*
        다음 프레임에 실제 갱신 1회 수행
        - 프레임 내 이벤트 폭주를 1회로 합침
    */
    private IEnumerator RefreshEndOfFrame()
    {
        // 같은 프레임에 여러 번 호출돼도 1번만 실제 갱신
        yield return null;

        refreshPending = false;
        RefreshHaveCounts_Immediate();
    }

    /*
        보유 수량(have) 갱신
        - rows(itemId -> row)에 대해 SaveManager 자원 수량을 읽어 SetHave 적용
        - lastUpgradeCompleted가 true면 각 row에 업그레이드 완료 표시(SetUpgradeCompleted) 적용
    */
    private void RefreshHaveCounts_Immediate()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        // 보유 수량 갱신
        foreach (var kv in rows)
        {
            var row = kv.Value;
            if (row == null) continue;

            int have = sm.GetResource(kv.Key);
            row.SetHave(have);
        }

        // 업그레이드 완료 상태 표시(이미 계산된 값 사용)
        if (rows.Count > 0 && lastUpgradeCompleted)
        {
            foreach (var kv in rows)
            {
                if (kv.Value != null)
                    kv.Value.SetUpgradeCompleted();
            }
        }
    }

    // ─────────────────────────────
    // 업그레이드 완료 체크 최적화(캐시)
    // ─────────────────────────────

    /*
        currentStep에 해당하는 “대상 캐릭터(item_num = step-1)”를 CharacterItem에서 찾아 index 캐싱
        - 다음 완료 여부 체크에서 O(1) 접근을 위해 cachedTargetIndex를 저장
    */
    private void CacheTargetIndex()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null)
        {
            cachedTargetItemNum = int.MinValue;
            cachedTargetIndex = -1;
            return;
        }

        int targetItemNum = currentStep - 1;
        cachedTargetItemNum = targetItemNum;
        cachedTargetIndex = -1;

        if (targetItemNum < 0) return;

        // item_num으로 찾기(한 번만)
        for (int i = 0; i < cm.CharacterItem.Count; i++)
        {
            var it = cm.CharacterItem[i];
            if (it != null && it.item_num == targetItemNum)
            {
                cachedTargetIndex = i;
                break;
            }
        }
    }

    /*
        업그레이드 완료 여부 반환(캐시 기반)
        - 캐시가 step과 불일치하거나 index가 유효하지 않으면 CacheTargetIndex()로 갱신
        - cachedTargetIndex의 item_upgrade 값으로 완료 여부 판단
    */
    private bool IsUpgradeCompleted_Cached()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null) return false;

        // 캐시가 현재 step과 맞지 않거나 index가 범위 밖이면 재캐시
        if (cachedTargetItemNum != currentStep - 1 || cachedTargetIndex < 0 || cachedTargetIndex >= cm.CharacterItem.Count)
        {
            CacheTargetIndex();
        }

        if (cachedTargetIndex < 0) return false;

        var it = cm.CharacterItem[cachedTargetIndex];
        return it != null && it.item_upgrade;
    }
}
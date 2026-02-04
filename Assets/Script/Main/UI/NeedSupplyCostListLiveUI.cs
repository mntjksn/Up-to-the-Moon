using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeedSupplyCostListLiveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform parent;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private bool hideWhenNoCost = true;

    [Header("Auto Step From Unlock")]
    [SerializeField] private bool autoDetectStep = true;
    [SerializeField] private float autoRefreshInterval = 0.25f;

    private int currentStep = -1;
    private int lastUnlockedIndex = -999;
    private bool lastUpgradeCompleted = false;

    // itemId -> rowUI 캐시
    private readonly Dictionary<int, SupplyCostRowLiveUI> rows = new Dictionary<int, SupplyCostRowLiveUI>();

    // ★ row 풀(Instantiate/Destroy 스파이크 방지)
    private readonly Stack<SupplyCostRowLiveUI> rowPool = new Stack<SupplyCostRowLiveUI>(16);

    // ★ 업그레이드 완료 여부 판정 최적화용 캐시
    private int cachedTargetItemNum = int.MinValue;
    private int cachedTargetIndex = -1; // CharacterItem에서 찾은 index

    private Coroutine autoCo;

    // ★ 리소스 변경 이벤트가 너무 자주 오면 디바운스(1프레임에 1번만 갱신)
    private bool refreshPending = false;
    private Coroutine refreshCo;

    private void Awake()
    {
        if (parent == null) parent = transform;
    }

    private void OnEnable()
    {
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

    public void SetStep(int step)
    {
        step = Mathf.Max(0, step);
        if (currentStep == step) { RequestRefresh(); return; }

        currentStep = step;

        // ★ step 바뀌면 업그레이드 판정 캐시 갱신
        CacheTargetIndex();

        lastUpgradeCompleted = IsUpgradeCompleted_Cached();
        RebuildRows();
        RequestRefresh();
    }

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

    private void AutoUpdateStepIfChanged(bool force)
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null || cm.CharacterItem.Count == 0)
        {
            if (force) Clear();
            return;
        }

        // ★ newestUnlocked 찾기: 역순 탐색 + 첫 발견 시 break (매틱 O(n) -> 평균 단축)
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

        if (newestUnlocked < 0)
        {
            if (force || lastUnlockedIndex != newestUnlocked)
            {
                lastUnlockedIndex = newestUnlocked;
                Clear();
            }
            return;
        }

        int step = cm.CharacterItem[newestUnlocked].item_num + 1;

        if (force || lastUnlockedIndex != newestUnlocked || currentStep != step)
        {
            lastUnlockedIndex = newestUnlocked;
            SetStep(step);
        }

        // ★ 완료 체크: 캐시 기반
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
    private void RebuildRows()
    {
        var im = ItemManager.Instance;
        var ucm = UpgradeCostManager.Instance;

        if (rowPrefab == null || parent == null) return;
        if (im == null || !im.IsLoaded) return;
        if (ucm == null || !ucm.IsLoaded) return;

        RebuildToEmpty();

        var costs = ucm.GetCostsByStep(currentStep);
        if (costs == null || costs.Count == 0)
        {
            gameObject.SetActive(!hideWhenNoCost);
            return;
        }

        gameObject.SetActive(true);

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];

            var mat = im.GetItem(c.itemId);
            Sprite spr = (mat != null) ? mat.itemimg : null;

            // ★ 풀에서 재사용
            var row = GetRowFromPool();
            row.transform.SetParent(parent, false);
            row.gameObject.SetActive(true);

            row.Setup(c.itemId, spr, c.count);
            rows[c.itemId] = row;
        }
    }

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

    private void RebuildToEmpty()
    {
        // ★ Destroy 대신 풀로 회수 (모바일 스파이크 원인 제거)
        rows.Clear();
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var t = parent.GetChild(i);
            var row = t.GetComponent<SupplyCostRowLiveUI>();
            if (row != null)
            {
                row.gameObject.SetActive(false);
                row.transform.SetParent(transform, false); // 임시 보관
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
    private void RequestRefresh()
    {
        if (!isActiveAndEnabled) return;

        if (refreshPending) return;
        refreshPending = true;

        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = StartCoroutine(RefreshEndOfFrame());
    }

    private IEnumerator RefreshEndOfFrame()
    {
        // 같은 프레임에 여러 번 호출돼도 1번만 실제 갱신
        yield return null;

        refreshPending = false;
        RefreshHaveCounts_Immediate();
    }

    private void RefreshHaveCounts_Immediate()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        foreach (var kv in rows)
        {
            var row = kv.Value;
            if (row == null) continue;

            int have = sm.GetResource(kv.Key);
            row.SetHave(have);
        }

        if (rows.Count > 0 && lastUpgradeCompleted) // ★ 이미 계산된 값 사용
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

        // item_num으로 찾기 (한 번만)
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

    private bool IsUpgradeCompleted_Cached()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null) return false;

        if (cachedTargetItemNum != currentStep - 1 || cachedTargetIndex < 0 || cachedTargetIndex >= cm.CharacterItem.Count)
        {
            CacheTargetIndex();
        }

        if (cachedTargetIndex < 0) return false;

        var it = cm.CharacterItem[cachedTargetIndex];
        return it != null && it.item_upgrade;
    }
}
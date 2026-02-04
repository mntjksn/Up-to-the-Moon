using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    UpgradeSlot

    [역할]
    - 업그레이드 슬롯(캐릭터 1개)의 UI를 표시/갱신한다.
      (아이콘/이름/설명/속도/가격/해금/업그레이드 버튼 상태 + 필요 재료 리스트)
    - SaveManager 자원/골드 변화 이벤트를 구독해서,
      전체 Refresh 대신 "가벼운 갱신(버튼/재료 수량)"만 수행한다.
    - UpgradeCostManager의 step(캐릭터 단계)에 따른 필요 재료를 표시한다.

    [설계 의도]
    1) Setup 1회 + Refresh 반복
       - Setup(idx)로 인덱스를 지정하고 initialized=true로 표시한 뒤
         Refresh()로 초기 UI를 한 번 세팅한다.

    2) 로드 타이밍 안전
       - OnEnable에서 initialized된 슬롯만 RefreshWhenReady() 코루틴을 돌려
         CharacterManager/ItemManager/UpgradeCostManager 로드 완료 후 Refresh()를 수행한다.

    3) 필요 재료 UI 최적화(빌드/업데이트 분리)
       - EnsureNeedSupplyRows(step): step에 맞는 row 개수만 "필요할 때만" 생성/활성화 정리
       - UpdateNeedSupplyRowsOnly(): 이미 만든 row의 "스프라이트/필요수량"만 갱신
       - row는 파괴(Destroy)하지 않고 재사용하여 모바일 프리즈/GC를 줄인다.

    4) 이벤트 대응 최적화
       - 자원/골드 변경 이벤트에서는 전체 Refresh() 대신
         버튼 상태 + 필요재료 표시만 갱신(HandleResourceChanged/HandleGoldChanged)

    [주의/전제]
    - slotPrefab(혹은 이 프리팹)에 필요한 UI 참조들이 연결되어 있어야 한다.
    - UpgradeCostManager.GetCostsByStep(step)의 반환 리스트는 내부 참조일 수 있으므로
      cachedCosts는 new로 복사하지 않고 참조만 유지한다(외부에서 변경되지 않는다는 전제).
*/
public class UpgradeSlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;      // CharacterManager.CharacterItem에서 참조할 인덱스
    private bool initialized = false;            // Setup() 호출 여부(초기화 완료 플래그)

    [Header("UI")]
    [SerializeField] private Image icon;                 // 캐릭터/아이템 아이콘
    [SerializeField] private TextMeshProUGUI nameText;   // 이름
    [SerializeField] private TextMeshProUGUI subText;    // 설명/부가 텍스트
    [SerializeField] private TextMeshProUGUI speedText;  // 속도 표시

    [Header("Panel")]
    [SerializeField] private GameObject unlockMain; // 미해금/해금 상태 패널(메인)
    [SerializeField] private GameObject unlockDone; // 업그레이드 완료 패널

    [Header("MainPanel")]
    [SerializeField] private TextMeshProUGUI priceText; // 해금 가격 표시
    [SerializeField] private Button unlockButton;       // 해금 버튼

    [Header("UpgradePanel")]
    [SerializeField] private Button upgradeButton;      // 업그레이드 버튼

    [Header("Need Supply UI")]
    [SerializeField] private Transform needSupplyParent;    // 필요 재료 row들이 붙을 부모
    [SerializeField] private GameObject supplyCostPrefab;   // 재료 row 프리팹(SupplyCostRowUI)

    [Header("SFX")]
    [SerializeField] private AudioSource sfx; // 버튼 클릭 효과음

    private CharacterItem item;     // 현재 슬롯이 참조 중인 캐릭터 데이터
    private Coroutine refreshCo;    // 로드 대기 후 Refresh 수행용 코루틴

    // 캐시(매번 Instance 접근/탐색 줄이기)
    private SaveManager sm;
    private ItemManager im;
    private UpgradeCostManager ucm;

    private int stepCached = -1;         // 마지막으로 본 step 캐시
    private List<Cost> cachedCosts;      // step에 따른 비용 리스트 참조(복사하지 않음)

    // row 캐시(한 번 만든 UI를 재사용)
    private readonly List<SupplyCostRowUI> costRows = new List<SupplyCostRowUI>(8);

    private void OnEnable()
    {
        /*
            활성화 시 초기 바인딩
            - 매니저 캐시 갱신
            - SaveManager 이벤트 구독(자원/골드 변경)
            - 버튼 리스너 연결(Unlock/Upgrade)
            - 초기화된 슬롯이면 로드 완료 대기 후 Refresh
        */
        sm = SaveManager.Instance;
        im = ItemManager.Instance;
        ucm = UpgradeCostManager.Instance;

        // 자원/골드 변경 이벤트 구독(중복 방지 위해 -= 후 +=)
        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnResourceChanged += HandleResourceChanged;

            sm.OnGoldChanged -= HandleGoldChanged;
            sm.OnGoldChanged += HandleGoldChanged;
        }

        // Unlock 버튼 리스너 바인딩
        if (unlockButton != null)
        {
            unlockButton.onClick.RemoveListener(OnClickUnlock);
            unlockButton.onClick.AddListener(OnClickUnlock);
        }

        // Upgrade 버튼 리스너 바인딩
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnClickUpgrade);
            upgradeButton.onClick.AddListener(OnClickUpgrade);
        }

        // Setup이 호출되지 않은 슬롯은 아직 인덱스/데이터가 확정되지 않았으니 갱신하지 않음
        if (!initialized) return;

        // 로드 완료 타이밍을 고려해 다음 프레임~몇 프레임 대기 후 Refresh
        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = StartCoroutine(RefreshWhenReady());
    }

    private void OnDisable()
    {
        // 이벤트/리스너/코루틴 정리(중복 호출/메모리 누수 방지)
        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnGoldChanged -= HandleGoldChanged;
        }

        if (unlockButton != null) unlockButton.onClick.RemoveListener(OnClickUnlock);
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OnClickUpgrade);

        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = null;
    }

    /*
        슬롯 초기화(인덱스 지정)
        - UpgradeManager에서 슬롯 생성 시 호출
        - 최초 1회 전체 Refresh 수행
    */
    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh(); // 최초 1회 전체 갱신
    }

    /*
        관련 매니저들이 로드 완료될 때까지 대기 후 Refresh
        - 1프레임 쉬고 시작(인스턴스 생성/바인딩 안정화)
        - safety 루프로 무한 대기 방지
    */
    private IEnumerator RefreshWhenReady()
    {
        yield return null;

        int safety = 200;
        while (safety-- > 0)
        {
            var cm = CharacterManager.Instance;

            // 필요한 매니저들이 존재 + 로드 완료 상태인지 확인
            if (cm != null && cm.IsLoaded &&
                ItemManager.Instance != null && ItemManager.Instance.IsLoaded &&
                UpgradeCostManager.Instance != null && UpgradeCostManager.Instance.IsLoaded)
            {
                break;
            }

            yield return null;
        }

        // 캐시 갱신(로드 완료 후 다시 잡아 안전성 확보)
        sm = SaveManager.Instance;
        im = ItemManager.Instance;
        ucm = UpgradeCostManager.Instance;

        Refresh();
    }

    /*
        슬롯 전체 Refresh
        - CharacterManager에서 index에 해당하는 CharacterItem을 가져와 UI 반영
        - 아이콘/텍스트/패널/버튼 상태를 ApplyUI로 반영
        - 필요 재료 UI는 row 생성/활성 정리(Ensure)와 값 갱신(Update)로 분리 처리
    */
    public void Refresh()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return;

        // index 범위/데이터 유효성 체크
        if (cm.CharacterItem == null || index < 0 || index >= cm.CharacterItem.Count)
        {
            ApplyUI(null);
            return;
        }

        item = cm.CharacterItem[index];

        // 고정 UI(아이콘/텍스트/패널/버튼) 반영
        ApplyUI(item);

        // 필요 재료 UI: step 기준으로 row 구성 + 표시 업데이트
        if (item != null)
        {
            int step = item.item_num + 1;
            EnsureNeedSupplyRows(step);     // row 생성/활성 정리(필요할 때만)
            UpdateNeedSupplyRowsOnly();     // 표시값(스프라이트/수량)만 갱신
        }
    }

    /*
        고정 UI 반영(아이콘/텍스트/패널/버튼 상태)
        - it == null: UI 초기화/비활성
        - it != null: 현재 캐릭터 데이터를 기반으로 UI 표시
        - 버튼은 전체 Refresh 없이도 이벤트에서 다시 계산할 수 있게 "가볍게" 갱신
    */
    private void ApplyUI(CharacterItem it)
    {
        // 데이터가 없으면 UI 비우기/비활성화
        if (it == null)
        {
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            if (nameText != null) nameText.text = "";
            if (subText != null) subText.text = "";
            if (speedText != null) speedText.text = "";
            if (priceText != null) priceText.text = "";
            if (unlockMain != null) unlockMain.SetActive(false);
            if (unlockDone != null) unlockDone.SetActive(false);
            if (unlockButton != null) unlockButton.interactable = false;
            if (upgradeButton != null) upgradeButton.interactable = false;
            return;
        }

        // 패널 상태(해금/업그레이드 여부)
        if (unlockMain != null) unlockMain.SetActive(it.item_unlock);
        if (unlockDone != null) unlockDone.SetActive(it.item_upgrade);

        // 아이콘 표시
        if (icon != null)
        {
            icon.enabled = (it.itemimg != null);
            icon.sprite = it.itemimg;
        }

        // 텍스트 표시
        if (nameText != null) nameText.text = it.name;
        if (subText != null) subText.text = it.sub;
        if (speedText != null) speedText.text = $"{it.item_speed} Km / s";
        if (priceText != null) priceText.text = $"{NumberFormatter.FormatKorean(it.item_price)}원";

        // 해금 버튼 상태 계산
        if (unlockButton != null)
        {
            bool prevOk = CanUnlockByPrevRule();                 // 이전 캐릭터 업그레이드 완료 조건
            bool notYetUnlocked = !it.item_unlock;               // 아직 해금되지 않음
            bool haveGold = (sm != null) && (sm.GetGold() >= it.item_price); // 골드 충분

            unlockButton.interactable = prevOk && notYetUnlocked && haveGold;
        }

        // 업그레이드 버튼 상태 계산(필요 재료 충족 여부)
        if (upgradeButton != null)
        {
            int step = it.item_num + 1;
            upgradeButton.interactable = (sm != null) && CanAffordCosts(step);
        }
    }

    /*
        해금 조건(이전 규칙)
        - index 0~1은 바로 해금 가능
        - 그 외는 "이전 캐릭터가 item_upgrade=true(업그레이드 완료)"일 때만 해금 가능
    */
    private bool CanUnlockByPrevRule()
    {
        if (index <= 1) return true;

        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null) return false;

        int prevIndex = index - 1;
        if ((uint)prevIndex >= (uint)cm.CharacterItem.Count) return false;

        var prev = cm.CharacterItem[prevIndex];
        return prev != null && prev.item_upgrade;
    }

    /*
        해금 버튼 클릭 처리
        - 골드 체크 후 차감
        - item.item_unlock=true 반영 + 저장
        - 클릭은 빈도가 낮으므로 전체 Refresh 허용
    */
    private void OnClickUnlock()
    {
        if (item == null || sm == null) return;
        if (sm.GetGold() < item.item_price) return;

        // 효과음 재생(설정에 따라 mute)
        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        // 비용 지불
        sm.AddGold(-item.item_price);

        // 상태 변경 + 저장
        item.item_unlock = true;
        CharacterManager.Instance.SaveToJson();

        // 전체 Refresh
        Refresh();
    }

    /*
        업그레이드 버튼 클릭 처리
        - 필요 재료 충족 검사 후 차감
        - item_upgrade/item_unlock 반영 + 저장
        - 현재 캐릭터/속도(SaveManager) 갱신
        - 미션 카운트 증가
        - 클릭은 빈도가 낮으므로 전체 Refresh 허용
    */
    private void OnClickUpgrade()
    {
        if (item == null || sm == null) return;

        int step = item.item_num + 1;
        if (!CanAffordCosts(step)) return;

        // 효과음 재생(설정에 따라 mute)
        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        // 재료 차감
        SpendCosts(step);

        // 상태 변경
        item.item_upgrade = true;
        item.item_unlock = true;

        // 저장
        CharacterManager.Instance.SaveToJson();

        // 현재 캐릭터/속도 세팅
        var cm = CharacterManager.Instance;

        int nextIndex = index;
        if (cm != null && cm.IsLoaded && cm.CharacterItem != null &&
            (uint)nextIndex < (uint)cm.CharacterItem.Count)
        {
            var next = cm.CharacterItem[nextIndex];
            sm.SetCurrentCharacterId(next.item_num);
            sm.SetSpeed(next.item_speed);
        }
        else
        {
            sm.SetCurrentCharacterId(item.item_num);
            sm.SetSpeed(item.item_speed);
        }

        // 미션 진행도 반영
        MissionProgressManager.Instance?.Add("character_upgrade_count", 1);

        // 전체 Refresh
        Refresh();
    }

    // -----------------------------
    // Need Supply UI 최적화 핵심
    // -----------------------------

    /*
        필요 재료 row 확보/정리
        - step이 바뀌면 cachedCosts를 갱신
        - cachedCosts.Count 만큼 row가 없으면 생성
        - 초과 row는 Destroy하지 않고 비활성화로 재사용
    */
    private void EnsureNeedSupplyRows(int step)
    {
        if (needSupplyParent == null || supplyCostPrefab == null) return;
        if (im == null || !im.IsLoaded) return;
        if (ucm == null || !ucm.IsLoaded) return;

        // step 바뀌면 캐시 갱신
        if (stepCached != step)
        {
            stepCached = step;
            cachedCosts = ucm.GetCostsByStep(step); // 내부 리스트 참조
        }

        int needCount = (cachedCosts != null) ? cachedCosts.Count : 0;

        // 필요한 만큼만 생성
        for (int i = costRows.Count; i < needCount; i++)
        {
            var rowGO = Instantiate(supplyCostPrefab, needSupplyParent);
            var rowUI = rowGO.GetComponent<SupplyCostRowUI>();
            if (rowUI != null) costRows.Add(rowUI);
            else Destroy(rowGO);
        }

        // 활성/비활성 정리 (Destroy 금지)
        for (int i = 0; i < costRows.Count; i++)
        {
            if (costRows[i] != null)
                costRows[i].gameObject.SetActive(i < needCount);
        }
    }

    /*
        필요 재료 표시값만 업데이트
        - row 재생성 없이 스프라이트/필요수량만 반영
        - 자원 변화 이벤트에서 호출되어 UI를 가볍게 갱신할 때 사용
    */
    private void UpdateNeedSupplyRowsOnly()
    {
        if (cachedCosts == null || cachedCosts.Count == 0) return;
        if (im == null || !im.IsLoaded) return;

        // 표시 업데이트(스프라이트/필요수량)
        for (int i = 0; i < cachedCosts.Count; i++)
        {
            var c = cachedCosts[i];

            var rowUI = (i < costRows.Count) ? costRows[i] : null;
            if (rowUI == null) continue;

            Sprite spr = null;
            var matItem = im.GetItem(c.itemId);
            if (matItem != null) spr = matItem.itemimg;

            rowUI.Set(spr, c.count);
        }
    }

    /*
        비용 충족 여부 검사
        - step에 해당하는 비용 리스트를 가져와
          SaveManager의 자원 수량이 모두 충분한지 확인
        - costs가 비어 있으면 비용이 없다는 의미로 true 반환
    */
    private bool CanAffordCosts(int step)
    {
        if (sm == null) return false;
        if (ucm == null || !ucm.IsLoaded) return false;

        var costs = ucm.GetCostsByStep(step);
        if (costs == null || costs.Count == 0) return true;

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            if (sm.GetResource(c.itemId) < c.count) return false;
        }
        return true;
    }

    /*
        비용 차감
        - step에 해당하는 비용만큼 자원을 차감한다
    */
    private void SpendCosts(int step)
    {
        if (sm == null) return;
        if (ucm == null || !ucm.IsLoaded) return;

        var costs = ucm.GetCostsByStep(step);
        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            sm.AddResource(c.itemId, -c.count);
        }
    }

    /*
        자원 변화 이벤트 핸들러
        - 전체 Refresh() 대신 가벼운 갱신만 수행
          1) 업그레이드 버튼 가능 여부
          2) 해금 버튼 가능 여부
          3) 필요 재료 row 값 업데이트(재생성 없이)
    */
    private void HandleResourceChanged()
    {
        if (!initialized || item == null) return;

        // 업그레이드 버튼 상태 갱신
        if (upgradeButton != null)
            upgradeButton.interactable = (sm != null) && CanAffordCosts(item.item_num + 1);

        // 해금 버튼 상태 갱신
        if (unlockButton != null)
        {
            bool prevOk = CanUnlockByPrevRule();
            bool notYetUnlocked = !item.item_unlock;
            bool haveGold = (sm != null) && (sm.GetGold() >= item.item_price);
            unlockButton.interactable = prevOk && notYetUnlocked && haveGold;
        }

        // 필요재료 UI는 row 재생성 없이 업데이트만
        UpdateNeedSupplyRowsOnly();
    }

    /*
        골드 변화 이벤트 핸들러
        - 전체 Refresh() 대신 버튼 상태만 갱신
          (골드는 해금/업그레이드 가능 여부에 영향을 줌)
    */
    private void HandleGoldChanged()
    {
        if (!initialized || item == null) return;

        // 해금 버튼 상태 갱신
        if (unlockButton != null)
        {
            bool prevOk = CanUnlockByPrevRule();
            bool notYetUnlocked = !item.item_unlock;
            bool haveGold = (sm != null) && (sm.GetGold() >= item.item_price);
            unlockButton.interactable = prevOk && notYetUnlocked && haveGold;
        }

        // 업그레이드 버튼 상태 갱신
        if (upgradeButton != null)
            upgradeButton.interactable = (sm != null) && CanAffordCosts(item.item_num + 1);
    }
}
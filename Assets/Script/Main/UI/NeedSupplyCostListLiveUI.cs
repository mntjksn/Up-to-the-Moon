using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeedSupplyCostListLiveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform parent;       // row들이 붙을 곳(없으면 자기 자신)
    [SerializeField] private GameObject rowPrefab;   // Panel_Supply_Cost_Live
    [SerializeField] private bool hideWhenNoCost = true;

    [Header("Auto Step From Unlock")]
    [SerializeField] private bool autoDetectStep = true;
    [SerializeField] private float autoRefreshInterval = 0.25f; // unlock 변동 감지용(가벼운 폴링)

    private int currentStep = -1;
    private int lastUnlockedIndex = -999;

    private bool lastUpgradeCompleted = false;

    // itemId -> rowUI 캐시
    private readonly Dictionary<int, SupplyCostRowLiveUI> rows = new Dictionary<int, SupplyCostRowLiveUI>();

    private Coroutine autoCo;

    private void Awake()
    {
        if (parent == null) parent = transform;
    }

    private void OnEnable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged += RefreshHaveCounts;

        if (autoDetectStep)
        {
            if (autoCo != null) StopCoroutine(autoCo);
            autoCo = StartCoroutine(AutoDetectRoutine());
        }

        RefreshHaveCounts();
    }

    private void OnDisable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged -= RefreshHaveCounts;

        if (autoCo != null)
        {
            StopCoroutine(autoCo);
            autoCo = null;
        }
    }

    /// <summary>
    /// (선택) 외부에서 직접 step 지정하고 싶을 때만 사용
    /// </summary>
    public void SetStep(int step)
    {
        step = Mathf.Max(0, step);
        if (currentStep == step) { RefreshHaveCounts(); return; }

        currentStep = step;
        lastUpgradeCompleted = IsUpgradeCompleted();
        RebuildRows();
        RefreshHaveCounts();
    }

    public void Clear()
    {
        currentStep = -1;
        lastUnlockedIndex = -999;
        RebuildToEmpty();

        if (hideWhenNoCost) gameObject.SetActive(false);
        else gameObject.SetActive(true);
    }

    // ─────────────────────────────
    // 핵심: unlock 상태에서 step 자동 계산
    // ─────────────────────────────
    private IEnumerator AutoDetectRoutine()
    {
        // 매니저 준비될 때까지 대기
        while (CharacterManager.Instance == null || !CharacterManager.Instance.IsLoaded)
            yield return null;

        while (UpgradeCostManager.Instance == null || !UpgradeCostManager.Instance.IsLoaded)
            yield return null;

        while (ItemManager.Instance == null || !ItemManager.Instance.IsLoaded)
            yield return null;

        // 첫 1회 적용
        AutoUpdateStepIfChanged(force: true);

        // 이후 주기적으로 unlock 변동 감지
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

        // "가장 큰 item_num 중 unlock=true" 찾기
        int newestUnlocked = -1;
        for (int i = 0; i < cm.CharacterItem.Count; i++)
        {
            var it = cm.CharacterItem[i];
            if (it != null && it.item_unlock)
                newestUnlocked = i;
        }

        // unlock이 하나도 없으면 표시 안 함
        if (newestUnlocked < 0)
        {
            if (force || lastUnlockedIndex != newestUnlocked)
            {
                lastUnlockedIndex = newestUnlocked;
                Clear();
            }
            return;
        }

        // 다음 업그레이드 step = (unlock된 캐릭터의 item_num + 1)
        // ※ 너 기존 규칙 그대로
        int step = cm.CharacterItem[newestUnlocked].item_num + 1;

        if (force || lastUnlockedIndex != newestUnlocked || currentStep != step)
        {
            lastUnlockedIndex = newestUnlocked;
            SetStep(step);
        }

        bool nowCompleted = IsUpgradeCompleted(); // 너가 방금 만든 함수

        if (force || nowCompleted != lastUpgradeCompleted)
        {
            lastUpgradeCompleted = nowCompleted;
            RefreshHaveCounts(); // 여기서 SetUpgradeCompleted()까지 들어가게 됨
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
            if (hideWhenNoCost) gameObject.SetActive(false);
            else gameObject.SetActive(true);
            return;
        }

        gameObject.SetActive(true);

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];

            var mat = im.GetItem(c.itemId);
            Sprite spr = (mat != null) ? mat.itemimg : null;

            var go = Instantiate(rowPrefab, parent);
            var row = go.GetComponent<SupplyCostRowLiveUI>();
            if (row == null)
            {
                Debug.LogError("[NeedSupplyCostListLiveUI] rowPrefab에 SupplyCostRowLiveUI가 없음");
                Destroy(go);
                continue;
            }

            // row가 "have / need" 표기 가능하게 만들려면
            // Setup에서 need를 저장하고, SetHave에서 have/need 텍스트로 갱신하면 됨
            row.Setup(c.itemId, spr, c.count);
            rows[c.itemId] = row;
        }
    }

    private void RebuildToEmpty()
    {
        rows.Clear();
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void RefreshHaveCounts()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        foreach (var kv in rows)
        {
            int itemId = kv.Key;
            var row = kv.Value;
            if (row == null) continue;

            int have = sm.GetResource(itemId);
            row.SetHave(have);
        }

        if (rows.Count > 0 && IsUpgradeCompleted())
        {
            foreach (var kv in rows)
            {
                if (kv.Value != null)
                    kv.Value.SetUpgradeCompleted();
            }
        }
    }

    private bool IsUpgradeCompleted()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null) return false;

        // 너가 currentStep을 "업그레이드 비용 step"으로 쓰고 있으니
        // step = (캐릭터 item_num + 1) 규칙이면
        int targetItemNum = currentStep - 1;
        if (targetItemNum < 0) return false;

        // item_num으로 찾기 (index랑 item_num이 항상 같다는 보장이 없어서 안전하게)
        for (int i = 0; i < cm.CharacterItem.Count; i++)
        {
            var it = cm.CharacterItem[i];
            if (it != null && it.item_num == targetItemNum)
                return it.item_upgrade;
        }

        return false;
    }
}
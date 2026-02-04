using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeSlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;
    private bool initialized = false;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI subText;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Panel")]
    [SerializeField] private GameObject unlockMain;
    [SerializeField] private GameObject unlockDone;

    [Header("MainPanel")]
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button unlockButton;

    [Header("UpgradePanel")]
    [SerializeField] private Button upgradeButton;

    [Header("Need Supply UI")]
    [SerializeField] private Transform needSupplyParent;
    [SerializeField] private GameObject supplyCostPrefab;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    private CharacterItem item;
    private Coroutine refreshCo;

    // 캐시
    private SaveManager sm;
    private ItemManager im;
    private UpgradeCostManager ucm;

    private int stepCached = -1;
    private List<Cost> cachedCosts; // ucm의 내부 리스트 참조 (new 안 함)

    // row 캐시(한 번 만든 UI를 재사용)
    private readonly List<SupplyCostRowUI> costRows = new List<SupplyCostRowUI>(8);

    private void OnEnable()
    {
        sm = SaveManager.Instance;
        im = ItemManager.Instance;
        ucm = UpgradeCostManager.Instance;

        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnResourceChanged += HandleResourceChanged;

            sm.OnGoldChanged -= HandleGoldChanged;
            sm.OnGoldChanged += HandleGoldChanged;
        }

        if (unlockButton != null)
        {
            unlockButton.onClick.RemoveListener(OnClickUnlock);
            unlockButton.onClick.AddListener(OnClickUnlock);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnClickUpgrade);
            upgradeButton.onClick.AddListener(OnClickUpgrade);
        }

        if (!initialized) return;

        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = StartCoroutine(RefreshWhenReady());
    }

    private void OnDisable()
    {
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

    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh(); // 최초 1회 전체 갱신
    }

    private IEnumerator RefreshWhenReady()
    {
        yield return null;

        int safety = 200;
        while (safety-- > 0)
        {
            var cm = CharacterManager.Instance;

            if (cm != null && cm.IsLoaded &&
                ItemManager.Instance != null && ItemManager.Instance.IsLoaded &&
                UpgradeCostManager.Instance != null && UpgradeCostManager.Instance.IsLoaded)
            {
                break;
            }

            yield return null;
        }

        // 캐시 갱신
        sm = SaveManager.Instance;
        im = ItemManager.Instance;
        ucm = UpgradeCostManager.Instance;

        Refresh();
    }

    public void Refresh()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return;

        if (cm.CharacterItem == null || index < 0 || index >= cm.CharacterItem.Count)
        {
            ApplyUI(null);
            return;
        }

        item = cm.CharacterItem[index];

        // 고정 UI(아이콘/텍스트/패널) 반영
        ApplyUI(item);

        // 필요 재료 UI는 “빌드/업데이트 분리”
        if (item != null)
        {
            int step = item.item_num + 1;
            EnsureNeedSupplyRows(step);     // row 생성은 필요할 때만
            UpdateNeedSupplyRowsOnly();     // 수량/스프라이트 갱신
        }
    }

    private void ApplyUI(CharacterItem it)
    {
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

        if (unlockMain != null) unlockMain.SetActive(it.item_unlock);
        if (unlockDone != null) unlockDone.SetActive(it.item_upgrade);

        if (icon != null)
        {
            icon.enabled = (it.itemimg != null);
            icon.sprite = it.itemimg;
        }

        if (nameText != null) nameText.text = it.name;
        if (subText != null) subText.text = it.sub;
        if (speedText != null) speedText.text = $"{it.item_speed} Km / s";
        if (priceText != null) priceText.text = $"{NumberFormatter.FormatKorean(it.item_price)}원";

        // 버튼만 “가볍게” 갱신
        if (unlockButton != null)
        {
            bool prevOk = CanUnlockByPrevRule();
            bool notYetUnlocked = !it.item_unlock;
            bool haveGold = (sm != null) && (sm.GetGold() >= it.item_price);

            unlockButton.interactable = prevOk && notYetUnlocked && haveGold;
        }

        if (upgradeButton != null)
        {
            int step = it.item_num + 1;
            upgradeButton.interactable = (sm != null) && CanAffordCosts(step);
        }
    }

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

    private void OnClickUnlock()
    {
        if (item == null || sm == null) return;
        if (sm.GetGold() < item.item_price) return;

        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        sm.AddGold(-item.item_price);

        item.item_unlock = true;
        CharacterManager.Instance.SaveToJson();

        // 전체 Refresh는 OK (클릭은 드물어서)
        Refresh();
    }

    private void OnClickUpgrade()
    {
        if (item == null || sm == null) return;

        int step = item.item_num + 1;
        if (!CanAffordCosts(step)) return;

        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        SpendCosts(step);

        item.item_upgrade = true;
        item.item_unlock = true;

        CharacterManager.Instance.SaveToJson();

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

        MissionProgressManager.Instance?.Add("character_upgrade_count", 1);

        Refresh();
    }

    // -----------------------------
    // Need Supply UI 최적화 핵심
    // -----------------------------

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

        // 필요한 만큼만 생성, 나머지는 비활성
        for (int i = costRows.Count; i < needCount; i++)
        {
            var rowGO = Instantiate(supplyCostPrefab, needSupplyParent);
            var rowUI = rowGO.GetComponent<SupplyCostRowUI>();
            if (rowUI != null) costRows.Add(rowUI);
            else Destroy(rowGO);
        }

        // 활성/비활성만 정리 (Destroy 금지)
        for (int i = 0; i < costRows.Count; i++)
        {
            if (costRows[i] != null)
                costRows[i].gameObject.SetActive(i < needCount);
        }
    }

    private void UpdateNeedSupplyRowsOnly()
    {
        if (cachedCosts == null || cachedCosts.Count == 0) return;
        if (im == null || !im.IsLoaded) return;

        // 실제 표시 업데이트(스프라이트/필요수량)만
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

    // 이벤트 때는 “전체 Refresh” 대신 가벼운 갱신만
    private void HandleResourceChanged()
    {
        if (!initialized || item == null) return;

        // 버튼 상태 갱신
        if (upgradeButton != null)
            upgradeButton.interactable = (sm != null) && CanAffordCosts(item.item_num + 1);

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

    private void HandleGoldChanged()
    {
        if (!initialized || item == null) return;

        if (unlockButton != null)
        {
            bool prevOk = CanUnlockByPrevRule();
            bool notYetUnlocked = !item.item_unlock;
            bool haveGold = (sm != null) && (sm.GetGold() >= item.item_price);
            unlockButton.interactable = prevOk && notYetUnlocked && haveGold;
        }

        if (upgradeButton != null)
            upgradeButton.interactable = (sm != null) && CanAffordCosts(item.item_num + 1);
    }
}
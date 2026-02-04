using System.Collections;
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

    private void OnEnable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged += HandleResourceChanged;
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
        var sm = SaveManager.Instance;
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
        Refresh();
    }

    private IEnumerator RefreshWhenReady()
    {
        yield return null;

        int safety = 200;
        while (safety-- > 0)
        {
            var cm = CharacterManager.Instance;
            var im = ItemManager.Instance;
            var ucm = UpgradeCostManager.Instance;

            if (cm != null && cm.IsLoaded &&
                im != null && im.IsLoaded &&
                ucm != null && ucm.IsLoaded)
            {
                break;
            }

            yield return null;
        }

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
        ApplyUI(item);

        if (item != null)
            RefreshNeedSupplyUI(item.item_num + 1);
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

        var sm = SaveManager.Instance;

        if (unlockButton != null)
        {
            bool prevOk = CanUnlockByPrevRule();            // 단계 조건
            bool notYetUnlocked = !it.item_unlock;          // 이미 unlock이면 굳이 누를 필요 없음
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
        // 0번은 기본캐릭(무시), 1번은 "처음 시작"이라 조건 없이 열어줌
        if (index <= 1) return true;

        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded || cm.CharacterItem == null) return false;

        int prevIndex = index - 1;
        if (prevIndex < 0 || prevIndex >= cm.CharacterItem.Count) return false;

        var prev = cm.CharacterItem[prevIndex];
        if (prev == null) return false;

        return prev.item_upgrade; // 이전 캐릭 업그레이드 완료해야 unlock 가능
    }

    private void OnClickUnlock()
    {
        if (item == null) return;

        var sm = SaveManager.Instance;
        if (sm == null) return;

        if (sm.GetGold() < item.item_price) return;

        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        sm.AddGold(-item.item_price);

        item.item_unlock = true;
        CharacterManager.Instance.SaveToJson();

        Refresh();
    }

    private void OnClickUpgrade()
    {
        if (item == null) return;

        var sm = SaveManager.Instance;
        if (sm == null) return;

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
            nextIndex >= 0 && nextIndex < cm.CharacterItem.Count)
        {
            var next = cm.CharacterItem[nextIndex];

            sm.SetCurrentCharacterId(next.item_num); // 다음 캐릭으로 바꾸기
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

    private void RefreshNeedSupplyUI(int step)
    {
        if (needSupplyParent == null || supplyCostPrefab == null) return;

        var im = ItemManager.Instance;
        var ucm = UpgradeCostManager.Instance;

        if (im == null || !im.IsLoaded) return;
        if (ucm == null || !ucm.IsLoaded) return;

        for (int i = needSupplyParent.childCount - 1; i >= 0; i--)
            Destroy(needSupplyParent.GetChild(i).gameObject);

        var costs = ucm.GetCostsByStep(step);

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];

            var rowGO = Instantiate(supplyCostPrefab, needSupplyParent);
            var rowUI = rowGO.GetComponent<SupplyCostRowUI>();

            Sprite spr = null;
            var matItem = im.GetItem(c.itemId);
            if (matItem != null) spr = matItem.itemimg;

            if (rowUI != null)
                rowUI.Set(spr, c.count);
        }
    }

    private bool CanAffordCosts(int step)
    {
        var sm = SaveManager.Instance;
        var ucm = UpgradeCostManager.Instance;

        if (sm == null) return false;
        if (ucm == null || !ucm.IsLoaded) return false;

        var costs = ucm.GetCostsByStep(step);
        if (costs == null || costs.Count == 0) return true;

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            int have = sm.GetResource(c.itemId);
            if (have < c.count) return false;
        }

        return true;
    }

    private void SpendCosts(int step)
    {
        var sm = SaveManager.Instance;
        var ucm = UpgradeCostManager.Instance;

        if (sm == null) return;
        if (ucm == null || !ucm.IsLoaded) return;

        var costs = ucm.GetCostsByStep(step);

        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            sm.AddResource(c.itemId, -c.count);
        }
    }

    private void HandleResourceChanged() => Refresh();
    private void HandleGoldChanged() => Refresh();
}
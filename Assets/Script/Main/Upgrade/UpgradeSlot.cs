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
    [SerializeField] private TextMeshProUGUI NameText;
    [SerializeField] private TextMeshProUGUI SubText;
    [SerializeField] private TextMeshProUGUI SpeedText;

    [Header("Panel")]
    [SerializeField] private GameObject Unlock_Main;
    [SerializeField] private GameObject Unlock_Done;

    [Header("MainPanel")]
    [SerializeField] private TextMeshProUGUI PriceText;
    [SerializeField] private Button UnlockButton;

    [Header("UpgradePanel")]
    [SerializeField] private Button UpgradeButton;

    [Header("Need Supply UI")]
    [SerializeField] private Transform needSupplyParent;   // Panel_Need_Supply
    [SerializeField] private GameObject supplyCostPrefab;  // Panel_Supply_Cost 프리팹

    private CharacterItem item;
    private Coroutine refreshCo;

    private void OnEnable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnResourceChanged += HandleResourceChanged;
            SaveManager.Instance.OnGoldChanged += HandleGoldChanged;
        }

        if (UnlockButton != null)
        {
            UnlockButton.onClick.RemoveListener(isUnlock);
            UnlockButton.onClick.AddListener(isUnlock);
        }

        if (UpgradeButton != null)
        {
            UpgradeButton.onClick.RemoveListener(isUpgrade);
            UpgradeButton.onClick.AddListener(isUpgrade);
        }

        if (!initialized) return;

        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = StartCoroutine(RefreshWhenReady());
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnResourceChanged -= HandleResourceChanged;
            SaveManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }

        if (UnlockButton != null)
            UnlockButton.onClick.RemoveListener(isUnlock);

        if (UpgradeButton != null)
            UpgradeButton.onClick.RemoveListener(isUpgrade);

        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = null;
    }

    // UpgradeManager에서 슬롯 생성할 때 호출
    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh(); // 즉시 1회
    }

    private IEnumerator RefreshWhenReady()
    {
        yield return null; // 1프레임 대기

        // 매니저 로드 완료까지 대기 (패널 토글 시 타이밍 문제 방지)
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

        // 패널 다시 켤 때도 항상 재료 UI 다시 빌드
        if (item != null)
            RefreshNeedSupplyUI(item.item_num + 1);
    }

    private void ApplyUI(CharacterItem it)
    {
        if (it == null)
        {
            if (icon) { icon.sprite = null; icon.enabled = false; }
            if (NameText) NameText.text = "";
            if (SubText) SubText.text = "";
            if (SpeedText) SpeedText.text = "";
            if (PriceText) PriceText.text = "";
            if (Unlock_Main) Unlock_Main.SetActive(false);
            if (Unlock_Done) Unlock_Done.SetActive(false);
            if (UnlockButton) UnlockButton.interactable = false;
            if (UpgradeButton) UpgradeButton.interactable = false;
            return;
        }

        // 패널 토글(원하는 규칙대로)
        if (Unlock_Main) Unlock_Main.SetActive(it.item_unlock);
        if (Unlock_Done) Unlock_Done.SetActive(it.item_upgrade);

        // 아이콘
        if (icon != null)
        {
            icon.enabled = (it.itemimg != null);
            icon.sprite = it.itemimg;
        }

        // 텍스트
        if (NameText) NameText.text = it.name;
        if (SubText) SubText.text = it.sub;
        if (SpeedText) SpeedText.text = $"{it.item_speed} Km / s";
        if (PriceText) PriceText.text = $"{FormatKoreanNumber(it.item_price)}원";

        // 버튼 interactable
        if (UnlockButton != null && SaveManager.Instance != null)
            UnlockButton.interactable = (it.item_price <= SaveManager.Instance.GetGold());

        if (UpgradeButton != null && SaveManager.Instance != null)
        {
            int step = it.item_num + 1;
            UpgradeButton.interactable = CanAffordCosts(step);
        }
    }

    private void isUnlock()
    {
        if (item == null) return;
        if (SaveManager.Instance == null) return;

        long price = item.item_price;
        if (SaveManager.Instance.GetGold() < price) return;

        SaveManager.Instance.AddGold(-price);

        item.item_unlock = true;
        CharacterManager.Instance.SaveToJson();

        // 즉시 UI 반영
        ApplyUI(item);
        RefreshNeedSupplyUI(item.item_num + 1);
    }

    private void isUpgrade()
    {
        if (item == null) return;
        if (SaveManager.Instance == null) return;

        int step = item.item_num + 1;

        // 재료 충분한지 체크
        if (!CanAffordCosts(step)) return;

        // 재료 차감
        SpendCosts(step);

        // 업그레이드 완료 처리(너 규칙대로)
        item.item_upgrade = true;          // 업그레이드 완료 표시
        item.item_unlock = true;           // 필요하면 유지 (이미 unlock일 수도)
        CharacterManager.Instance.SaveToJson();

        var cm = CharacterManager.Instance;
        int nextIndex = index + 1;
        if (cm != null && cm.IsLoaded && nextIndex >= 0 && nextIndex < cm.CharacterItem.Count)
        {
            var next = cm.CharacterItem[nextIndex];
            SaveManager.Instance.SetCurrentCharacterId(item.item_num);
            SaveManager.Instance.SetSpeed(next.item_speed);
        }
        else
        {
            // 다음이 없으면 현재 item_speed라도 적용
            SaveManager.Instance.SetCurrentCharacterId(item.item_num);
            SaveManager.Instance.SetSpeed(item.item_speed);
        }

        MissionProgressManager.Instance?.Add("character_upgrade_count", 1);

        Refresh();
    }

    private void RefreshNeedSupplyUI(int step)
    {
        if (needSupplyParent == null || supplyCostPrefab == null) return;

        var im = ItemManager.Instance;
        var ucm = UpgradeCostManager.Instance;

        // 로드 안 됐으면 "삭제하지 말고" 그냥 나감 (사라짐 방지)
        if (im == null || !im.IsLoaded) return;
        if (ucm == null || !ucm.IsLoaded) return;

        // 이제 안전하게 삭제/재생성
        for (int i = needSupplyParent.childCount - 1; i >= 0; i--)
            Destroy(needSupplyParent.GetChild(i).gameObject);

        var costs = ucm.GetCostsByStep(step);

        foreach (var c in costs)
        {
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
        var ucm = UpgradeCostManager.Instance;
        if (ucm == null || !ucm.IsLoaded) return false;

        var costs = ucm.GetCostsByStep(step);
        if (costs == null || costs.Count == 0) return true; // 비용 없으면 가능

        foreach (var c in costs)
        {
            int have = SaveManager.Instance.GetResource(c.itemId); // 네 함수명으로 맞추기
            if (have < c.count) return false;
        }
        return true;
    }

    private void SpendCosts(int step)
    {
        var ucm = UpgradeCostManager.Instance;
        var costs = ucm.GetCostsByStep(step);

        foreach (var c in costs)
        {
            SaveManager.Instance.AddResource(c.itemId, -c.count); // 네 함수명으로 맞추기
        }
    }

    private void HandleResourceChanged() => Refresh();
    private void HandleGoldChanged() => Refresh();

    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";
        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;
        const ulong EOK = 100_000_000UL;
        const ulong JO = 1_000_000_000_000UL;
        const ulong GYEONG = 10_000_000_000_000_000UL;

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(jo).Append("조"); }
        if (eok > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(eok).Append("억"); }
        if (man > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(man).Append("만"); }
        if (rest > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(rest); }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SupplySellSlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;
    private bool initialized = false;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Button")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI buttonText;

    private SupplyItem item;

    private void OnEnable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnResourceChanged += HandleResourceChanged;
            SaveManager.Instance.OnGoldChanged += HandleGoldChanged;
        }

        // 버튼 클릭 연결(중복 방지 위해 OnEnable에서 한번 세팅)
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickSellAll);
            button.onClick.AddListener(OnClickSellAll);
        }

        if (!initialized) return;
        Refresh();
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnResourceChanged -= HandleResourceChanged;
            SaveManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }

        if (button != null)
            button.onClick.RemoveListener(OnClickSellAll);
    }

    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh();
    }

    public void Refresh()
    {
        var im = ItemManager.Instance;
        if (im == null || !im.IsLoaded) return;

        if (im.SupplyItem == null || index < 0 || index >= im.SupplyItem.Count)
        {
            ApplyUI(null);
            return;
        }

        item = im.SupplyItem[index];
        ApplyUI(item);
    }

    private void ApplyUI(SupplyItem it)
    {
        // 아이콘
        if (icon != null)
        {
            if (it != null && it.itemimg != null)
            {
                icon.enabled = true;
                icon.sprite = it.itemimg;
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;

                if (it != null)
                    Debug.LogWarning($"[SupplySlot] Sprite missing: index={index}, name={it.name}, path={it.spritePath}");
            }
        }

        if (nameText != null)
        {
            if (it == null)
            {
                nameText.text = "";
                if (countText != null) countText.text = "";
                if (buttonText != null) buttonText.text = "";
                if (button != null) button.interactable = false;
                return;
            }

            string safeName = string.IsNullOrWhiteSpace(it.name) ? $"Item {index}" : it.name;
            nameText.text = $"{safeName}";

            int owned = SaveManager.Instance.GetResource(it.item_num);
            if (countText != null)
                countText.text = $"{FormatKoreanNumber(owned)}개";

            long totalPrice = (long)owned * it.item_price;
            if (buttonText != null)
                buttonText.text = $"{FormatKoreanNumber(totalPrice)}원";

            // 0개면 버튼 비활성화
            if (button != null)
                button.interactable = owned > 0;
        }
    }

    // 버튼 누르면 전부 판매
    private void OnClickSellAll()
    {
        if (item == null || SaveManager.Instance == null) return;

        int owned = SaveManager.Instance.GetResource(item.item_num);
        if (owned <= 0) return;

        long totalPrice = (long)owned * item.item_price;

        // 골드 지급
        SaveManager.Instance.AddGold(totalPrice);

        // 자원 전부 차감 (owned 만큼 빼기)
        SaveManager.Instance.AddResource(item.item_num, -owned);

        // 이벤트로 Refresh 되지만, 클릭 직후 즉시 반영 원하면 한 번 더
        Refresh();
    }

    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";

        // 음수도 안전하게
        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;                 // 10^4
        const ulong EOK = 100_000_000UL;            // 10^8
        const ulong JO = 1_000_000_000_000UL;      // 10^12
        const ulong GYEONG = 10_000_000_000_000_000UL; // 10^16

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(jo).Append("조");
        }
        if (eok > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(eok).Append("억");
        }
        if (man > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(man).Append("만");
        }
        if (rest > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(rest);
        }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }

    private void HandleResourceChanged() => Refresh();
    private void HandleGoldChanged() => Refresh();
}
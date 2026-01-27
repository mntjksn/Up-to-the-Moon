using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SupplySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;
    private bool initialized = false;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;

    private SupplyItem item;

    private void OnEnable()
    {
        // Instantiate 직후(Setup 전) 방어
        if (!initialized) return;
        Refresh();
    }

    // StorageManager에서 슬롯 생성할 때 호출
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

        // 인덱스 범위 체크
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

        // 텍스트
        if (countText != null)
        {
            if (it == null)
            {
                countText.text = "";
                return;
            }

            // name 비어도 안전
            string safeName = string.IsNullOrWhiteSpace(it.name)
                ? $"Item {index}"
                : it.name;

            int owned = SaveManager.Instance.GetResource(it.item_num);
            countText.text = $"{FormatKoreanNumber(owned)}개";
        }
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
}
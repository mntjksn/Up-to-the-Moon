using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SupplyCostRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;

    public void Set(Sprite sprite, int needCount)
    {
        if (icon != null)
        {
            icon.enabled = (sprite != null);
            icon.sprite = sprite;
        }

        if (countText != null)
            countText.text = $"{FormatKoreanNumber(needCount)}개";
    }

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
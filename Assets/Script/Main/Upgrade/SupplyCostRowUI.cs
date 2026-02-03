using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 강화 비용 행 1줄 UI (아이콘 + 필요 개수)
public class SupplyCostRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;

    // 외부에서 호출: 아이콘과 필요 개수 세팅
    public void Set(Sprite sprite, int needCount)
    {
        // 아이콘 표시
        if (icon != null)
        {
            icon.enabled = (sprite != null);
            icon.sprite = sprite;
        }

        // 필요 개수 텍스트
        if (countText != null)
            countText.text = $"{NumberFormatter.FormatKorean(needCount)}개";
    }
}
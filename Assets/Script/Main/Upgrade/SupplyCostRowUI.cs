using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    SupplyCostRowUI

    [역할]
    - “강화/업그레이드 필요 재료” UI의 1줄(row)을 표시한다.
      (아이콘 + 필요 개수 텍스트)

    [설계 의도]
    1) 단순/경량 UI 컴포넌트
       - 외부에서 Set(sprite, needCount)만 호출하면 즉시 표시가 갱신된다.
       - 자체적으로 데이터를 저장하거나 매니저를 참조하지 않고, 표시만 담당한다.

    2) null-safe 처리
       - icon/countText 참조가 비어 있어도 오류 없이 동작하도록 null 체크를 한다.

    [주의/전제]
    - NumberFormatter.FormatKorean(int) 함수가 존재해야 한다.
    - icon / countText는 프리팹 인스펙터에서 연결되어 있어야 한다.
*/

// 강화 비용 행 1줄 UI (아이콘 + 필요 개수)
public class SupplyCostRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;            // 재료 아이콘
    [SerializeField] private TextMeshProUGUI countText; // 필요 개수 텍스트

    /*
        외부에서 호출: 아이콘과 필요 개수 세팅
        - sprite: 표시할 아이콘(없으면 아이콘 비활성)
        - needCount: 필요한 개수(한글 포맷 + "개" 접미사)
    */
    public void Set(Sprite sprite, int needCount)
    {
        // 아이콘 표시(스프라이트가 없으면 숨김)
        if (icon != null)
        {
            icon.enabled = (sprite != null);
            icon.sprite = sprite;
        }

        // 필요 개수 텍스트 표시
        if (countText != null)
            countText.text = $"{NumberFormatter.FormatKorean(needCount)}개";
    }
}
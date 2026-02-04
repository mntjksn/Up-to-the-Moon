using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    BookSupplyPrefab

    [역할]
    - 광물 사전(Book Supply)에서 선택된 광물의
      아이콘, 이름, 설명을 표시하는 상세 패널 UI를 담당한다.
    - 전달받은 인덱스를 기준으로 ItemManager의 SupplyItem 데이터를 참조하여
      UI를 갱신한다.

    [설계 의도]
    1) 데이터 직접 참조
       - 별도의 데이터 복사 없이 ItemManager의 리스트를 직접 조회하여
         항상 최신 데이터를 표시한다.

    2) UI 변경 최소화
       - 마지막으로 적용된 Sprite/문자열을 캐시하고,
         값이 변경된 경우에만 UI를 갱신하여
         불필요한 Canvas 리빌드 비용을 줄인다.

    3) 안전한 접근
       - ItemManager 존재 여부, 로드 완료 여부, 인덱스 범위를 모두 검사하여
         런타임 오류를 방지한다.
*/
public class BookSupplyPrefab : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image thisimg;              // 광물 아이콘
    [SerializeField] private TextMeshProUGUI chname;    // 광물 이름
    [SerializeField] private TextMeshProUGUI sub;       // 광물 설명

    // 현재 표시 중인 데이터 인덱스
    private int bookIndex = -1;

    // ---- 마지막 반영 값 캐시 ----
    private Sprite lastSprite;
    private string lastName;
    private string lastSub;

    /*
        상세 패널 초기화

        - 표시할 광물의 인덱스를 전달받는다.
        - 이전 인덱스와 다르면 캐시를 무효화하여
          UI가 반드시 갱신되도록 한다.
    */
    public void Init(int index)
    {
        if (bookIndex != index)
        {
            lastSprite = null;
            lastName = null;
            lastSub = null;
        }

        bookIndex = index;
        Refresh();
    }

    /*
        현재 인덱스에 해당하는 데이터로 UI 갱신
    */
    public void Refresh()
    {
        ItemManager item = ItemManager.Instance;
        if (item == null || !item.IsLoaded) return;

        var list = item.SupplyItem;
        if (list == null || (uint)bookIndex >= (uint)list.Count) return;

        SupplyItem it = list[bookIndex];
        if (it == null) return;

        // 아이콘 갱신
        if (thisimg != null && it.itemimg != null && lastSprite != it.itemimg)
        {
            thisimg.sprite = it.itemimg;
            lastSprite = it.itemimg;
        }

        // 이름 갱신
        if (chname != null && it.name != null && !string.Equals(lastName, it.name))
        {
            chname.text = it.name;
            lastName = it.name;
        }

        // 설명 갱신
        if (sub != null && it.sub != null && !string.Equals(lastSub, it.sub))
        {
            sub.text = it.sub;
            lastSub = it.sub;
        }
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    BookSkyPrefab

    [역할]
    - "지역 사전(도감)" 슬롯 하나를 담당하는 UI 컴포넌트이다.
    - BackgroundManager의 BackgroundItem 데이터를 기반으로
      이미지, 이름, 설명 텍스트를 표시한다.

    [설계 의도]
    1) 인덱스 기반 바인딩
       - Init(index)를 통해 자신이 표시할 데이터 인덱스를 저장한다.
       - 슬롯 생성 시 한 번만 인덱스를 지정하고, 이후 Refresh에서 해당 인덱스만 참조한다.
    2) UI 갱신 최소화
       - 마지막으로 적용한 Sprite / 문자열을 캐시(lastSprite, lastName, lastSub)한다.
       - 값이 실제로 변경된 경우에만 UI를 갱신하여 불필요한 Canvas Rebuild를 줄인다.
    3) 안전성 고려
       - BackgroundManager가 없거나 아직 로드되지 않은 경우 즉시 리턴한다.
       - 인덱스 범위 체크를 통해 예외를 방지한다.
*/
public class BookSkyPrefab : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image thisimg;              // 지역 이미지
    [SerializeField] private TextMeshProUGUI chname;    // 지역 이름
    [SerializeField] private TextMeshProUGUI sub;       // 지역 설명

    // 이 슬롯이 참조하는 BackgroundItem 인덱스
    private int bookIndex = -1;

    // 마지막 반영값 캐시 (불필요한 UI rebuild 방지)
    private Sprite lastSprite;
    private string lastName;
    private string lastSub;

    /*
        슬롯 초기화

        - 외부(BookSkyManager)에서 인덱스를 전달받는다.
        - 인덱스가 바뀌면 캐시를 무효화하여 다음 Refresh에서
          반드시 UI가 갱신되도록 한다.
    */
    public void Init(int index)
    {
        bookIndex = index;

        // index 변경 시 캐시 초기화
        lastSprite = null;
        lastName = null;
        lastSub = null;

        Refresh();
    }

    /*
        슬롯 내용 갱신

        - BackgroundManager에서 데이터 획득
        - 캐시와 비교하여 실제 값이 바뀐 경우에만 UI 변경
    */
    public void Refresh()
    {
        var bg = BackgroundManager.Instance;
        if (bg == null || !bg.IsLoaded) return;

        var list = bg.BackgroundItem;
        if (list == null) return;

        // 빠른 범위 체크 (음수/초과 모두 방지)
        if ((uint)bookIndex >= (uint)list.Count) return;

        var item = list[bookIndex];
        if (item == null) return;

        // 이미지 갱신
        if (thisimg != null &&
            item.itemimg != null &&
            lastSprite != item.itemimg)
        {
            thisimg.sprite = item.itemimg;
            lastSprite = item.itemimg;
        }

        // 이름 텍스트 갱신
        if (chname != null &&
            item.name != null &&
            !string.Equals(lastName, item.name))
        {
            chname.text = item.name;
            lastName = item.name;
        }

        // 설명 텍스트 갱신
        if (sub != null &&
            item.sub != null &&
            !string.Equals(lastSub, item.sub))
        {
            sub.text = item.sub;
            lastSub = item.sub;
        }
    }
}
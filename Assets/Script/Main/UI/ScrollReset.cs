using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/*
    ScrollReset

    [역할]
    - 오브젝트가 활성화될 때(패널이 열릴 때)
      ScrollRect의 스크롤 위치를 항상 최상단으로 되돌린다.

    [설계 의도]
    1) 레이아웃 갱신 이후에 위치 설정
       - OnEnable에서 바로 값을 넣으면,
         Content Size Fitter / Layout Group 등의 영향으로
         다음 프레임에 위치가 다시 바뀌는 경우가 있다.
       - 그래서 코루틴으로 한 프레임 대기 후 위치를 설정한다.

    2) 컴포넌트 강제 보장
       - [RequireComponent(typeof(ScrollRect))]로
         이 스크립트가 붙어 있는 오브젝트에는
         반드시 ScrollRect가 함께 존재하도록 한다.

    3) 캐싱 사용
       - Awake에서 ScrollRect를 한 번만 GetComponent하여
         매번 호출 비용을 줄인다.

    [주의/전제]
    - 세로 스크롤을 사용하는 ScrollRect 기준이다.
    - horizontalNormalizedPosition은 건드리지 않는다.
    - verticalNormalizedPosition:
        1 = 최상단
        0 = 최하단
*/
[RequireComponent(typeof(ScrollRect))]
public class ScrollReset : MonoBehaviour
{
    private ScrollRect scrollRect; // ScrollRect 캐시

    private void Awake()
    {
        // ScrollRect 캐싱
        scrollRect = GetComponent<ScrollRect>();
    }

    private void OnEnable()
    {
        // 레이아웃 갱신 이후에 위치를 맞추기 위해
        StartCoroutine(ResetNextFrame());
    }

    /*
        한 프레임 대기 후 스크롤 위치 초기화
        - UI 레이아웃 계산이 끝난 뒤 실행된다.
    */
    private IEnumerator ResetNextFrame()
    {
        yield return null;

        // 세로 스크롤 최상단 (1 = 위, 0 = 아래)
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }
}
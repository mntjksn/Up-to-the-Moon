using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ScrollRect가 활성화될 때
// 스크롤 위치를 최상단으로 초기화하는 스크립트
public class ScrollReset : MonoBehaviour
{
    // 제어할 ScrollRect 컴포넌트
    private ScrollRect scrollRect;

    private void OnEnable()
    {
        // ScrollRect 캐싱
        scrollRect = GetComponent<ScrollRect>();

        // UI 레이아웃 갱신 이후에 위치를 맞추기 위해
        // 다음 프레임에서 스크롤 위치 리셋
        StartCoroutine(ResetNextFrame());
    }

    // 한 프레임 대기 후 스크롤 위치 초기화
    private IEnumerator ResetNextFrame()
    {
        // Canvas / LayoutGroup 정렬 완료 대기
        yield return null;

        // 세로 스크롤 최상단으로 이동
        // (1 = top, 0 = bottom)
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 활성화될 때 스크롤을 최상단으로 되돌리는 스크립트
[RequireComponent(typeof(ScrollRect))]
public class ScrollReset : MonoBehaviour
{
    private ScrollRect scrollRect;

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

    // 한 프레임 대기 후 스크롤 위치 초기화
    private IEnumerator ResetNextFrame()
    {
        yield return null;

        // 세로 스크롤 최상단 (1 = 위, 0 = 아래)
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }
}
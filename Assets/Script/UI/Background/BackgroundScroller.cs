using UnityEngine;

public class BackgroundLoopUI : MonoBehaviour
{
    [Header("Assign 2 UI Images (RectTransform)")]
    public RectTransform bg1;
    public RectTransform bg2;

    [Header("Speed (pixels per second)")]
    public float moveSpeed = 200f;

    float height;

    void Start()
    {
        // bg의 실제 높이(픽셀). 보통 stretch면 rect.height가 화면 높이(예: 1920)로 나옴
        height = bg1.rect.height;

        // 초기 배치: bg1 위, bg2 아래 (원하는 방향대로)
        bg1.anchoredPosition = Vector2.zero;
        bg2.anchoredPosition = new Vector2(0f, height);
    }

    void Update()
    {
        float dy = moveSpeed * Time.deltaTime;

        bg1.anchoredPosition -= new Vector2(0f, dy);
        bg2.anchoredPosition -= new Vector2(0f, dy);

        // bg가 아래로 완전히 빠지면 다른 bg 위로 올려서 이어붙임
        if (bg1.anchoredPosition.y <= -height)
            bg1.anchoredPosition = new Vector2(0f, bg2.anchoredPosition.y + height);

        if (bg2.anchoredPosition.y <= -height)
            bg2.anchoredPosition = new Vector2(0f, bg1.anchoredPosition.y + height);
    }
}
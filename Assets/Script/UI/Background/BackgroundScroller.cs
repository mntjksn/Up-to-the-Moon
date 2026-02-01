using System.Collections;
using UnityEngine;

public class BackgroundLoopUI : MonoBehaviour
{
    [Header("Assign 2 UI Images (RectTransform)")]
    public RectTransform bg1;
    public RectTransform bg2;

    [Header("Speed (pixels per second)")]
    public float moveSpeed = 200f;

    float height;

    // 캐릭터 ID별 배경 속도 테이블
    float[] speedTable =
    {
        50f,    // 0
        55f,    // 1
        60f,    // 2
        65f,    // 3
        75f,    // 4
        125f,   // 5
        250f,   // 6
        500f,   // 7
        750f,   // 8
        1000f,  // 9
        1250f,  // 10
        1500f,  // 11
        2000f,  // 12
        2500f,  // 13
        3000f   // 14
    };

    void Start()
    {
        height = bg1.rect.height;

        bg1.anchoredPosition = Vector2.zero;
        bg2.anchoredPosition = new Vector2(0f, height);

        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        // SaveManager 준비될 때까지 대기
        while (SaveManager.Instance == null)
            yield return null;

        // 이벤트 구독
        SaveManager.Instance.OnCharacterChanged += ApplySpeedByCharacter;

        // 시작 시 현재 캐릭터 반영
        ApplySpeedByCharacter(SaveManager.Instance.GetCurrentCharacterId());
    }

    void OnDestroy()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnCharacterChanged -= ApplySpeedByCharacter;
    }

    void ApplySpeedByCharacter(int id)
    {
        if (id < 0 || id >= speedTable.Length)
            return;

        moveSpeed = speedTable[id];
    }

    void Update()
    {
        float dy = moveSpeed * Time.deltaTime;

        bg1.anchoredPosition -= new Vector2(0f, dy);
        bg2.anchoredPosition -= new Vector2(0f, dy);

        if (bg1.anchoredPosition.y <= -height)
            bg1.anchoredPosition =
                new Vector2(0f, bg2.anchoredPosition.y + height);

        if (bg2.anchoredPosition.y <= -height)
            bg2.anchoredPosition =
                new Vector2(0f, bg1.anchoredPosition.y + height);
    }
}
using System.Collections;
using UnityEngine;

public class BackgroundLoopUI : MonoBehaviour
{
    [Header("Assign 2 UI Images (RectTransform)")]
    [SerializeField] private RectTransform bg1;
    [SerializeField] private RectTransform bg2;

    [Header("Speed (pixels per second)")]
    [SerializeField] private float moveSpeed = 200f;

    private float height;
    private Coroutine initCo;

    // 캐릭터 ID별 배경 속도 테이블
    private readonly float[] speedTable =
    {
        50f,   // 0
        55f,   // 1
        60f,   // 2
        65f,   // 3
        75f,   // 4
        125f,  // 5
        250f,  // 6
        500f,  // 7
        750f,  // 8
        1000f, // 9
        1250f, // 10
        1500f, // 11
        2000f, // 12
        2500f, // 13
        3000f  // 14
    };

    private void Start()
    {
        InitPositions();
    }

    private void OnEnable()
    {
        InitPositions(); // 씬/패널 재활성 시 위치 꼬임 방지(선택)
        TryBindToSaveManager();
    }

    private void OnDisable()
    {
        UnbindFromSaveManager();

        if (initCo != null)
        {
            StopCoroutine(initCo);
            initCo = null;
        }
    }

    private void InitPositions()
    {
        if (bg1 == null || bg2 == null) return;

        // height가 0으로 잡히는 경우(레이아웃 아직 안 잡힘) 대비
        height = bg1.rect.height;
        if (height <= 0f) height = 1f;

        bg1.anchoredPosition = Vector2.zero;
        bg2.anchoredPosition = new Vector2(0f, height);
    }

    private void TryBindToSaveManager()
    {
        SaveManager save = SaveManager.Instance;

        if (save != null)
        {
            save.OnCharacterChanged += ApplySpeedByCharacter;
            ApplySpeedByCharacter(save.GetCurrentCharacterId());
            return;
        }

        if (initCo == null)
            initCo = StartCoroutine(WaitAndBind());
    }

    private IEnumerator WaitAndBind()
    {
        while (SaveManager.Instance == null)
            yield return null;

        initCo = null;

        SaveManager save = SaveManager.Instance;
        if (save == null) yield break;

        save.OnCharacterChanged += ApplySpeedByCharacter;
        ApplySpeedByCharacter(save.GetCurrentCharacterId());
    }

    private void UnbindFromSaveManager()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null) return;

        save.OnCharacterChanged -= ApplySpeedByCharacter;
    }

    private void ApplySpeedByCharacter(int id)
    {
        if ((uint)id >= (uint)speedTable.Length) return; // 범위체크 빠르게
        moveSpeed = speedTable[id];
    }

    private void Update()
    {
        if (bg1 == null || bg2 == null) return;

        float dy = moveSpeed * Time.deltaTime;

        // anchoredPosition get/set 최소화 (로컬로 계산 후 1번씩만 set)
        Vector2 p1 = bg1.anchoredPosition;
        Vector2 p2 = bg2.anchoredPosition;

        p1.y -= dy;
        p2.y -= dy;

        if (p1.y <= -height)
            p1.y = p2.y + height;

        if (p2.y <= -height)
            p2.y = p1.y + height;

        bg1.anchoredPosition = p1;
        bg2.anchoredPosition = p2;
    }
}
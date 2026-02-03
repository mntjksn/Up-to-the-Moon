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
        if (bg1 == null || bg2 == null) return;

        height = bg1.rect.height;

        bg1.anchoredPosition = Vector2.zero;
        bg2.anchoredPosition = new Vector2(0f, height);
    }

    private void OnEnable()
    {
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
        if (id < 0 || id >= speedTable.Length) return;
        moveSpeed = speedTable[id];
    }

    private void Update()
    {
        if (bg1 == null || bg2 == null) return;

        float dy = moveSpeed * Time.deltaTime;
        Vector2 delta = new Vector2(0f, dy);

        bg1.anchoredPosition -= delta;
        bg2.anchoredPosition -= delta;

        if (bg1.anchoredPosition.y <= -height)
            bg1.anchoredPosition = new Vector2(0f, bg2.anchoredPosition.y + height);

        if (bg2.anchoredPosition.y <= -height)
            bg2.anchoredPosition = new Vector2(0f, bg1.anchoredPosition.y + height);
    }
}
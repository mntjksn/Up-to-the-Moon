using System.Collections;
using UnityEngine;

/*
    BackgroundLoopUI

    [역할]
    - UI 배경 이미지 2장을 세로로 이어 붙여 무한 스크롤(루프) 효과를 만든다.
    - 캐릭터 ID에 따라 배경 이동 속도를 변경하여 연출을 강화한다.

    [설계 의도]
    - 배경을 2장만 사용하고 위치를 재배치하여 무한 루프를 구현한다(메모리/오브젝트 수 최소화).
    - 캐릭터 변경은 이벤트 기반으로 반영하여 불필요한 폴링을 줄인다.
    - 레이아웃이 아직 계산되지 않아 height가 0이 되는 상황을 방어한다.
    - Update에서는 anchoredPosition 접근/설정을 최소화하여 UI 갱신 비용을 줄인다.
*/
public class BackgroundLoopUI : MonoBehaviour
{
    [Header("Assign 2 UI Images (RectTransform)")]
    // 루프 스크롤에 사용할 UI 배경 2장(같은 크기 전제)
    [SerializeField] private RectTransform bg1;
    [SerializeField] private RectTransform bg2;

    [Header("Speed (pixels per second)")]
    // 배경 이동 속도(픽셀/초). 캐릭터에 따라 변경된다.
    [SerializeField] private float moveSpeed = 200f;

    // 배경 한 장의 높이(재배치 기준)
    private float height;

    // SaveManager가 늦게 생성되는 경우를 대비한 바인딩 코루틴
    private Coroutine initCo;

    // 캐릭터 ID별 배경 속도 테이블
    // 주의: 캐릭터 ID와 배열 인덱스가 1:1로 매칭된다는 전제이다.
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
        // 최초 실행 시 배경 위치를 기준 상태로 맞춘다.
        InitPositions();
    }

    private void OnEnable()
    {
        // 패널/오브젝트가 재활성화될 때 위치가 꼬일 수 있으므로 초기화한다.
        InitPositions();

        // 캐릭터 변경 이벤트를 구독하고, 현재 캐릭터 기준 속도를 즉시 적용한다.
        TryBindToSaveManager();
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제(메모리 누수/중복 호출 방지)
        UnbindFromSaveManager();

        // 바인딩 대기 코루틴이 돌고 있으면 중단한다.
        if (initCo != null)
        {
            StopCoroutine(initCo);
            initCo = null;
        }
    }

    /*
        배경 초기 위치 설정

        - bg1은 (0, 0), bg2는 바로 위(height)로 배치하여 연속된 스크롤처럼 보이게 한다.
        - 레이아웃 계산 전에는 rect.height가 0일 수 있으므로 안전 값으로 보정한다.
    */
    private void InitPositions()
    {
        if (bg1 == null || bg2 == null) return;

        height = bg1.rect.height;
        if (height <= 0f) height = 1f;

        bg1.anchoredPosition = Vector2.zero;
        bg2.anchoredPosition = new Vector2(0f, height);
    }

    /*
        SaveManager 이벤트 바인딩 시도

        - SaveManager가 이미 존재하면 즉시 이벤트를 구독한다.
        - 생성 순서에 따라 아직 없을 수 있으므로 없으면 코루틴으로 대기한다.
    */
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

    // SaveManager가 생성될 때까지 기다렸다가 이벤트를 구독한다.
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

    // 이벤트 구독 해제(비활성/파괴 시점에 호출된다).
    private void UnbindFromSaveManager()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null) return;

        save.OnCharacterChanged -= ApplySpeedByCharacter;
    }

    /*
        캐릭터 ID 기반 속도 적용

        - 배열 범위를 빠르게 체크하기 위해 uint 캐스팅을 사용한다.
        - 유효하지 않은 ID면 기존 속도를 유지한다.
    */
    private void ApplySpeedByCharacter(int id)
    {
        if ((uint)id >= (uint)speedTable.Length) return;
        moveSpeed = speedTable[id];
    }

    /*
        배경 루프 이동

        - 두 배경을 동일 속도로 아래로 이동시킨다.
        - 한 배경이 화면 아래로 완전히 내려가면, 다른 배경의 위로 재배치하여 루프를 만든다.
        - anchoredPosition 접근/설정을 로컬 변수로 처리하여 호출 횟수를 줄인다.
    */
    private void Update()
    {
        if (bg1 == null || bg2 == null) return;

        float dy = moveSpeed * Time.deltaTime;

        Vector2 p1 = bg1.anchoredPosition;
        Vector2 p2 = bg2.anchoredPosition;

        p1.y -= dy;
        p2.y -= dy;

        // 한 장이 완전히 내려가면 다른 장의 위로 붙여 루프를 유지한다.
        if (p1.y <= -height)
            p1.y = p2.y + height;

        if (p2.y <= -height)
            p2.y = p1.y + height;

        bg1.anchoredPosition = p1;
        bg2.anchoredPosition = p2;
    }
}
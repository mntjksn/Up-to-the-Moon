using System.Collections;
using UnityEngine;

/*
    PlayerFloatSwing

    [역할]
    - 플레이어(캐릭터)의 “둥둥 떠오름(상하 부유)” + “살짝 흔들림(회전)” 연출을 담당한다.
    - 캐릭터 ID에 따라(= 캐릭터마다 느낌 다르게) 부유/회전 파라미터를 테이블로 적용한다.

    [설계 의도]
    1) 이벤트 기반 파라미터 적용
       - 매 프레임 캐릭터 ID를 확인하지 않고,
         SaveManager.OnCharacterChanged 이벤트로만 float/swing 값을 갱신한다.

    2) 스냅(튀는 현상) 방지 옵션
       - recacheBasePosOnEnable=true면: 패널/오브젝트 켜질 때 현재 위치를 기준점으로 다시 잡는다.
       - false면: 최초 1회 기준점을 유지해서 켜질 때 “기준점 점프”를 방지한다.

    3) 움직임 계산 최소화
       - Transform 캐시, TWO_PI 상수 사용, Sin 기반으로 간단한 주기 운동 구현.
*/
public class PlayerFloatSwing : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] private float floatAmplitude = 0.25f; // 상하 진폭
    [SerializeField] private float floatSpeed = 1.2f;      // 상하 속도(주파수)

    [Header("Swing")]
    [SerializeField] private float swingAngle = 10f; // 회전 최대 각도
    [SerializeField] private float swingSpeed = 0.9f; // 회전 속도(주파수)

    [Header("Phase")]
    [SerializeField] private bool offsetByRandomPhase = true; // 개체마다 시작 위상 랜덤

    [Header("Base Position")]
    [SerializeField] private bool recacheBasePosOnEnable = true; // OnEnable 시 기준 위치 다시 캡쳐

    private Vector3 basePos;
    private float phaseA;
    private float phaseB;

    private Coroutine bindCo;
    private Transform tr;

    private const float TWO_PI = Mathf.PI * 2f;

    // 캐릭터 ID별 파라미터 테이블
    // x=floatAmplitude, y=floatSpeed, z=swingAngle, w=swingSpeed
    private readonly Vector4[] table =
    {
        new Vector4(0.5f,   0.25f, 15f, 0.1f),
        new Vector4(1f,     0.3f,  25f, 0.25f),
        new Vector4(0.75f,  0.2f,  20f, 0.2f),
        new Vector4(0.5f,   0.15f, 30f, 0.1f),
        new Vector4(0.5f,   0.05f, 15f, 0.05f),
        new Vector4(1f,     0.1f,  5f,  0.05f),
        new Vector4(0.2f,   0.3f,  5f,  0.5f),
        new Vector4(0.3f,   0.5f,  7.5f,0.75f),
        new Vector4(0.2f,   0.5f,  3.5f,0.35f),
        new Vector4(0.05f,  0.25f, 3.5f,0.5f),
        new Vector4(0.025f, 7.5f,  1f,  1.5f),
        new Vector4(0.05f,  15f,   2.5f,2f),
        new Vector4(0.25f,  0.25f, 2.5f,0.05f),
        new Vector4(0.025f, 25f,   5f,  0.25f),
        new Vector4(0.01f,  100f,  5f,  0.15f),
    };

    private void Awake()
    {
        tr = transform;

        // 랜덤 위상(동일한 움직임으로 “동기화” 되는 느낌 방지)
        if (offsetByRandomPhase)
        {
            phaseA = Random.Range(0f, 100f);
            phaseB = Random.Range(0f, 100f);
        }

        // 최초 1회 기준점 캡쳐
        // (recacheBasePosOnEnable=false면 이 값이 계속 유지됨)
        basePos = tr.position;
    }

    private void OnEnable()
    {
        // 켤 때 기준점 다시 잡을지 옵션
        if (recacheBasePosOnEnable)
            basePos = tr.position;

        if (bindCo != null) StopCoroutine(bindCo);
        bindCo = StartCoroutine(BindRoutine());
    }

    private void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        // 이벤트 해제
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnCharacterChanged -= ApplyByCharacter;
    }

    private IEnumerator BindRoutine()
    {
        // SaveManager 준비 대기
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        // 중복 구독 방지
        sm.OnCharacterChanged -= ApplyByCharacter;
        sm.OnCharacterChanged += ApplyByCharacter;

        // 현재 캐릭터 파라미터 즉시 적용
        ApplyByCharacter(sm.GetCurrentCharacterId());

        bindCo = null;
    }

    private void ApplyByCharacter(int id)
    {
        if ((uint)id >= (uint)table.Length) return;

        Vector4 v = table[id];
        floatAmplitude = v.x;
        floatSpeed = v.y;
        swingAngle = v.z;
        swingSpeed = v.w;
    }

    private void Update()
    {
        float t = Time.time;

        // 상하 부유
        float y = Mathf.Sin((t + phaseA) * floatSpeed * TWO_PI) * floatAmplitude;
        tr.position = basePos + new Vector3(0f, y, 0f);

        // Z 회전 흔들림
        float rotZ = Mathf.Sin((t + phaseB) * swingSpeed * TWO_PI) * swingAngle;
        tr.localRotation = Quaternion.Euler(0f, 0f, rotZ);
    }
}
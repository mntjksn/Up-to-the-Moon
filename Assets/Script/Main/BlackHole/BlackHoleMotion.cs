using UnityEngine;

/*
    BlackHoleMotion

    [역할]
    - 블랙홀 오브젝트에 지속적인 회전과 스케일 펄스(확대/축소) 연출을 적용한다.
    - 게임플레이 로직과 분리된 순수 연출용 컴포넌트이다.

    [설계 의도]
    - transform 참조를 캐시하여 Update에서의 접근 비용을 줄인다.
    - Sin 파형을 사용하여 자연스러운 주기적 스케일 변화를 만든다.
*/
public class BlackHoleMotion : MonoBehaviour
{
    [Header("Rotate")]
    // 초당 회전 각도
    [SerializeField] private float rotateSpeed = 180f;

    [Header("Pulse (Scale)")]
    // 펄스 변화 속도
    [SerializeField] private float pulseSpeed = 2.2f;

    // 스케일 변화량(±값)
    [SerializeField] private float pulseAmount = 0.12f;

    // 초기 스케일(기준값)
    private Vector3 baseScale;

    // transform 캐시(성능 최적화)
    private Transform cachedTr;

    // 2 * PI 상수(삼각함수 계산용)
    private const float TAU = 6.28318530718f;

    private void Awake()
    {
        // transform을 캐시하고 기준 스케일을 저장한다.
        cachedTr = transform;
        baseScale = cachedTr.localScale;
    }

    private void Update()
    {
        // 혹시 오브젝트가 비활성 상태면 연산을 스킵한다.
        if (!cachedTr.gameObject.activeInHierarchy) return;

        // Z축 기준 회전 연출
        cachedTr.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        // Sin 파형을 이용해 0~1 범위의 보간값을 만든다.
        float t = (Mathf.Sin(Time.time * pulseSpeed * TAU) + 1f) * 0.5f;

        // 기준 스케일에서 ±pulseAmount 범위로 보간한다.
        float scale = Mathf.Lerp(1f - pulseAmount, 1f + pulseAmount, t);

        // 기준 스케일에 보간된 배율을 곱하여 적용한다.
        cachedTr.localScale = baseScale * scale;
    }
}
using UnityEngine;

/*
    ResourcePickupVFX

    [역할]
    - 자원 픽업 연출 오브젝트를 블랙홀(타겟)로 끌어당기며 회전/축소시키는 VFX 컴포넌트이다.
    - VFX 수명은 "스케일이 일정 이하(killScale)가 되면 종료" 규칙으로 관리한다.
    - 오브젝트 풀(ResourcePickupVFXPool)이 존재하면 재사용하고, 없으면 Destroy로 정리한다.

    [설계 의도]
    1) 풀링 지원
       - Init()에서 스프라이트/타겟/스케일을 초기화하여 재사용을 안전하게 만든다.
       - ReturnToPool()에서 pool이 있으면 Release, 없으면 Destroy로 동작한다.
    2) 캐싱으로 비용 절감
       - transform, SpriteRenderer를 Awake에서 캐싱하여 매 프레임 GetComponent 호출을 피한다.
    3) 회전 처리 최적화
       - RotateAround 대신 (pos - targetPos) 벡터를 직접 회전시키는 방식으로 2D 회전을 구현한다.
       - 불필요한 함수 호출을 줄이고, 원하는 회전축(z)만 다룰 수 있다.
*/
public class ResourcePickupVFX : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float pullSpeed = 4f;      // 타겟으로 끌어당기는 속도
    [SerializeField] private float rotateSpeed = 180f;  // 초당 회전 각도(도)
    [SerializeField] private float shrinkSpeed = 2f;    // 초당 축소량
    [SerializeField] private float killScale = 0.05f;   // 이 스케일 이하가 되면 종료(반납)

    // 빨려 들어갈 대상(블랙홀 Transform)
    private Transform target;

    // 표시용 스프라이트 렌더러(자원 아이콘)
    private SpriteRenderer sr;

    // 풀에서 재사용할 때 원래 스케일로 복구하기 위한 기준값
    private Vector3 startScale;

    // 풀 참조(없으면 Destroy로 정리)
    private ResourcePickupVFXPool pool;

    // Transform 캐시(매 프레임 property 접근 비용 최소화)
    private Transform cachedTr;

    private void Awake()
    {
        cachedTr = transform;
        sr = GetComponentInChildren<SpriteRenderer>();
        startScale = cachedTr.localScale;
    }

    // 풀에서 VFX 생성 시 주입한다.
    public void SetPool(ResourcePickupVFXPool p) => pool = p;

    /*
        풀 재사용 초기화

        - sprite: 표시할 아이콘 스프라이트
        - targetTr: 빨려 들어갈 타겟(블랙홀)

        재사용 오브젝트는 이전 상태(스케일/타겟)가 남아있을 수 있으므로
        Init에서 반드시 초기 상태로 되돌린다.
    */
    public void Init(Sprite sprite, Transform targetTr)
    {
        target = targetTr;

        if (sr != null)
            sr.sprite = sprite;

        // 재사용 시 스케일 초기화(수명 규칙이 스케일 기반이므로 필수)
        cachedTr.localScale = startScale;

        // 필요하면 회전 초기화도 가능하다.
        // cachedTr.rotation = Quaternion.identity;
    }

    private void Update()
    {
        // 타겟이 없어지면 즉시 풀로 반납한다(씬 전환/오브젝트 파괴 대비)
        if (target == null)
        {
            ReturnToPool();
            return;
        }

        float dt = Time.deltaTime;

        Vector3 targetPos = target.position;
        Vector3 pos = cachedTr.position;

        // 1) 타겟 쪽으로 끌어당기기(프레임 독립)
        pos = Vector3.MoveTowards(pos, targetPos, pullSpeed * dt);

        // 2) 회전 효과
        // RotateAround를 쓰지 않고, offset 벡터를 직접 회전시켜 2D 회전을 구현한다.
        Vector3 offset = pos - targetPos;

        float angle = rotateSpeed * dt;         // 이번 프레임 회전 각도(도)
        float rad = angle * Mathf.Deg2Rad;      // 라디안 변환
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        float x = offset.x * cos - offset.y * sin;
        float y = offset.x * sin + offset.y * cos;

        pos = targetPos + new Vector3(x, y, offset.z);
        cachedTr.position = pos;

        // 3) 축소로 수명 관리
        // localScale get/set을 최소화하기 위해 한 번만 읽고 계산 후 다시 세팅한다.
        Vector3 sc = cachedTr.localScale;

        float s = sc.x - shrinkSpeed * dt;
        if (s <= killScale)
        {
            ReturnToPool();
            return;
        }

        // 음수 방지(안전)
        if (s < 0f) s = 0f;

        sc.x = s;
        sc.y = s;
        sc.z = startScale.z; // z는 원래 값 유지(보통 1)
        cachedTr.localScale = sc;
    }

    /*
        VFX 종료 처리

        - 풀을 사용 중이면 Release로 반납한다.
        - 풀이 없으면 Destroy로 정리한다.
    */
    private void ReturnToPool()
    {
        target = null;

        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }
}
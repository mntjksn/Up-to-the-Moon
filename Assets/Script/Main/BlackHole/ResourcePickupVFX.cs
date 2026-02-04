using UnityEngine;

public class ResourcePickupVFX : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float pullSpeed = 4f;
    [SerializeField] private float rotateSpeed = 180f;
    [SerializeField] private float shrinkSpeed = 2f;
    [SerializeField] private float killScale = 0.05f;

    private Transform target;
    private SpriteRenderer sr;

    private Vector3 startScale;
    private ResourcePickupVFXPool pool;

    private Transform cachedTr;

    private void Awake()
    {
        cachedTr = transform;
        sr = GetComponentInChildren<SpriteRenderer>();
        startScale = cachedTr.localScale;
    }

    // 풀에서 주입
    public void SetPool(ResourcePickupVFXPool p) => pool = p;

    /// <summary>
    /// 풀 재사용 Init
    /// </summary>
    public void Init(Sprite sprite, Transform targetTr)
    {
        target = targetTr;

        if (sr != null)
            sr.sprite = sprite;

        // 재사용할 때 상태 초기화
        cachedTr.localScale = startScale;

        // (선택) 혹시 풀에서 꺼낼 때 위치/회전을 세팅 안 해주는 경우 대비
        // 풀에서 spawnPos로 이미 배치하고 있으니 보통 필요없지만, 안전하게 남겨둠
        // cachedTr.rotation = Quaternion.identity;
    }

    private void Update()
    {
        if (target == null)
        {
            ReturnToPool();
            return;
        }

        float dt = Time.deltaTime;

        Vector3 targetPos = target.position;
        Vector3 pos = cachedTr.position;

        // 1) 타겟 쪽으로 끌어당기기
        pos = Vector3.MoveTowards(pos, targetPos, pullSpeed * dt);

        // 2) 회전 효과 (RotateAround 대신: (pos-target)을 회전시켜 다시 더함)
        Vector3 offset = pos - targetPos;

        // z축 회전(2D)
        float angle = rotateSpeed * dt;
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        float x = offset.x * cos - offset.y * sin;
        float y = offset.x * sin + offset.y * cos;

        pos = targetPos + new Vector3(x, y, offset.z);

        cachedTr.position = pos;

        // 3) 축소 (localScale get/set 최소화)
        Vector3 sc = cachedTr.localScale;
        float s = sc.x - shrinkSpeed * dt;
        if (s <= killScale)
        {
            ReturnToPool();
            return;
        }

        if (s < 0f) s = 0f;
        sc.x = s;
        sc.y = s;
        sc.z = startScale.z; // 원래 z 유지(보통 1)
        cachedTr.localScale = sc;
    }

    private void ReturnToPool()
    {
        target = null;

        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }
}
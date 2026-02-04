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
    private ResourcePickupVFXPool pool;   // 추가

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        startScale = transform.localScale;
    }

    // 풀에서 주입
    public void SetPool(ResourcePickupVFXPool p) => pool = p;

    public void Init(Sprite sprite, Transform targetTr)
    {
        target = targetTr;

        if (sr != null)
            sr.sprite = sprite;

        // 재사용할 때 상태 초기화
        transform.localScale = startScale;
    }

    private void Update()
    {
        if (target == null)
        {
            ReturnToPool();
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            pullSpeed * Time.deltaTime
        );

        transform.RotateAround(
            target.position,
            Vector3.forward,
            rotateSpeed * Time.deltaTime
        );

        float s = Mathf.Max(0f, transform.localScale.x - shrinkSpeed * Time.deltaTime);
        transform.localScale = new Vector3(s, s, 1f);

        if (transform.localScale.x <= killScale)
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        target = null;
        if (pool != null) pool.Release(this);
        else Destroy(gameObject); // 혹시 풀 없을 때 안전장치
    }
}
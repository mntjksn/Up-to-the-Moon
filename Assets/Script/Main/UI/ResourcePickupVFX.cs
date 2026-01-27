using UnityEngine;

public class ResourcePickupVFX : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float pullSpeed = 4f;      // 중심으로 끌리는 속도
    [SerializeField] private float rotateSpeed = 180f; // 회전 속도 (도/초)
    [SerializeField] private float shrinkSpeed = 2f;
    [SerializeField] private float killScale = 0.05f;

    private Transform target;
    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void Init(Sprite sprite, Transform targetTr)
    {
        target = targetTr;
        if (sr != null)
            sr.sprite = sprite;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        //  중심 쪽으로 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            pullSpeed * Time.deltaTime
        );

        //  중심 기준 회전 (곡선 느낌 핵심)
        transform.RotateAround(
            target.position,
            Vector3.forward,
            rotateSpeed * Time.deltaTime
        );

        //  축소
        float s = Mathf.Max(0f, transform.localScale.x - shrinkSpeed * Time.deltaTime);
        transform.localScale = new Vector3(s, s, 1f);

        if (transform.localScale.x <= killScale)
            Destroy(gameObject);
    }
}
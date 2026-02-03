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

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void Init(Sprite sprite, Transform targetTr)
    {
        target = targetTr;

        if (sr != null)
            sr.sprite = sprite;

        if (target == null)
            Destroy(gameObject);
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
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
        transform.localScale = Vector3.one * s;

        if (s <= killScale)
            Destroy(gameObject);
    }
}
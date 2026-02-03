using UnityEngine;

public class SurpriseBoxFloatDown : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float fallSpeed = 1.5f;
    [SerializeField] private float swayAmplitude = 0.4f;
    [SerializeField] private float swayFrequency = 1.2f;

    [Header("Optional")]
    [SerializeField] private float rotateSpeed = 0f;
    [SerializeField] private float destroyPadding = 1.5f;

    private float baseX;
    private float seed;
    private Vector3 pos;

    private void Awake()
    {
        pos = transform.position;
        baseX = pos.x;
        seed = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        // 아래로 이동
        pos.y -= fallSpeed * Time.deltaTime;

        // 좌우 흔들림
        float xOffset = Mathf.Sin((Time.time + seed) * (Mathf.PI * 2f) * swayFrequency) * swayAmplitude;
        pos.x = baseX + xOffset;

        transform.position = pos;

        // 회전(선택)
        if (rotateSpeed != 0f)
            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        DestroyIfOutOfScreen();
    }

    private void DestroyIfOutOfScreen()
    {
        var cam = Camera.main;
        if (cam == null) return;

        float bottomY = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, 0f)).y;

        if (transform.position.y < bottomY - destroyPadding)
            Destroy(gameObject);
    }
}
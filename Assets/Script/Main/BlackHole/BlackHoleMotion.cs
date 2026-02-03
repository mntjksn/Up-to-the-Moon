using UnityEngine;

public class BlackHoleMotion : MonoBehaviour
{
    [Header("Rotate")]
    [SerializeField] private float rotateSpeed = 180f;   // 초당 회전 각도

    [Header("Pulse (Scale)")]
    [SerializeField] private float pulseSpeed = 2.2f;    // 펄스 속도
    [SerializeField] private float pulseAmount = 0.12f;  // 스케일 변화량

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        // 회전
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        // 스케일 펄스
        float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float scale = Mathf.Lerp(1f - pulseAmount, 1f + pulseAmount, t);

        transform.localScale = baseScale * scale;
    }
}
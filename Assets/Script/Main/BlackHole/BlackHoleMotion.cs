using UnityEngine;

public class BlackHoleMotion : MonoBehaviour
{
    [Header("Rotate")]
    [SerializeField] private float rotateSpeed = 180f; // degrees/sec

    [Header("Pulse (Scale)")]
    [SerializeField] private float pulseSpeed = 2.2f;  // cycles/sec ´À³¦
    [SerializeField] private float pulseAmount = 0.12f; // 0.12¸é ¡¾12%

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        // 1) È¸Àü
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        // 2) ÆÞ½º(»çÀÎÆÄ)
        float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0~1
        float scaleMul = Mathf.Lerp(1f - pulseAmount, 1f + pulseAmount, t);
        transform.localScale = baseScale * scaleMul;
    }
}
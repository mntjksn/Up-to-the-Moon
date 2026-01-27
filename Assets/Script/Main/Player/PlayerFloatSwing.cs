using UnityEngine;

public class PlayerFloatSwing : MonoBehaviour
{
    [Header("Float (Up/Down)")]
    [SerializeField] private float floatAmplitude = 0.25f; // 위아래 이동량
    [SerializeField] private float floatSpeed = 1.2f;      // 속도

    [Header("Swing (Pendulum)")]
    [SerializeField] private float swingAngle = 10f;       // 최대 각도(도)
    [SerializeField] private float swingSpeed = 0.9f;      // 흔들 속도

    [Header("Phase")]
    [SerializeField] private bool offsetByRandomPhase = true;

    private Vector3 basePos;
    private float phaseA;
    private float phaseB;

    private void Awake()
    {
        basePos = transform.position;

        if (offsetByRandomPhase)
        {
            phaseA = Random.Range(0f, 100f);
            phaseB = Random.Range(0f, 100f);
        }
    }

    private void OnEnable()
    {
        // 재활성화될 때 기준점 갱신(씬 이동/리셋 대응)
        basePos = transform.position;
    }

    private void Update()
    {
        float t = Time.time;

        // 1) 위아래 둥실 (Position)
        float y = Mathf.Sin((t + phaseA) * floatSpeed * Mathf.PI * 2f) * floatAmplitude;
        transform.position = basePos + new Vector3(0f, y, 0f);

        // 2) 시계추 스윙 (Rotation, 머리 피벗 기준)
        float rotZ = Mathf.Sin((t + phaseB) * swingSpeed * Mathf.PI * 2f) * swingAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
    }
}
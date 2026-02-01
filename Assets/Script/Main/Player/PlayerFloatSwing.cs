using System.Collections;
using UnityEngine;

public class PlayerFloatSwing : MonoBehaviour
{
    [Header("Float (Up/Down)")]
    [SerializeField] private float floatAmplitude = 0.25f;
    [SerializeField] private float floatSpeed = 1.2f;

    [Header("Swing (Pendulum)")]
    [SerializeField] private float swingAngle = 10f;
    [SerializeField] private float swingSpeed = 0.9f;

    [Header("Phase")]
    [SerializeField] private bool offsetByRandomPhase = true;

    private Vector3 basePos;
    private float phaseA;
    private float phaseB;

    // ─────────────────────────────
    // 캐릭터 ID별 값 테이블
    // [Amplitude, FloatSpeed, Angle, SwingSpeed]
    // ─────────────────────────────
    private readonly Vector4[] table =
    {
        new Vector4(0.5f,   0.25f, 15f, 0.1f),   // 0
        new Vector4(1f,     0.3f,  25f, 0.25f),  // 1
        new Vector4(0.75f,  0.2f,  20f, 0.2f),   // 2
        new Vector4(0.5f,   0.15f, 30f, 0.1f),   // 3
        new Vector4(0.5f,   0.05f, 15f, 0.05f),  // 4
        new Vector4(1f,     0.1f,  5f,  0.05f),  // 5
        new Vector4(0.2f,   0.3f,  5f,  0.5f),   // 6
        new Vector4(0.3f,   0.5f,  7.5f,0.75f),  // 7
        new Vector4(0.2f,   0.5f,  3.5f,0.35f),  // 8
        new Vector4(0.05f,  0.25f, 3.5f,0.5f),   // 9
        new Vector4(0.025f, 7.5f,  1f,  1.5f),   // 10
        new Vector4(0.05f,  15f,   2.5f,2f),     // 11
        new Vector4(0.25f,  0.25f, 2.5f,0.05f),  // 12
        new Vector4(0.025f, 25f,   5f,  0.25f),  // 13
        new Vector4(0.01f,  100f,  5f,  0.15f),  // 14
    };

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
        basePos = transform.position;
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        while (SaveManager.Instance == null)
            yield return null;

        SaveManager.Instance.OnCharacterChanged += ApplyByCharacter;

        ApplyByCharacter(SaveManager.Instance.GetCurrentCharacterId());
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnCharacterChanged -= ApplyByCharacter;
    }

    private void ApplyByCharacter(int id)
    {
        if (id < 0 || id >= table.Length)
            return;

        Vector4 v = table[id];

        floatAmplitude = v.x;
        floatSpeed = v.y;
        swingAngle = v.z;
        swingSpeed = v.w;
    }

    private void Update()
    {
        float t = Time.time;

        float y = Mathf.Sin((t + phaseA) * floatSpeed * Mathf.PI * 2f)
                  * floatAmplitude;
        transform.position = basePos + new Vector3(0f, y, 0f);

        float rotZ = Mathf.Sin((t + phaseB) * swingSpeed * Mathf.PI * 2f)
                     * swingAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
    }
}
using System.Collections;
using UnityEngine;

public class PlayerFloatSwing : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] private float floatAmplitude = 0.25f;
    [SerializeField] private float floatSpeed = 1.2f;

    [Header("Swing")]
    [SerializeField] private float swingAngle = 10f;
    [SerializeField] private float swingSpeed = 0.9f;

    [Header("Phase")]
    [SerializeField] private bool offsetByRandomPhase = true;

    [Header("Base Position")]
    [SerializeField] private bool recacheBasePosOnEnable = true; // 원하면 기존처럼, 아니면 스냅 방지

    private Vector3 basePos;
    private float phaseA;
    private float phaseB;

    private Coroutine bindCo;
    private Transform tr;

    private const float TWO_PI = Mathf.PI * 2f;

    private readonly Vector4[] table =
    {
        new Vector4(0.5f,   0.25f, 15f, 0.1f),
        new Vector4(1f,     0.3f,  25f, 0.25f),
        new Vector4(0.75f,  0.2f,  20f, 0.2f),
        new Vector4(0.5f,   0.15f, 30f, 0.1f),
        new Vector4(0.5f,   0.05f, 15f, 0.05f),
        new Vector4(1f,     0.1f,  5f,  0.05f),
        new Vector4(0.2f,   0.3f,  5f,  0.5f),
        new Vector4(0.3f,   0.5f,  7.5f,0.75f),
        new Vector4(0.2f,   0.5f,  3.5f,0.35f),
        new Vector4(0.05f,  0.25f, 3.5f,0.5f),
        new Vector4(0.025f, 7.5f,  1f,  1.5f),
        new Vector4(0.05f,  15f,   2.5f,2f),
        new Vector4(0.25f,  0.25f, 2.5f,0.05f),
        new Vector4(0.025f, 25f,   5f,  0.25f),
        new Vector4(0.01f,  100f,  5f,  0.15f),
    };

    private void Awake()
    {
        tr = transform;

        if (offsetByRandomPhase)
        {
            phaseA = Random.Range(0f, 100f);
            phaseB = Random.Range(0f, 100f);
        }

        // recacheBasePosOnEnable=false면 최초 1회만 기준점 잡음
        basePos = tr.position;
    }

    private void OnEnable()
    {
        if (recacheBasePosOnEnable)
            basePos = tr.position;

        if (bindCo != null) StopCoroutine(bindCo);
        bindCo = StartCoroutine(BindRoutine());
    }

    private void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnCharacterChanged -= ApplyByCharacter;
    }

    private IEnumerator BindRoutine()
    {
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        sm.OnCharacterChanged -= ApplyByCharacter;
        sm.OnCharacterChanged += ApplyByCharacter;

        ApplyByCharacter(sm.GetCurrentCharacterId());

        bindCo = null;
    }

    private void ApplyByCharacter(int id)
    {
        if ((uint)id >= (uint)table.Length) return;

        Vector4 v = table[id];

        floatAmplitude = v.x;
        floatSpeed = v.y;
        swingAngle = v.z;
        swingSpeed = v.w;
    }

    private void Update()
    {
        float t = Time.time;

        float y = Mathf.Sin((t + phaseA) * floatSpeed * TWO_PI) * floatAmplitude;
        tr.position = basePos + new Vector3(0f, y, 0f);

        float rotZ = Mathf.Sin((t + phaseB) * swingSpeed * TWO_PI) * swingAngle;
        tr.localRotation = Quaternion.Euler(0f, 0f, rotZ);
    }
}
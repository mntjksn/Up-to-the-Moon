using UnityEngine;

public class SurpriseBoxFloatDown : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float fallSpeed = 1.5f;      // 아래로 내려오는 속도(유닛/초)
    [SerializeField] private float swayAmplitude = 0.4f;  // 좌우 흔들림 폭(유닛)
    [SerializeField] private float swayFrequency = 1.2f;  // 흔들림 속도(Hz)

    [Header("Optional")]
    [SerializeField] private float rotateSpeed = 0f;      // 살짝 회전 주고 싶으면(도/초)
    [SerializeField] private float destroyPadding = 1.5f; // 화면 아래로 더 내려가면 삭제

    private Vector3 startPos;
    private float seed;

    private void Awake()
    {
        startPos = transform.position;
        seed = Random.Range(0f, 1000f); // 상자마다 흔들림 시작점 다르게
    }

    private void Update()
    {
        // 아래로 이동
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // 좌우 둥둥(사인)
        float xOffset = Mathf.Sin((Time.time + seed) * (Mathf.PI * 2f) * swayFrequency) * swayAmplitude;
        transform.position = new Vector3(startPos.x + xOffset, transform.position.y, transform.position.z);

        // (선택) 살짝 회전
        if (rotateSpeed != 0f)
            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        // startPos의 x는 계속 기준이 되도록(현재 y만 내려오니 x 기준은 고정)
        startPos.y = transform.position.y; // 다음 프레임 기준 갱신(자연스러운 흔들림)

        DestroyIfOutOfScreen();
    }

    private void DestroyIfOutOfScreen()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // 화면 맨 아래 월드 좌표
        float bottomY = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, 0f)).y;

        // 오브젝트가 화면 아래로 충분히 내려가면 삭제
        if (transform.position.y < bottomY - destroyPadding)
            Destroy(gameObject);
    }
}
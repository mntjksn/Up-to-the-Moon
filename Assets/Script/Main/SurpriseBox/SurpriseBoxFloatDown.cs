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

    private Transform tr;
    private Camera cam;

    private float baseX;
    private float seed;
    private Vector3 pos;

    // 화면 하단 y 캐시
    private float cachedBottomY;
    private int cachedScreenW;
    private int cachedScreenH;

    // sin용 시간 누적
    private float phase;

    private void Awake()
    {
        tr = transform;

        pos = tr.position;
        baseX = pos.x;

        seed = Random.Range(0f, 1000f);
        phase = seed;

        CacheCameraAndBottom();
    }

    private void OnEnable()
    {
        // 혹시 씬 로드/카메라 교체에 대비
        CacheCameraAndBottom();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 아래로 이동
        pos.y -= fallSpeed * dt;

        // 좌우 흔들림 (Time.time 대신 누적값)
        phase += dt * swayFrequency * (Mathf.PI * 2f);
        float xOffset = Mathf.Sin(phase) * swayAmplitude;
        pos.x = baseX + xOffset;

        tr.position = pos;

        // 회전(선택)
        if (rotateSpeed != 0f)
            tr.Rotate(0f, 0f, rotateSpeed * dt);

        DestroyIfOutOfScreen();
    }

    private void CacheCameraAndBottom()
    {
        cam = Camera.main;
        cachedScreenW = Screen.width;
        cachedScreenH = Screen.height;

        if (cam != null)
            cachedBottomY = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, 0f)).y;
    }

    private void DestroyIfOutOfScreen()
    {
        // 해상도 변경/회전 대응 (필요할 때만 재계산)
        if (cam == null || cachedScreenW != Screen.width || cachedScreenH != Screen.height)
            CacheCameraAndBottom();

        if (cam == null) return;

        if (pos.y < cachedBottomY - destroyPadding)
            Destroy(gameObject);
    }
}
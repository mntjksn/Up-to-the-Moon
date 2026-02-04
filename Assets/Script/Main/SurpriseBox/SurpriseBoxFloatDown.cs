using UnityEngine;

/*
    SurpriseBoxFloatDown

    [역할]
    - 서프라이즈 박스(또는 오브젝트)가 화면 위에서 아래로 "떠내려오듯" 내려오게 만든다.
    - 내려오면서 좌우로 흔들림(sine sway)을 주고, 옵션으로 회전도 적용할 수 있다.
    - 화면 하단 밖으로 충분히 내려가면(destroyPadding) 오브젝트를 파괴한다.

    [설계 의도]
    1) Transform/Camera 캐싱
       - 매 프레임 GetComponent/Camera.main 호출 대신 캐시(tr, cam)로 비용 절감
       - 화면 하단 월드 좌표(cachedBottomY)도 캐싱해 비교만 수행

    2) 흔들림 계산 최적화
       - Time.time 대신 phase 누적값을 사용해 (Update마다 dt로 증가)
         외부 타임스케일/프레임 변동에 덜 민감하게 만든다.
       - seed로 시작 위상을 랜덤하게 만들어 여러 개가 떨어질 때 흔들림이 서로 달라지게 한다.

    3) 해상도 변경 대응
       - Screen.width/height를 캐싱해 두고 변경이 감지될 때만
         ViewportToWorldPoint로 cachedBottomY를 다시 계산한다.
       - 모바일 회전/해상도 변경 시에도 파괴 기준이 어긋나지 않도록 한다.

    [주의/전제]
    - Camera.main을 사용하므로, 메인 카메라에 "MainCamera" 태그가 있어야 한다.
    - pos/baseX는 월드 좌표 기준이며, UI(RectTransform) 오브젝트에는 그대로 적용하면 안 맞을 수 있다.
    - destroyPadding은 화면 하단보다 얼마나 더 내려가야 파괴할지(여유) 값이다.
*/
public class SurpriseBoxFloatDown : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float fallSpeed = 1.5f;      // 아래로 내려가는 속도(월드 유닛/초)
    [SerializeField] private float swayAmplitude = 0.4f;  // 좌우 흔들림 폭(월드 유닛)
    [SerializeField] private float swayFrequency = 1.2f;  // 좌우 흔들림 주파수(Hz 느낌)

    [Header("Optional")]
    [SerializeField] private float rotateSpeed = 0f;      // 회전 속도(도/초). 0이면 회전 없음
    [SerializeField] private float destroyPadding = 1.5f; // 화면 하단 밖으로 더 내려갈 여유 거리

    private Transform tr; // transform 캐시
    private Camera cam;   // 메인 카메라 캐시

    private float baseX;  // 흔들림 기준 x(초기 x)
    private float seed;   // 흔들림 위상 랜덤 시드
    private Vector3 pos;  // 위치 계산용 캐시(트랜스폼 접근 최소화)

    // 화면 하단 y 캐시(월드 좌표)
    private float cachedBottomY;
    private int cachedScreenW;
    private int cachedScreenH;

    // sin용 시간/위상 누적값(Time.time 대신 사용)
    private float phase;

    private void Awake()
    {
        // transform 캐시
        tr = transform;

        // 초기 위치/기준 x 저장
        pos = tr.position;
        baseX = pos.x;

        // 위상 랜덤 시드(여러 오브젝트가 서로 다른 흔들림을 갖게)
        seed = Random.Range(0f, 1000f);
        phase = seed;

        // 카메라/화면 하단 월드 좌표 캐싱
        CacheCameraAndBottom();
    }

    private void OnEnable()
    {
        // 혹시 씬 로드/카메라 교체에 대비해 다시 캐싱
        CacheCameraAndBottom();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 1) 아래로 이동
        pos.y -= fallSpeed * dt;

        // 2) 좌우 흔들림(누적 위상 기반)
        //    phase는 라디안 기준으로 누적되며, swayFrequency로 속도를 조절한다.
        phase += dt * swayFrequency * (Mathf.PI * 2f);
        float xOffset = Mathf.Sin(phase) * swayAmplitude;
        pos.x = baseX + xOffset;

        // 3) 실제 위치 반영
        tr.position = pos;

        // 4) 회전(선택)
        if (rotateSpeed != 0f)
            tr.Rotate(0f, 0f, rotateSpeed * dt);

        // 5) 화면 밖이면 파괴
        DestroyIfOutOfScreen();
    }

    /*
        카메라/화면 하단 월드좌표 캐싱
        - Screen.width/height를 저장해 해상도 변경 여부를 감지할 수 있게 한다.
        - ViewportToWorldPoint(y=0)로 화면 하단의 월드 y좌표를 구한다.
    */
    private void CacheCameraAndBottom()
    {
        cam = Camera.main;
        cachedScreenW = Screen.width;
        cachedScreenH = Screen.height;

        if (cam != null)
            cachedBottomY = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, 0f)).y;
    }

    /*
        화면 밖 파괴 처리
        - 카메라가 없거나, 해상도가 바뀌었으면 캐시 재계산
        - pos.y가 화면 하단(cachedBottomY) - padding보다 작아지면 파괴
    */
    private void DestroyIfOutOfScreen()
    {
        // 해상도 변경/회전 대응(필요할 때만 재계산)
        if (cam == null || cachedScreenW != Screen.width || cachedScreenH != Screen.height)
            CacheCameraAndBottom();

        if (cam == null) return;

        // 화면 하단 아래로 충분히 내려가면 제거
        if (pos.y < cachedBottomY - destroyPadding)
            Destroy(gameObject);
    }
}
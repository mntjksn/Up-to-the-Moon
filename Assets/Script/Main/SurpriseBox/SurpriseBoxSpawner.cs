using System.Collections;
using UnityEngine;

/*
    SurpriseBoxSpawner

    [역할]
    - 일정 시간이 지난 뒤(startDelay)부터,
      일정 랜덤 간격(minSpawnTime~maxSpawnTime)으로 서프라이즈 박스를 생성한다.
    - 생성 위치는 “화면 상단” 기준으로 잡으며,
      x는 화면 좌상단~우상단 사이에서 padding을 적용해 랜덤,
      y는 화면 상단보다 약간 위(yOffset)에서 생성한다.

    [설계 의도]
    1) 카메라/화면 코너 좌표 캐싱
       - Camera.main 호출을 매번 하지 않도록 cam을 캐시한다.
       - ViewportToWorldPoint로 얻는 상단 코너(leftTop/rightTop) 좌표를 캐시해 재사용한다.
       - Screen.width/height 변경(해상도/회전) 감지 시에만 코너 좌표를 다시 계산한다.

    2) 스폰 타이밍: 코루틴 기반
       - StartCoroutine(SpawnRoutine)로 반복 스폰
       - 시작 딜레이는 WaitForSeconds(startWait)로 1회 대기 후 진입

    3) 모바일 성능 관점 메모
       - while 루프에서 WaitForSeconds(nextTime)를 매번 new로 만들면 GC 원인이 될 수 있다.
         (현재 코드에 주석으로도 적혀 있음)
       - 다만 nextTime이 랜덤이라 캐싱하기가 애매하며,
         GC가 문제라면 커스텀 타이머(Time.time 기반) 방식으로 바꾸는 선택지가 있다
         (여기서는 “코드 수정”이 아닌 주석 설명만 남김)

    [주의/전제]
    - surpriseBoxPrefab이 null이면 스폰하지 않는다.
    - Camera.main을 쓰므로 메인 카메라에 "MainCamera" 태그가 있어야 한다.
    - 이 스크립트는 world space 기준 Instantiate이며,
      UI(RectTransform) 요소를 뿌리는 방식과는 다르다.
*/
public class SurpriseBoxSpawner : MonoBehaviour
{
    [SerializeField] private GameObject surpriseBoxPrefab; // 생성할 서프라이즈 박스 프리팹

    [Header("Start Delay")]
    [SerializeField] private float startDelay = 60f; // 시작 후 첫 스폰까지 대기 시간

    [Header("Random Spawn Time")]
    [SerializeField] private float minSpawnTime = 15f; // 다음 스폰까지 최소 대기
    [SerializeField] private float maxSpawnTime = 40f; // 다음 스폰까지 최대 대기

    [Header("Spawn Range")]
    [SerializeField] private float xPadding = 0.5f; // 화면 좌/우 끝에서 떨어질 여유
    [SerializeField] private float yOffset = 1.0f;  // 화면 상단보다 얼마나 위에서 생성할지

    private Camera cam; // 메인 카메라 캐시

    // 캐시(화면 상단 코너 월드좌표)
    private Vector3 leftTop;   // 화면 좌상단 월드 좌표
    private Vector3 rightTop;  // 화면 우상단 월드 좌표
    private int cachedW;       // 해상도 변경 감지용(가로)
    private int cachedH;       // 해상도 변경 감지용(세로)

    private WaitForSeconds startWait; // 시작 딜레이 캐시(1회 대기용)

    private void Awake()
    {
        // 카메라/상단 코너 좌표 캐싱
        CacheCamera();
        CacheTopCorners();

        // 시작 딜레이 캐싱
        startWait = new WaitForSeconds(startDelay);
    }

    private void OnEnable()
    {
        // 혹시 씬 전환/카메라 교체 대비(재캐싱)
        CacheCamera();
        CacheTopCorners();
    }

    private void Start()
    {
        // 프리팹이 없으면 동작 불가
        if (surpriseBoxPrefab == null)
        {
            Debug.LogWarning("[SurpriseBoxSpawner] surpriseBoxPrefab is null");
            return;
        }

        // 반복 스폰 코루틴 시작
        StartCoroutine(SpawnRoutine());
    }

    /*
        스폰 루틴
        1) startDelay만큼 대기
        2) 무한 루프:
           - SpawnBox()로 1개 생성
           - 랜덤 시간(minSpawnTime~maxSpawnTime) 대기 후 반복
    */
    private IEnumerator SpawnRoutine()
    {
        // startDelay가 런타임에 바뀔 수 있으면 여기서 다시 생성
        if (startWait == null) startWait = new WaitForSeconds(startDelay);
        yield return startWait;

        while (true)
        {
            SpawnBox();

            float nextTime = Random.Range(minSpawnTime, maxSpawnTime);

            // 랜덤이라 캐싱 의미 없음(그래도 GC는 이 줄이 원인)
            // - WaitForSeconds는 생성 시 GC 후보가 될 수 있음
            // - 필요 시 Time 기반 타이머(누적/절대시간)로 전환 가능(여기서는 수정하지 않음)
            yield return new WaitForSeconds(nextTime);
        }
    }

    /*
        메인 카메라 캐싱
        - cam이 비어 있을 때만 Camera.main으로 잡는다.
    */
    private void CacheCamera()
    {
        if (cam == null) cam = Camera.main;
    }

    /*
        화면 상단 코너 좌표 캐싱
        - 현재 Screen.width/height를 저장해 해상도/회전 변경을 감지할 수 있게 한다.
        - ViewportToWorldPoint로 좌상단(0,1), 우상단(1,1) 월드 좌표를 계산한다.
    */
    private void CacheTopCorners()
    {
        cachedW = Screen.width;
        cachedH = Screen.height;

        if (cam == null) return;

        leftTop = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
        rightTop = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
    }

    /*
        박스 1개 생성
        - 해상도/회전 변경 시에만 상단 코너 좌표를 재계산한다.
        - x는 좌~우 상단 사이에서 padding 적용 후 랜덤
        - y는 상단보다 yOffset만큼 위에서 생성
    */
    private void SpawnBox()
    {
        if (cam == null) CacheCamera();
        if (cam == null) return;

        // 해상도/회전 변경 시에만 재계산
        if (cachedW != Screen.width || cachedH != Screen.height)
            CacheTopCorners();

        float x = Random.Range(leftTop.x + xPadding, rightTop.x - xPadding);
        float y = leftTop.y + yOffset;

        Instantiate(surpriseBoxPrefab, new Vector3(x, y, 0f), Quaternion.identity);
    }
}
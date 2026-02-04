using System.Collections;
using UnityEngine;

public class SurpriseBoxSpawner : MonoBehaviour
{
    [SerializeField] private GameObject surpriseBoxPrefab;

    [Header("Start Delay")]
    [SerializeField] private float startDelay = 60f;

    [Header("Random Spawn Time")]
    [SerializeField] private float minSpawnTime = 15f;
    [SerializeField] private float maxSpawnTime = 40f;

    [Header("Spawn Range")]
    [SerializeField] private float xPadding = 0.5f;
    [SerializeField] private float yOffset = 1.0f;

    private Camera cam;

    // 캐시
    private Vector3 leftTop;
    private Vector3 rightTop;
    private int cachedW;
    private int cachedH;

    private WaitForSeconds startWait;

    private void Awake()
    {
        CacheCamera();
        CacheTopCorners();

        startWait = new WaitForSeconds(startDelay);
    }

    private void OnEnable()
    {
        // 혹시 씬 전환/카메라 교체 대비
        CacheCamera();
        CacheTopCorners();
    }

    private void Start()
    {
        if (surpriseBoxPrefab == null)
        {
            Debug.LogWarning("[SurpriseBoxSpawner] surpriseBoxPrefab is null");
            return;
        }

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // startDelay가 런타임에 바뀔 수 있으면 여기서 다시 생성
        if (startWait == null) startWait = new WaitForSeconds(startDelay);
        yield return startWait;

        while (true)
        {
            SpawnBox();

            float nextTime = Random.Range(minSpawnTime, maxSpawnTime);
            yield return new WaitForSeconds(nextTime); // 랜덤이라 캐싱 의미 없음(그래도 GC는 이 줄이 원인)
        }
    }

    private void CacheCamera()
    {
        if (cam == null) cam = Camera.main;
    }

    private void CacheTopCorners()
    {
        cachedW = Screen.width;
        cachedH = Screen.height;

        if (cam == null) return;

        leftTop = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
        rightTop = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
    }

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
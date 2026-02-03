using System.Collections;
using UnityEngine;

public class SurpriseBoxSpawner : MonoBehaviour
{
    [SerializeField] private GameObject surpriseBoxPrefab;

    [Header("Start Delay")]
    [SerializeField] private float startDelay = 60f;   // 게임 시작 후 60초

    [Header("Random Spawn Time")]
    [SerializeField] private float minSpawnTime = 15f; // 최소 15초
    [SerializeField] private float maxSpawnTime = 40f; // 최대 40초

    [Header("Spawn Range")]
    [SerializeField] private float xPadding = 0.5f;
    [SerializeField] private float yOffset = 1.0f;

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // 1 시작 딜레이
        yield return new WaitForSeconds(startDelay);

        // 2 무한 반복
        while (true)
        {
            SpawnBox();

            float nextTime = Random.Range(minSpawnTime, maxSpawnTime);
            yield return new WaitForSeconds(nextTime);
        }
    }

    private void SpawnBox()
    {
        if (surpriseBoxPrefab == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 leftTop = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
        Vector3 rightTop = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        float x = Random.Range(leftTop.x + xPadding, rightTop.x - xPadding);
        float y = leftTop.y + yOffset;

        Instantiate(surpriseBoxPrefab, new Vector3(x, y, 0f), Quaternion.identity);
    }
}
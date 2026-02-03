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

    private void Awake()
    {
        cam = Camera.main;
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
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SpawnBox();

            float nextTime = Random.Range(minSpawnTime, maxSpawnTime);
            yield return new WaitForSeconds(nextTime);
        }
    }

    private void SpawnBox()
    {
        if (cam == null) return;

        Vector3 leftTop = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
        Vector3 rightTop = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        float x = Random.Range(leftTop.x + xPadding, rightTop.x - xPadding);
        float y = leftTop.y + yOffset;

        Instantiate(
            surpriseBoxPrefab,
            new Vector3(x, y, 0f),
            Quaternion.identity
        );
    }
}
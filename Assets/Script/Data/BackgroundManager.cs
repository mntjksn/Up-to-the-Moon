using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class BackgroundItem
{
    public string name;
    public string sub;

    public int item_num;
    public long zoneMinKm;

    // Resources.Load 경로(확장자 제외)
    public string spritePath;

    // 런타임 전용(저장 안 됨)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class BackgroundItemListWrapper
{
    // JSON 최상위 키가 "BackgroundItem" 이므로 필드명도 동일해야 한다
    public List<BackgroundItem> BackgroundItem;
}

public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance;

    // JSON 키와 동일하게 유지
    public List<BackgroundItem> BackgroundItem = new List<BackgroundItem>();

    // 로드 완료 여부
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "BackgroundItemData.json";

    [Header("Optimization")]
    [SerializeField] private int spriteLoadsPerFrame = 4; // 프레임당 스프라이트 로드 개수(0이면 전부 한 프레임)
    [SerializeField] private bool yieldDuringFileIO = false; // true면 파일 IO 전후 한 프레임씩 양보(프리즈 완화용)

    private Coroutine loadCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 중복 로드 방지
        if (loadCo == null)
            loadCo = StartCoroutine(LoadBackgroundItemRoutine());
    }

    // km 기준으로 현재 적용될 배경 1개를 반환
    // 전제: zoneMinKm 오름차순 정렬
    // (최적화) 이진탐색 O(log n)
    public BackgroundItem GetByKm(float km)
    {
        if (BackgroundItem == null || BackgroundItem.Count == 0)
            return null;

        long kmL = (long)km;

        int lo = 0;
        int hi = BackgroundItem.Count - 1;

        // kmL 이하인 값 중 가장 큰 zoneMinKm의 인덱스 찾기
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long midVal = BackgroundItem[mid].zoneMinKm;

            if (midVal <= kmL)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        int idx = Mathf.Clamp(hi, 0, BackgroundItem.Count - 1);
        return BackgroundItem[idx];
    }

    private IEnumerator LoadBackgroundItemRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        if (yieldDuringFileIO) yield return null;

        string json = null;
        try
        {
            json = File.ReadAllText(targetPath);
        }
        catch
        {
            json = null;
        }

        if (yieldDuringFileIO) yield return null;

        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        LoadFromJson(json);
        SortByZoneMinKm();

        // (최적화) Resources.Load를 분산 처리
        yield return LoadSpritesSpread();

        IsLoaded = true;
        loadCo = null;
    }

    private IEnumerator CopyFromStreamingAssetsIfNeeded(string targetPath)
    {
        string streamingPath = Path.Combine(Application.streamingAssetsPath, JSON_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
        UnityWebRequest req = UnityWebRequest.Get(streamingPath);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                File.WriteAllText(targetPath, req.downloadHandler.text);
            }
            catch { }
        }
#else
        if (File.Exists(streamingPath))
        {
            try
            {
                File.Copy(streamingPath, targetPath, true);
            }
            catch { }
        }

        yield break;
#endif
    }

    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        BackgroundItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱
                string wrapped = "{\"BackgroundItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<BackgroundItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱
                wrapper = JsonUtility.FromJson<BackgroundItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        BackgroundItem = (wrapper != null && wrapper.BackgroundItem != null)
            ? wrapper.BackgroundItem
            : new List<BackgroundItem>();
    }

    private void SortByZoneMinKm()
    {
        if (BackgroundItem == null) return;
        BackgroundItem.Sort((a, b) => a.zoneMinKm.CompareTo(b.zoneMinKm));
    }

    private IEnumerator LoadSpritesSpread()
    {
        if (BackgroundItem == null) yield break;

        int loadedThisFrame = 0;

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            var item = BackgroundItem[i];
            if (item == null) continue;
            if (item.itemimg != null) continue; // 이미 로드됨
            if (string.IsNullOrEmpty(item.spritePath)) continue;

            item.itemimg = Resources.Load<Sprite>(item.spritePath);

            if (spriteLoadsPerFrame > 0)
            {
                loadedThisFrame++;
                if (loadedThisFrame >= spriteLoadsPerFrame)
                {
                    loadedThisFrame = 0;
                    yield return null; // 프레임 양보
                }
            }
        }
    }

    private void SetEmptyAndFinish()
    {
        BackgroundItem = new List<BackgroundItem>();
        IsLoaded = true;
    }
}
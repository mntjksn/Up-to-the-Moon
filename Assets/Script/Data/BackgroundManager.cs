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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(LoadBackgroundItemRoutine());
    }

    // km 기준으로 현재 적용될 배경 1개를 반환
    // 전제: zoneMinKm 오름차순 정렬
    public BackgroundItem GetByKm(float km)
    {
        if (BackgroundItem == null || BackgroundItem.Count == 0)
            return null;

        BackgroundItem current = BackgroundItem[0];

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            if (km >= BackgroundItem[i].zoneMinKm)
                current = BackgroundItem[i];
            else
                break;
        }

        return current;
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
            yield break;
        }

        string json = File.ReadAllText(targetPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            yield break;
        }

        LoadFromJson(json);
        SortByZoneMinKm();
        LoadSpritesOnce();

        IsLoaded = true;
    }

    private IEnumerator CopyFromStreamingAssetsIfNeeded(string targetPath)
    {
        string streamingPath = Path.Combine(Application.streamingAssetsPath, JSON_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
        UnityWebRequest req = UnityWebRequest.Get(streamingPath);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            File.WriteAllText(targetPath, req.downloadHandler.text);
#else
        if (File.Exists(streamingPath))
            File.Copy(streamingPath, targetPath, true);

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

    private void LoadSpritesOnce()
    {
        if (BackgroundItem == null) return;

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            BackgroundItem item = BackgroundItem[i];

            if (item == null) continue;
            if (string.IsNullOrEmpty(item.spritePath)) continue;

            item.itemimg = Resources.Load<Sprite>(item.spritePath);
        }
    }

    private void SetEmptyAndFinish()
    {
        BackgroundItem = new List<BackgroundItem>();
        IsLoaded = true;
    }
}
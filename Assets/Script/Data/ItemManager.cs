using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class SupplyItem
{
    public string name;
    public string sub;

    public int item_num;
    public int item_price;
    public int zoneMinKm;

    // Resources.Load 경로(확장자 제외)
    public string spritePath;

    // 런타임 전용(저장 안 됨)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class SupplyItemListWrapper
{
    // JSON 최상위 키가 "SupplyItem" 이므로 필드명도 동일해야 한다
    public List<SupplyItem> SupplyItem;
}

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance;

    // JSON 키와 동일하게 유지
    public List<SupplyItem> SupplyItem = new List<SupplyItem>();

    // 로드 완료 여부
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "SupplyItemData.json";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(LoadSupplyItemRoutine());
    }

    // km 기준으로 해금된 아이템 목록 반환
    public List<SupplyItem> GetUnlockedByKm(float km)
    {
        var list = new List<SupplyItem>();

        if (SupplyItem == null || SupplyItem.Count == 0)
            return list;

        for (int i = 0; i < SupplyItem.Count; i++)
        {
            if (km >= SupplyItem[i].zoneMinKm)
                list.Add(SupplyItem[i]);
        }

        return list;
    }

    private IEnumerator LoadSupplyItemRoutine()
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

        SupplyItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱
                string wrapped = "{\"SupplyItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        SupplyItem = (wrapper != null && wrapper.SupplyItem != null)
            ? wrapper.SupplyItem
            : new List<SupplyItem>();
    }

    private void LoadSpritesOnce()
    {
        if (SupplyItem == null) return;

        for (int i = 0; i < SupplyItem.Count; i++)
        {
            SupplyItem item = SupplyItem[i];

            if (item == null) continue;
            if (string.IsNullOrEmpty(item.spritePath)) continue;

            item.itemimg = Resources.Load<Sprite>(item.spritePath);
        }
    }

    private void SetEmptyAndFinish()
    {
        SupplyItem = new List<SupplyItem>();
        IsLoaded = true;
    }

    // 리스트 인덱스로 가져오기
    public SupplyItem GetItem(int index)
    {
        if (SupplyItem == null)
        {
            Debug.LogError("[ItemManager] 아이템 리스트가 비어 있습니다.");
            return null;
        }

        if (index < 0 || index >= SupplyItem.Count)
        {
            Debug.LogError("[ItemManager] 잘못된 인덱스 요청: " + index);
            return null;
        }

        return SupplyItem[index];
    }
}
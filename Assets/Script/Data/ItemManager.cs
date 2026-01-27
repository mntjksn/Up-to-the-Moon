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

    // 런타임 전용(저장 안됨)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class SupplyItemListWrapper
{
    // SupplyItem.json의 최상위 키가 "SupplyItem" 이므로 필드명도 동일해야 한다
    public List<SupplyItem> SupplyItem;
}


public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance;

    // 아이템 목록
    public List<SupplyItem> SupplyItem = new List<SupplyItem>();

    public List<SupplyItem> GetUnlockedByKm(float km)
    {
        var list = new List<SupplyItem>();
        if (SupplyItem == null) return list;

        for (int i = 0; i < SupplyItem.Count; i++)
        {
            if (km >= SupplyItem[i].zoneMinKm)
                list.Add(SupplyItem[i]);
        }
        return list;
    }

    // 로드 완료 여부(다른 스크립트에서 접근 타이밍 방지용)
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "SupplyItemData.json";

    private void Awake()
    {
        // 싱글톤 유지
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(LoadSupplyItemRoutine());
    }

    private IEnumerator LoadSupplyItemRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        // JSON이 없으면 StreamingAssets에서 복사
        if (!File.Exists(targetPath))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, JSON_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
            UnityWebRequest req = UnityWebRequest.Get(streamingPath);
            yield return req.SendWebRequest();

            if (!req.isNetworkError && !req.isHttpError)
                File.WriteAllText(targetPath, req.downloadHandler.text);
#else
            if (File.Exists(streamingPath))
                File.Copy(streamingPath, targetPath, true);
#endif
        }

        // 파일이 여전히 없으면 빈 리스트
        if (!File.Exists(targetPath))
        {
            SupplyItem = new List<SupplyItem>();
            IsLoaded = true;
            yield break;
        }

        // JSON 읽기
        string json = File.ReadAllText(targetPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            SupplyItem = new List<SupplyItem>();
            IsLoaded = true;
            yield break;
        }

        json = json.TrimStart();

        // 배열 JSON / wrapper JSON 모두 대응
        SupplyItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열만 있을 경우 "SupplyItem"로 감싸서 파싱
                string wrapped = "{\"SupplyItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(wrapped);
            }
            else
            {
                // {"SupplyItem":[...]} 형태면 그대로 파싱
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

        // 리소스 로드(1회)
        for (int i = 0; i < SupplyItem.Count; i++)
        {
            SupplyItem item = SupplyItem[i];

            if (!string.IsNullOrEmpty(item.spritePath))
                item.itemimg = Resources.Load<Sprite>(item.spritePath);
        }

        IsLoaded = true;
        yield break;
    }

    // 리스트 인덱스로 가져오기
    public SupplyItem GetItem(int index)
    {
        if (index < 0 || index >= SupplyItem.Count)
        {
            Debug.LogError("[ItemManager] 잘못된 인덱스 요청: " + index);
            return null;
        }

        return SupplyItem[index];
    }
}

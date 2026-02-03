using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class MissionItem
{
    public string id;
    public string title;
    public string desc;

    public string tier;      // easy, normal, hard
    public string category;  // growth, region, resource, upgrade, play

    public string goalType;  // accumulate, reach_value, count, unlock, multi_reach 등
    public string goalKey;
    public double goalTarget;

    public long rewardGold;

    public double currentValue;
    public bool isCompleted;
    public bool rewardClaimed;
}

[System.Serializable]
public class MissionItemListWrapper
{
    // JSON 최상위 키가 "missions" 라고 했으니 반드시 동일해야 함!
    public List<MissionItem> missions;
}

public class MissionDataManager : MonoBehaviour
{
    public static MissionDataManager Instance;

    public List<MissionItem> MissionItem = new List<MissionItem>();
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "MissionItemData.json";

    private Coroutine loadCo; // 추가

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        loadCo = StartCoroutine(LoadMissionRoutine()); // 변경
    }

    // 추가: 삭제 후 즉시 초기화 다시 로드
    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;

        if (loadCo != null) StopCoroutine(loadCo);
        loadCo = StartCoroutine(LoadMissionRoutine());
    }

    private IEnumerator LoadMissionRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        // 없으면 StreamingAssets에서 복사
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

        if (!File.Exists(targetPath))
        {
            MissionItem = new List<MissionItem>();
            IsLoaded = true;
            yield break;
        }

        string json = File.ReadAllText(targetPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            MissionItem = new List<MissionItem>();
            IsLoaded = true;
            yield break;
        }

        json = json.TrimStart();

        MissionItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                string wrapped = "{\"missions\":" + json + "}";
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(wrapped);
            }
            else
            {
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        MissionItem = (wrapper != null && wrapper.missions != null)
            ? wrapper.missions
            : new List<MissionItem>();

        IsLoaded = true;
        yield break; // 습관적으로 넣어도 좋음
    }

    public void SaveToJson()
    {
        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        var wrapper = new MissionItemListWrapper { missions = this.MissionItem };
        string json = JsonUtility.ToJson(wrapper, true);

        File.WriteAllText(targetPath, json);
    }
}
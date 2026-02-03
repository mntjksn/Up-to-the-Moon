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
    // JSON 최상위 키가 "missions" 이므로 필드명도 동일해야 한다
    public List<MissionItem> missions;
}

public class MissionDataManager : MonoBehaviour
{
    public static MissionDataManager Instance;

    // 런타임에서 사용하는 미션 리스트
    public List<MissionItem> MissionItem = new List<MissionItem>();

    // 로드 완료 여부
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "MissionItemData.json";

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

        StartLoad();
    }

    // 외부에서 즉시 재로딩 호출용
    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;
        StartLoad();
    }

    private void StartLoad()
    {
        if (loadCo != null)
            StopCoroutine(loadCo);

        loadCo = StartCoroutine(LoadMissionRoutine());
    }

    private IEnumerator LoadMissionRoutine()
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

        MissionItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱
                string wrapped = "{\"missions\":" + json + "}";
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱
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
    }

    private void SetEmptyAndFinish()
    {
        MissionItem = new List<MissionItem>();
        IsLoaded = true;
    }

    // 현재 미션 상태를 JSON으로 저장
    public void SaveToJson()
    {
        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        try
        {
            var wrapper = new MissionItemListWrapper
            {
                missions = this.MissionItem
            };

            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(targetPath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[MissionDataManager] SaveToJson 실패: " + e.Message);
        }
    }
}
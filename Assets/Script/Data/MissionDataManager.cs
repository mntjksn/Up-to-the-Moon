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

    [Header("Optimization - Save")]
    [SerializeField] private float saveDebounceSec = 0.5f; // 연속 저장 합치기
    [SerializeField] private bool prettyPrint = false;     // 기본 false 권장(문자열/용량/GC 감소)

    private Coroutine saveCo;
    private bool saveDirty;

    private string TargetPath => Path.Combine(Application.persistentDataPath, JSON_NAME);

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

        string targetPath = TargetPath;

        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        // 파일 IO 프리즈 완화용: 한 프레임 양보
        yield return null;

        string json = null;
        try { json = File.ReadAllText(targetPath); }
        catch { json = null; }

        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        LoadFromJson(json);

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
            try { File.WriteAllText(targetPath, req.downloadHandler.text); }
            catch { }
        }
#else
        if (File.Exists(streamingPath))
        {
            try { File.Copy(streamingPath, targetPath, true); }
            catch { }
        }

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

    // -----------------------
    // 저장 최적화
    // -----------------------

    // 기존처럼 외부에서 SaveToJson() 호출하면 됨.
    // 이제는 "바로 저장"이 아니라, debounce로 합쳐서 저장.
    public void SaveToJson()
    {
        saveDirty = true;

        if (!gameObject.activeInHierarchy) return;

        if (saveCo == null)
            saveCo = StartCoroutine(SaveRoutine());
    }

    private IEnumerator SaveRoutine()
    {
        // debounce: saveDebounceSec 동안 호출이 더 오면 합쳐짐
        float t = 0f;
        while (t < saveDebounceSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!saveDirty)
        {
            saveCo = null;
            yield break;
        }

        saveDirty = false;

        // JSON 생성/파일쓰기 전에 한 프레임 양보(게임 플레이 순간 멈춤 완화)
        yield return null;

        string targetPath = TargetPath;

        try
        {
            var wrapper = new MissionItemListWrapper { missions = this.MissionItem };
            string json = JsonUtility.ToJson(wrapper, prettyPrint);
            File.WriteAllText(targetPath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MissionDataManager] SaveToJson 실패: " + e.Message);
        }

        saveCo = null;

        // 저장 중 또 dirty 됐으면 다시 예약
        if (saveDirty)
            saveCo = StartCoroutine(SaveRoutine());
    }

    // (선택) 앱 종료/백그라운드 시 즉시 저장하고 싶으면 여기서 강제 저장 가능
    private void OnApplicationPause(bool pause)
    {
        if (!pause) return;
        if (!saveDirty) return;

        // 코루틴 저장 대기 중이면 여기서 한 번 강제 저장
        ForceSaveNow();
    }

    private void OnApplicationQuit()
    {
        if (saveDirty)
            ForceSaveNow();
    }

    private void ForceSaveNow()
    {
        saveDirty = false;

        try
        {
            var wrapper = new MissionItemListWrapper { missions = this.MissionItem };
            string json = JsonUtility.ToJson(wrapper, prettyPrint);
            File.WriteAllText(TargetPath, json);
        }
        catch { }
    }
}
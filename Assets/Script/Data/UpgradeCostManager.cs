using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class Cost
{
    public int itemId;
    public int count;
}

[System.Serializable]
public class UpgradeStep
{
    public int step;          // 업그레이드 단계(레벨)
    public List<Cost> costs;  // 필요한 재료 여러 개
}

[System.Serializable]
public class UpgradeStepWrapper
{
    // JSON 최상위 키가 "UpgradeStep" 이므로 필드명도 동일해야 한다
    public List<UpgradeStep> UpgradeStep;
}

public class UpgradeCostManager : MonoBehaviour
{
    public static UpgradeCostManager Instance;

    // JSON 키와 동일하게 유지
    public List<UpgradeStep> UpgradeStep = new List<UpgradeStep>();

    // 로드 완료 여부
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "UpgradeCostData.json";

    // step -> UpgradeStep 빠른 조회용
    private readonly Dictionary<int, UpgradeStep> stepMap = new Dictionary<int, UpgradeStep>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(LoadUpgradeCostRoutine());
    }

    private IEnumerator LoadUpgradeCostRoutine()
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
        BuildMap();

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
        else
            Debug.LogError("[UpgradeCostManager] StreamingAssets JSON 로드 실패: " + req.error);
#else
        if (File.Exists(streamingPath))
            File.Copy(streamingPath, targetPath, true);
        else
            Debug.LogWarning("[UpgradeCostManager] StreamingAssets에 JSON이 없습니다: " + streamingPath);

        yield break;
#endif
    }

    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        UpgradeStepWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱
                string wrapped = "{\"UpgradeStep\":" + json + "}";
                wrapper = JsonUtility.FromJson<UpgradeStepWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱
                wrapper = JsonUtility.FromJson<UpgradeStepWrapper>(json);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[UpgradeCostManager] JSON 파싱 실패: " + e.Message);
            wrapper = null;
        }

        UpgradeStep = (wrapper != null && wrapper.UpgradeStep != null)
            ? wrapper.UpgradeStep
            : new List<UpgradeStep>();
    }

    private void SetEmptyAndFinish()
    {
        UpgradeStep = new List<UpgradeStep>();
        BuildMap();
        IsLoaded = true;
    }

    private void BuildMap()
    {
        stepMap.Clear();

        if (UpgradeStep == null) return;

        for (int i = 0; i < UpgradeStep.Count; i++)
        {
            UpgradeStep s = UpgradeStep[i];
            if (s == null) continue;

            if (stepMap.ContainsKey(s.step))
            {
                Debug.LogWarning("[UpgradeCostManager] step 중복 발견: " + s.step + " (뒤 항목 무시)");
                continue;
            }

            stepMap.Add(s.step, s);
        }
    }

    // step으로 비용 리스트 가져오기
    public List<Cost> GetCostsByStep(int step)
    {
        if (!IsLoaded) return new List<Cost>();

        UpgradeStep s;
        if (stepMap.TryGetValue(step, out s) && s != null && s.costs != null)
            return s.costs;

        return new List<Cost>();
    }
}
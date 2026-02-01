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
    public List<UpgradeStep> UpgradeStep;
}

public class UpgradeCostManager : MonoBehaviour
{
    public static UpgradeCostManager Instance;

    // 아이템 목록
    public List<UpgradeStep> UpgradeStep = new List<UpgradeStep>();

    // 로드 완료 여부(다른 스크립트에서 접근 타이밍 방지용)
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "UpgradeCostData.json";

    private readonly Dictionary<int, UpgradeStep> stepMap = new Dictionary<int, UpgradeStep>();

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

        StartCoroutine(LoadUpgradeCostRoutine());
    }

    private IEnumerator LoadUpgradeCostRoutine()
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
            else
                Debug.LogError("[UpgradeCostManager] StreamingAssets JSON 로드 실패: " + req.error);
#else
            if (File.Exists(streamingPath))
                File.Copy(streamingPath, targetPath, true);
            else
                Debug.LogWarning("[UpgradeCostManager] StreamingAssets에 JSON이 없습니다: " + streamingPath);
#endif
        }

        // 파일이 여전히 없으면 빈 리스트
        if (!File.Exists(targetPath))
        {
            UpgradeStep = new List<UpgradeStep>();
            BuildMap();
            IsLoaded = true;
            yield break;
        }

        // JSON 읽기
        string json = File.ReadAllText(targetPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            UpgradeStep = new List<UpgradeStep>();
            BuildMap();
            IsLoaded = true;
            yield break;
        }

        json = json.TrimStart();

        UpgradeStepWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열만 있을 경우 "UpgradeStep"로 감싸서 파싱
                string wrapped = "{\"UpgradeStep\":" + json + "}";
                wrapper = JsonUtility.FromJson<UpgradeStepWrapper>(wrapped);
            }
            else
            {
                // {"UpgradeStep":[...]} 형태면 그대로 파싱
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

        BuildMap();

        IsLoaded = true;
        yield break;
    }

    private void BuildMap()
    {
        stepMap.Clear();

        for (int i = 0; i < UpgradeStep.Count; i++)
        {
            var s = UpgradeStep[i];
            if (s == null) continue;

            if (stepMap.ContainsKey(s.step))
            {
                Debug.LogWarning($"[UpgradeCostManager] step 중복 발견: {s.step} (뒤 항목 무시)");
                continue;
            }

            stepMap.Add(s.step, s);
        }
    }

    // step으로 비용 리스트 가져오기(권장)
    public List<Cost> GetCostsByStep(int step)
    {
        if (!IsLoaded) return new List<Cost>();

        if (stepMap.TryGetValue(step, out var s) && s != null)
            return s.costs ?? new List<Cost>();

        return new List<Cost>();
    }

    // step 단위로 Step 자체가 필요할 때
    public bool TryGetStep(int step, out UpgradeStep upgradeStep)
    {
        upgradeStep = null;
        if (!IsLoaded) return false;

        if (stepMap.TryGetValue(step, out var s) && s != null)
        {
            upgradeStep = s;
            return true;
        }

        return false;
    }
}

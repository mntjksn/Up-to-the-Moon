using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#region Data Models

[System.Serializable]
public class Cost
{
    // 필요한 재료(아이템) ID
    public int itemId;

    // 필요한 수량
    public int count;
}

[System.Serializable]
public class UpgradeStep
{
    // 업그레이드 단계(레벨)
    public int step;

    // 해당 단계에 필요한 재료 목록(여러 종류 가능)
    public List<Cost> costs;
}

[System.Serializable]
public class UpgradeStepWrapper
{
    // JsonUtility는 최상위 배열 파싱이 제한되므로 래퍼 구조로 감싼다.
    // JSON 최상위 키가 "UpgradeStep"이므로 필드명도 동일하게 유지한다.
    public List<UpgradeStep> UpgradeStep;
}

#endregion

/*
    UpgradeCostManager

    [역할]
    - 업그레이드 단계(step)별 필요 재료(Cost) 데이터를 JSON에서 로드한다.
    - step을 키로 빠르게 조회할 수 있도록 Dictionary(stepMap)를 구성한다.

    [설계 의도]
    - 조회는 UI 갱신/버튼 활성화 판단 등에서 반복 호출될 수 있으므로 O(1) 조회 구조를 사용한다.
    - 데이터가 없거나 로드 전 상태에서도 NullReference가 발생하지 않도록 방어한다.
    - 빈 결과는 매번 new List를 생성하지 않고 재사용 리스트(EmptyCosts)를 반환하여 GC를 줄인다.
*/
public class UpgradeCostManager : MonoBehaviour
{
    // 전역 접근을 위한 싱글톤 인스턴스
    public static UpgradeCostManager Instance;

    // JSON 키와 동일한 이름을 유지하여 파싱 시 매핑을 단순화한다.
    public List<UpgradeStep> UpgradeStep = new List<UpgradeStep>();

    // 외부에서 로드 완료 타이밍을 보장하기 위한 플래그
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "UpgradeCostData.json";

    // step -> UpgradeStep 빠른 조회용 캐시 맵(O(1) 조회)
    private readonly Dictionary<int, UpgradeStep> stepMap = new Dictionary<int, UpgradeStep>(256);

    // GC 방지: 결과가 없을 때 매번 new List를 만들지 않기 위한 재사용 리스트
    // 주의: 외부에서 이 리스트를 수정하지 않고 읽기 전용처럼 사용하도록 한다.
    private static readonly List<Cost> EmptyCosts = new List<Cost>(0);

    // 중복 로드 방지를 위한 코루틴 핸들
    private Coroutine loadCo;

    private void Awake()
    {
        // 싱글톤 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 씬 전환 중에도 로드가 중복 실행되지 않도록 보호한다.
        if (loadCo == null)
            loadCo = StartCoroutine(LoadUpgradeCostRoutine());
    }

    /*
        업그레이드 비용 데이터 로드 루틴

        로드 흐름:
        1) persistentDataPath에 JSON이 없으면 StreamingAssets에서 복사한다.
        2) JSON 텍스트를 읽고 파싱한다.
        3) stepMap을 구성하여 단계별 조회 성능을 확보한다.
    */
    private IEnumerator LoadUpgradeCostRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        // 최초 실행 시 persistentDataPath에 데이터가 없을 수 있으므로
        // StreamingAssets에서 복사하여 기준 데이터를 확보한다.
        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        // 복사 실패 또는 파일 미존재 시에도 게임이 멈추지 않도록 빈 데이터로 종료한다.
        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        // 동기 File IO로 인한 프리즈 체감을 줄이기 위해 한 프레임 양보한다.
        yield return null;

        string json = null;
        try { json = File.ReadAllText(targetPath); }
        catch { json = null; }

        // 비정상 데이터(빈 문자열 등) 방어 처리
        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        LoadFromJson(json);
        BuildMap();

        IsLoaded = true;
        loadCo = null;
    }

    /*
        StreamingAssets -> persistentDataPath 복사

        - Android에서는 StreamingAssets가 패키지 내부 경로가 될 수 있어
          File.Copy가 불가능한 경우가 있으므로 UnityWebRequest를 사용한다.
        - 에디터/기타 플랫폼에서는 File.Copy로 처리한다.
    */
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
        else
        {
            // 스트리밍 로드 실패는 개발 단계에서 빠르게 확인할 수 있도록 로그를 남긴다.
            Debug.LogError("[UpgradeCostManager] StreamingAssets JSON 로드 실패: " + req.error);
        }
#else
        if (File.Exists(streamingPath))
        {
            try { File.Copy(streamingPath, targetPath, true); }
            catch { }
        }
        else
        {
            Debug.LogWarning("[UpgradeCostManager] StreamingAssets에 JSON이 없습니다: " + streamingPath);
        }

        yield break;
#endif
    }

    /*
        JSON 파싱

        지원 형태:
        - 최상위 배열 JSON: [ { ... }, { ... } ]
        - 래퍼 JSON: { "UpgradeStep": [ ... ] }

        JsonUtility 제약:
        - 최상위 배열 파싱이 제한되므로 배열 입력은 래핑하여 처리한다.
    */
    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        UpgradeStepWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                string wrapped = "{\"UpgradeStep\":" + json + "}";
                wrapper = JsonUtility.FromJson<UpgradeStepWrapper>(wrapped);
            }
            else
            {
                wrapper = JsonUtility.FromJson<UpgradeStepWrapper>(json);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[UpgradeCostManager] JSON 파싱 실패: " + e.Message);
            wrapper = null;
        }

        // 파싱 실패 시에도 NullReference가 발생하지 않도록 빈 리스트로 초기화한다.
        UpgradeStep = (wrapper != null && wrapper.UpgradeStep != null)
            ? wrapper.UpgradeStep
            : new List<UpgradeStep>();
    }

    /*
        로드 실패 시에도 시스템이 동작 가능하도록 빈 데이터로 초기화하고 완료 처리한다.
        - stepMap도 함께 초기화하여 조회 로직이 안전하게 동작하도록 한다.
    */
    private void SetEmptyAndFinish()
    {
        UpgradeStep = new List<UpgradeStep>();
        BuildMap();
        IsLoaded = true;
    }

    /*
        stepMap 구성

        - step -> UpgradeStep 조회를 O(1)로 만들기 위한 캐시를 구성한다.
        - JSON 데이터에 step 중복이 있을 수 있으므로 중복을 감지하고 로그를 남긴다.
    */
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
                // 데이터 품질 문제를 빠르게 파악하기 위한 경고
                Debug.LogWarning("[UpgradeCostManager] step 중복 발견: " + s.step + " (뒤 항목 무시)");
                continue;
            }

            stepMap.Add(s.step, s);
        }
    }

    /*
        단계(step)에 해당하는 비용 목록을 반환한다.

        - 로드 전에는 빈 리스트를 반환하여 호출부에서 null 체크 부담을 줄인다.
        - 결과가 없을 때는 EmptyCosts를 반환하여 불필요한 할당과 GC를 방지한다.
    */
    public List<Cost> GetCostsByStep(int step)
    {
        if (!IsLoaded) return EmptyCosts;

        if (stepMap.TryGetValue(step, out var s) && s != null && s.costs != null)
            return s.costs;

        return EmptyCosts;
    }
}
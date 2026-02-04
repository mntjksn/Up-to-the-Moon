using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#region Data Models

[System.Serializable]
public class MissionItem
{
    // 미션 식별자(저장/업데이트 매칭 용도)
    public string id;

    // UI 표시용 텍스트
    public string title;
    public string desc;

    // 난이도 / 분류(필터링, UI 그룹핑 용도)
    public string tier;      // easy, normal, hard
    public string category;  // growth, region, resource, upgrade, play

    // 목표 유형 및 목표 키
    // 예: accumulate(gold), reach_value(speed), unlock(item_3) 등
    public string goalType;  // accumulate, reach_value, count, unlock, multi_reach 등
    public string goalKey;
    public double goalTarget;

    // 보상 값
    public long rewardGold;

    // 진행 상태(저장 대상)
    public double currentValue;
    public bool isCompleted;
    public bool rewardClaimed;
}

[System.Serializable]
public class MissionItemListWrapper
{
    // JsonUtility는 최상위 배열 파싱이 제한되므로 래퍼 구조로 감싼다.
    // JSON 최상위 키가 "missions"이므로 필드명도 동일하게 유지한다.
    public List<MissionItem> missions;
}

#endregion

/*
    MissionDataManager

    [역할]
    - 미션 데이터(JSON)를 로드/저장한다.
    - 미션 진행 상태(currentValue, isCompleted, rewardClaimed)를 앱 재실행 후에도 유지한다.

    [설계 의도]
    - 최초 실행 시 StreamingAssets의 기본 데이터를 persistentDataPath로 복사하여 기준 데이터를 확보한다.
    - 저장은 디바운스(debounce) 방식으로 묶어 파일 IO 호출을 줄이고 성능 스파이크를 완화한다.
    - JSON 파싱 실패/파일 손상 상황에서도 게임이 멈추지 않도록 방어 로직을 포함한다.
*/
public class MissionDataManager : MonoBehaviour
{
    // 전역 접근을 위한 싱글톤 인스턴스
    public static MissionDataManager Instance;

    // 런타임에서 사용하는 미션 리스트(로드 후 참조 대상)
    public List<MissionItem> MissionItem = new List<MissionItem>();

    // 외부에서 로드 완료 타이밍을 보장하기 위한 플래그
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "MissionItemData.json";

    // 중복 로드 방지를 위한 코루틴 핸들
    private Coroutine loadCo;

    [Header("Optimization - Save")]
    // 저장 요청이 연속으로 들어오는 경우(미션 값 연속 갱신 등)
    // 일정 시간 동안의 호출을 합쳐 실제 파일 저장을 1회로 줄인다.
    [SerializeField] private float saveDebounceSec = 0.5f;

    // 저장 JSON을 보기 좋게 줄바꿈할지 여부
    // 용량/문자열 생성/GC를 고려하여 기본은 false를 권장한다.
    [SerializeField] private bool prettyPrint = false;

    // 저장 코루틴 및 저장 예약 플래그
    private Coroutine saveCo;
    private bool saveDirty;

    // 실제 저장 위치는 persistentDataPath를 사용한다.
    private string TargetPath => Path.Combine(Application.persistentDataPath, JSON_NAME);

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

        StartLoad();
    }

    /*
        외부에서 즉시 재로딩을 호출하기 위한 API

        - 개발/테스트 중 데이터 리셋 또는 재적용 상황을 고려한다.
        - 비활성 상태에서는 코루틴 실행이 불가능하므로 방어한다.
    */
    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;
        StartLoad();
    }

    // 로드 코루틴 중복 실행을 방지하고, 항상 최신 로드를 보장한다.
    private void StartLoad()
    {
        if (loadCo != null)
            StopCoroutine(loadCo);

        loadCo = StartCoroutine(LoadMissionRoutine());
    }

    /*
        미션 데이터 로드 루틴

        로드 흐름:
        1) persistentDataPath에 JSON이 없으면 StreamingAssets에서 복사한다.
        2) JSON 텍스트를 읽고 파싱한다.
        3) 파싱 실패 시에도 빈 리스트로 안전하게 종료한다.
    */
    private IEnumerator LoadMissionRoutine()
    {
        IsLoaded = false;

        string targetPath = TargetPath;

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

        IsLoaded = true;
        loadCo = null;
    }

    /*
        StreamingAssets -> persistentDataPath 복사

        - Android에서는 StreamingAssets가 패키지 내부 경로가 될 수 있어
          File.Copy로 직접 접근이 불가능한 경우가 있으므로 UnityWebRequest를 사용한다.
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
#else
        if (File.Exists(streamingPath))
        {
            try { File.Copy(streamingPath, targetPath, true); }
            catch { }
        }

        yield break;
#endif
    }

    /*
        JSON 파싱

        지원 형태:
        - 최상위 배열 JSON: [ { ... }, { ... } ]
        - 래퍼 JSON: { "missions": [ ... ] }

        JsonUtility 제약:
        - 최상위 배열 파싱이 제한되므로 배열 입력은 래핑하여 처리한다.
    */
    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        MissionItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱한다.
                string wrapped = "{\"missions\":" + json + "}";
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱한다.
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        // 파싱 실패 시에도 NullReference가 발생하지 않도록 빈 리스트로 초기화한다.
        MissionItem = (wrapper != null && wrapper.missions != null)
            ? wrapper.missions
            : new List<MissionItem>();
    }

    // 로드 실패 시에도 시스템이 동작 가능하도록 빈 데이터로 초기화하고 완료 처리한다.
    private void SetEmptyAndFinish()
    {
        MissionItem = new List<MissionItem>();
        IsLoaded = true;
    }

    // -----------------------
    // 저장 최적화
    // -----------------------

    /*
        외부 호출용 저장 함수

        - 기존 코드처럼 SaveToJson()을 호출해도 동작 방식이 유지된다.
        - 단, 실제 저장은 즉시 수행하지 않고 debounce로 묶어 처리한다.
    */
    public void SaveToJson()
    {
        saveDirty = true;

        // 오브젝트가 비활성 상태면 코루틴 시작이 불가능하므로 예약만 유지한다.
        if (!gameObject.activeInHierarchy) return;

        // 이미 저장 코루틴이 실행 중이면 dirty 플래그만 누적한다.
        if (saveCo == null)
            saveCo = StartCoroutine(SaveRoutine());
    }

    /*
        저장 루틴

        - debounce 시간 동안 추가 호출을 기다린 뒤 실제 저장을 수행한다.
        - JSON 생성 및 파일 쓰기 전 한 프레임 양보하여 체감 프리징을 완화한다.
        - 저장 중 다시 호출되면(saveDirty=true) 종료 후 재예약한다.
    */
    private IEnumerator SaveRoutine()
    {
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

        // JSON 생성/파일 쓰기 전에 프레임을 양보한다.
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
            // 저장 실패는 치명적 예외로 처리하지 않고 경고 로그로 남긴다.
            Debug.LogWarning("[MissionDataManager] SaveToJson 실패: " + e.Message);
        }

        saveCo = null;

        // 저장 중 다시 dirty가 되었다면 저장을 재예약한다.
        if (saveDirty)
            saveCo = StartCoroutine(SaveRoutine());
    }
}
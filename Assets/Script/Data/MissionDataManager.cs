using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#region Data Models

/*
    MissionItem

    [역할]
    - 미션 1개의 “정의 + 진행 상태”를 담는 데이터 모델
    - JSON 로드/저장 대상이며, UI/로직에서 공통으로 참조한다.

    [필드 구성]
    - id: 저장/업데이트 매칭용 고유 식별자
    - title/desc: UI 표시 텍스트
    - tier/category: 난이도/분류(필터, UI 그룹핑)
    - goalType/goalKey/goalTarget: 목표 정의
    - rewardGold: 보상 값
    - currentValue/isCompleted/rewardClaimed: 진행 상태(저장 대상)
*/
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

/*
    MissionItemListWrapper

    [역할]
    - JsonUtility의 “최상위 배열 파싱 제한”을 우회하기 위한 래퍼 모델
    - JSON 형태가 { "missions": [ ... ] } 인 경우 필드명이 반드시 "missions"여야 한다.
*/
[System.Serializable]
public class MissionItemListWrapper
{
    // JSON 최상위 키가 "missions"이므로 필드명도 동일하게 유지한다.
    public List<MissionItem> missions;
}

#endregion

/*
    MissionDataManager

    [역할]
    - 미션 데이터(JSON)를 로드/저장한다.
    - 미션 진행 상태(currentValue, isCompleted, rewardClaimed)를 앱 재실행 후에도 유지한다.

    [로드 흐름]
    1) persistentDataPath에 JSON이 없으면 StreamingAssets에서 복사(초기 기준 데이터 확보)
    2) persistentDataPath의 JSON 텍스트를 읽고 파싱
    3) 파싱 실패/파일 손상/빈 파일이면 안전하게 빈 리스트로 처리(게임 중단 방지)

    [저장 설계]
    - SaveToJson()은 즉시 쓰지 않고 debounce(saveDebounceSec)로 묶어서 파일 I/O 횟수를 줄인다.
    - SaveToJsonImmediate()는 즉시 저장(디버그/강제 저장/종료 직전 등에 사용)

    [플랫폼 주의]
    - Android에서 StreamingAssets는 패키지 내부 경로가 될 수 있어 File.Copy가 실패할 수 있다.
      → UnityWebRequest로 읽어서 persistentDataPath에 WriteAllText 한다.
*/
public class MissionDataManager : MonoBehaviour
{
    // 전역 접근용 싱글톤
    public static MissionDataManager Instance;

    // 런타임에서 사용하는 미션 리스트(로드 후 참조 대상)
    public List<MissionItem> MissionItem = new List<MissionItem>();

    // 외부에서 로드 완료 타이밍을 보장하기 위한 플래그
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "MissionItemData.json";

    // 중복 로드 방지 코루틴 핸들
    private Coroutine loadCo;

    [Header("Optimization - Save")]
    [SerializeField] private float saveDebounceSec = 0.5f; // 저장 디바운스 시간
    [SerializeField] private bool prettyPrint = false;     // JSON 보기좋게 출력(기본 false 권장)

    // 저장 코루틴 및 저장 예약 플래그
    private Coroutine saveCo;
    private bool saveDirty;

    // 실제 저장 위치(persistentDataPath)
    private string TargetPath => Path.Combine(Application.persistentDataPath, JSON_NAME);

    private void Awake()
    {
        /*
            싱글톤 중복 생성 방지 + 씬 전환 유지
            - 이미 Instance가 있고 내가 아니면 파괴
            - 최초 생성이면 DontDestroyOnLoad로 유지
        */
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 시작 시 로드
        StartLoad();
    }

    // -----------------------
    // Load
    // -----------------------

    /*
        외부에서 즉시 재로딩을 호출하기 위한 API
        - 개발/테스트(데이터 리셋/재적용) 등에 사용
        - 비활성 상태에서는 코루틴 실행이 불가능하므로 방어
    */
    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;
        StartLoad();
    }

    /*
        로드 코루틴 중복 실행 방지
        - 기존 로드가 돌고 있으면 중단 후 최신 로드를 시작
    */
    private void StartLoad()
    {
        if (loadCo != null)
            StopCoroutine(loadCo);

        loadCo = StartCoroutine(LoadMissionRoutine());
    }

    /*
        미션 데이터 로드 루틴

        1) persistent에 파일 없으면 StreamingAssets에서 복사
        2) 파일 읽기
        3) JSON 파싱
        4) 실패 시 빈 데이터로 안전 종료
    */
    private IEnumerator LoadMissionRoutine()
    {
        IsLoaded = false;

        string targetPath = TargetPath;

        // 최초 실행: persistent에 없으면 StreamingAssets에서 복사
        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        // 복사 실패/파일 미존재: 빈 리스트로 종료(게임 중단 방지)
        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        // 동기 File I/O 프리즈 체감 완화를 위해 프레임 양보
        yield return null;

        string json = null;
        try { json = File.ReadAllText(targetPath); }
        catch { json = null; }

        // 비정상 데이터(빈 문자열 등) 방어
        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        // 파싱
        LoadFromJson(json);

        IsLoaded = true;
        loadCo = null;
    }

    /*
        StreamingAssets -> persistentDataPath 복사

        - Android(특히 apk 내부)에서는 StreamingAssets 파일을 File.Copy로 못 읽는 경우가 많다.
          → UnityWebRequest로 텍스트를 읽어 persistent에 WriteAllText
        - 그 외 플랫폼/에디터: File.Copy 사용
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
        - 최상위 배열은 직접 파싱이 제한되므로,
          배열 입력이면 {"missions": ...} 로 감싸서 파싱한다.
    */
    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        MissionItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON -> wrapper로 감싸서 파싱
                string wrapped = "{\"missions\":" + json + "}";
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON 그대로 파싱
                wrapper = JsonUtility.FromJson<MissionItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        // 파싱 결과 적용(실패 시 빈 리스트)
        MissionItem = (wrapper != null && wrapper.missions != null)
            ? wrapper.missions
            : new List<MissionItem>();

        /*
            보상 수령된 미션은 완료 상태 강제 유지
            - rewardClaimed=true인데 isCompleted=false로 저장돼 있더라도 UI/로직 일관성 유지
        */
        for (int i = 0; i < MissionItem.Count; i++)
        {
            var m = MissionItem[i];
            if (m == null) continue;

            if (m.rewardClaimed)
                m.isCompleted = true;
        }
    }

    /*
        로드 실패 시에도 시스템이 멈추지 않도록
        빈 리스트로 초기화하고 로드 완료 처리
    */
    private void SetEmptyAndFinish()
    {
        MissionItem = new List<MissionItem>();
        IsLoaded = true;
    }

    // -----------------------
    // Save (Debounce)
    // -----------------------

    /*
        외부 호출용 저장 함수(기존 코드 호환)
        - SaveToJson() 호출이 잦을 때 파일 쓰기 횟수를 줄이기 위해 debounce 방식 사용
        - 오브젝트 비활성 상태면 코루틴 실행이 안 되므로 dirty만 예약
    */
    public void SaveToJson()
    {
        saveDirty = true;

        if (!gameObject.activeInHierarchy) return;

        if (saveCo == null)
            saveCo = StartCoroutine(SaveRoutine());
    }

    /*
        저장 루틴
        - saveDebounceSec 동안 추가 호출을 기다렸다가 1회만 저장
        - JSON 생성/파일 쓰기 전 프레임 양보로 체감 프리즈 완화
        - 저장 중 다시 dirty가 되면 종료 후 재예약
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

        // JSON 생성/파일 쓰기 전 프레임 양보
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

        // 저장 중 다시 dirty가 되었으면 재예약
        if (saveDirty)
            saveCo = StartCoroutine(SaveRoutine());
    }

    /*
        즉시 저장(강제)
        - 디버그/강제 저장/앱 종료 직전 저장 등에 사용
        - 예약 코루틴이 있으면 중단하고 바로 파일에 기록
    */
    public void SaveToJsonImmediate()
    {
        if (saveCo != null)
        {
            StopCoroutine(saveCo);
            saveCo = null;
        }

        saveDirty = false;

        string targetPath = TargetPath;

        try
        {
            var wrapper = new MissionItemListWrapper { missions = this.MissionItem };
            string json = JsonUtility.ToJson(wrapper, prettyPrint);
            File.WriteAllText(targetPath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MissionDataManager] SaveToJsonImmediate 실패: " + e.Message);
        }
    }
}
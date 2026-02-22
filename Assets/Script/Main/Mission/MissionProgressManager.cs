using System;
using UnityEngine;

/*
    MissionProgressManager

    [역할]
    - 게임 내 모든 미션 진행도를 중앙에서 관리하는 매니저.
    - 각종 시스템(골드 획득, 이동, 업그레이드, 플레이 시간 등)에서
      미션 목표에 해당하는 값이 변하면 이 매니저를 통해 반영한다.
    - 변경된 미션 데이터는 일정 주기로 저장(saveInterval)된다.
    - UI는 OnMissionStateChanged 이벤트를 구독하여 변경 시점에 갱신된다.

    [설계 의도]
    1) 싱글톤 구조
       - Instance로 어디서든 접근 가능.
       - DontDestroyOnLoad로 씬 전환 시에도 유지.

    2) 저장 쓰로틀링(Save Throttle)
       - 변경이 발생할 때마다 즉시 저장하지 않고,
         saveInterval 이후에 한 번만 저장한다.
       - 잦은 디스크 I/O를 방지.

    3) 자동 Tick 시스템
       - gold, speed, km, play_time 등 자동으로 갱신되어야 하는 값은
         autoTickInterval 주기로만 체크한다.
       - 매 프레임 반복 체크를 피해서 성능을 확보.

    4) 이벤트 디바운스
       - 여러 미션이 한 프레임에 동시에 바뀌어도
         UI 이벤트는 프레임당 1회만 발생하도록 큐잉한다.

    [주의/전제]
    - MissionDataManager.Instance가 먼저 생성되어 있어야 한다.
    - MissionItem.goalType / goalKey 값은 문자열로 관리되므로
      오타가 나지 않도록 상수화해도 좋다.
*/

public class MissionProgressManager : MonoBehaviour
{
    // =====================
    // Singleton / Event
    // =====================

    public static MissionProgressManager Instance;               // 전역 접근용 싱글톤
    public static event Action OnMissionStateChanged;            // UI가 구독하는 “미션 상태 변경” 이벤트

    // =====================
    // Inspector Settings
    // =====================

    [Header("Save throttle")]
    [SerializeField] private float saveInterval = 0.5f;          // 변경이 많을 때 저장을 묶어서(쓰로틀) 처리하는 간격

    [Header("Auto tick (perf)")]
    [SerializeField] private float autoTickInterval = 0.25f;     // 자동 갱신(골드/속도/거리/플레이타임 등) 수행 주기

    // =====================
    // Runtime State
    // =====================

    private float nextSaveTime = 0f;                             // 다음 저장 가능한 시간(Time.time 기준)
    private bool dirty = false;                                  // 저장 필요 여부(변경 발생)

    private float nextAutoTickTime = 0f;                         // 다음 auto tick 시간(unscaledTime 기준)
    private float lastPlayTickTime = -1f;                        // 플레이타임 누적을 위한 “이전 tick 시각”

    private bool notifyQueued = false;                           // 이벤트 폭주 방지: 프레임당 1회만 Invoke

    // =====================
    // (추가) 문자열 비교 최적화용 상수/옵션
    // =====================

    // 문자열 비교는 문화권 영향 없는 Ordinal로 고정(빠르고 안정적)
    private const StringComparison CMP = StringComparison.Ordinal;

    // goalType 문자열을 상수로 고정(오타 방지 + 비교 비용/GC 최소화)
    private const string TYPE_REACH = "reach_value";
    private const string TYPE_ACC = "accumulate";
    private const string TYPE_COUNT = "count";
    private const string TYPE_UNLOCK = "unlock";
    private const string TYPE_MULTI = "multi_reach";

    // multi_reach에서 쓰는 goalKey 상수
    private const string KEY_EACH_RESOURCE_AMOUNT = "each_resource_amount";

    // =====================
    // Unity Lifecycle
    // =====================

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 바뀌어도 유지
    }

    private void Update()
    {
        // ---------------------
        // 1) Auto tick (주기적으로만)
        // ---------------------
        // 매 프레임 TickAutoKeys()를 돌리면 비용이 커질 수 있어,
        // autoTickInterval 간격으로만 수행
        if (Time.unscaledTime >= nextAutoTickTime)
        {
            nextAutoTickTime = Time.unscaledTime + Mathf.Max(0.05f, autoTickInterval);
            TickAutoKeys();
        }

        // ---------------------
        // 2) Save throttle
        // ---------------------
        // dirty 상태에서 saveInterval이 지나면 1회 저장
        if (dirty && Time.time >= nextSaveTime)
            FlushSave();

        // ---------------------
        // 3) Notify debounce
        // ---------------------
        // 여러 곳에서 QueueNotify()가 연속 호출되어도,
        // 실제 이벤트는 프레임당 1회만 발생
        if (notifyQueued)
        {
            notifyQueued = false;
            OnMissionStateChanged?.Invoke();
        }
    }

    // =====================
    // External API
    // =====================

    /// <summary>
    /// 누적형(accumulate/count) 목표값을 delta만큼 증가시킨다.
    /// </summary>
    public void Add(string goalKey, double delta)
    {
        var mdm = MissionDataManager.Instance;
        if (mdm == null || !mdm.IsLoaded) return;
        var list = mdm.MissionItem;

        bool changedAny = false;

        // 입력 key는 1회만 Trim
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.rewardClaimed) continue;

            // accumulate / count만 처리
            if (m.goalType != TYPE_ACC && m.goalType != TYPE_COUNT) continue;

            // 데이터가 깔끔하다는 가정으로 m.goalKey는 Ordinal 비교 우선,
            // 혹시 공백이 섞여 있으면 Trim 비교로 한 번 더 체크
            if (!string.Equals(m.goalKey, inKey, CMP) &&
                !string.Equals((m.goalKey ?? "").Trim(), inKey, CMP))
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            // 누적 값 적용(음수 방어로 0 미만 방지)
            m.currentValue = Math.Max(0, m.currentValue + delta);

            // 목표 달성 체크
            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            // 실제 변경이 있었을 때만 dirty/notify
            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /// <summary>
    /// 도달형(reach_value) 목표값을 value로 세팅한다.
    /// </summary>
    public void SetValue(string goalKey, double value)
    {
        var mdm = MissionDataManager.Instance;
        if (mdm == null || !mdm.IsLoaded) return;
        var list = mdm.MissionItem;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.rewardClaimed) continue;

            if (m.goalType != TYPE_REACH) continue;

            if (!string.Equals(m.goalKey, inKey, CMP) &&
                !string.Equals((m.goalKey ?? "").Trim(), inKey, CMP))
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            // 값 세팅
            m.currentValue = value;

            // 목표 달성 체크
            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            // 변경 감지
            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /// <summary>
    /// unlock 타입 목표를 “해금됨”으로 처리한다. (false는 변화 없음)
    /// </summary>
    public void SetUnlocked(string goalKey, bool unlocked)
    {
        // false면 아무 변화 없음(불필요 루프 방지)
        if (!unlocked) return;

        var mdm = MissionDataManager.Instance;
        if (mdm == null || !mdm.IsLoaded) return;
        var list = mdm.MissionItem;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.rewardClaimed) continue;

            if (m.goalType != TYPE_UNLOCK) continue;

            if (!string.Equals(m.goalKey, inKey, CMP) &&
                !string.Equals((m.goalKey ?? "").Trim(), inKey, CMP))
                continue;

            if (!m.isCompleted)
            {
                m.currentValue = 1;
                m.isCompleted = true;
                changedAny = true;
            }
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /// <summary>
    /// 저장 없이 UI만 갱신하고 싶을 때 사용(버튼 상태 갱신 등).
    /// </summary>
    public void NotifyMissionStateChangedUIOnly()
    {
        QueueNotify();
    }

    // =====================
    // Save / Notify
    // =====================

    /// <summary>
    /// 저장 쓰로틀이 만료되었을 때 실제 저장을 수행한다.
    /// </summary>
    public void FlushSave()
    {
        if (!dirty) return;                 // dirty 없으면 굳이 안 씀
        MissionDataManager.Instance?.SaveToJsonImmediate();
        dirty = false;
    }

    /// <summary>
    /// 변경 발생 시 저장 예약 + UI 갱신 예약
    /// </summary>
    private void MarkDirtyAndNotify()
    {
        dirty = true;
        nextSaveTime = Time.time + saveInterval; // Time.time 기준 저장 쓰로틀
        QueueNotify();
    }

    /// <summary>
    /// 프레임당 1회만 이벤트가 나가도록 플래그만 세팅
    /// </summary>
    private void QueueNotify()
    {
        notifyQueued = true;
    }

    // =====================
    // Auto Tick (periodic)
    // =====================

    /// <summary>
    /// 주기적으로 자동 반영되는 목표 키들을 갱신한다.
    /// </summary>
    private void TickAutoKeys()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        bool changedThisTick = false;

        // 골드/속도/거리 같은 값은 “reach_value” 목표에 반영
        changedThisTick |= SetValue_NoNotify("gold", sm.GetGold());
        changedThisTick |= SetValue_NoNotify("player_speed", sm.GetSpeed());
        changedThisTick |= SetValue_NoNotify("distance_km", sm.GetKm());

        // ---------------------
        // 플레이타임: unscaledTime 기반 “실제 경과” 누적
        // ---------------------
        float now = Time.unscaledTime;
        if (lastPlayTickTime < 0f) lastPlayTickTime = now;

        float dt = now - lastPlayTickTime;
        lastPlayTickTime = now;

        // 앱 복귀 직후 dt가 과도하게 커지는 것을 방지(최대 1초까지만 반영)
        dt = Mathf.Clamp(dt, 0f, 1f);

        changedThisTick |= AddPlayTime_NoNotify("play_time", dt);

        // ---------------------
        // 각 자원을 일정 이상 보유했는지 검사(multi_reach)
        // ---------------------
        var data = sm.Data;
        if (data != null && data.resources != null)
        {
            // Array/Convert 경로 제거: int[] 직접 검사
            changedThisTick |= CheckEachResourceAtLeast_NoNotify(data.resources, 2000);
        }

        // tick에서 변경이 발생했으면 저장/notify 예약
        if (changedThisTick)
        {
            dirty = true;
            nextSaveTime = Time.time + saveInterval;
            QueueNotify();
        }
    }

    // =====================
    // Internal - NoNotify (tick 전용)
    // =====================

    /// <summary>
    /// (tick 전용) reach_value 목표값을 설정하고, 변경 여부만 반환한다.
    /// </summary>
    private bool SetValue_NoNotify(string goalKey, double value)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return false;

        bool changedAny = false;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.rewardClaimed) continue;

            if (m.goalType != TYPE_REACH) continue;
            if (!string.Equals(m.goalKey, goalKey, CMP)) continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = value;

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    /// <summary>
    /// multi_reach(each_resource_amount) 목표를 ok 여부에 맞게 갱신한다.
    /// </summary>
    private bool SetMultiReachEachResourceAmount_NoNotify(long value, bool ok)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return false;

        bool changedAny = false;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.rewardClaimed) continue;

            if (m.goalType != TYPE_MULTI) continue;
            if (!string.Equals(m.goalKey, KEY_EACH_RESOURCE_AMOUNT, CMP)) continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            // 조건 만족이면 value, 아니면 0
            m.currentValue = ok ? value : 0;

            if (!m.isCompleted && ok)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    /// <summary>
    /// 모든 자원이 targetEach 이상인지 검사(int[] 직접).
    /// </summary>
    private bool CheckEachResourceAtLeast_NoNotify(int[] resources, int targetEach)
    {
        if (resources == null || resources.Length == 0) return false;

        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] < targetEach)
                return SetMultiReachEachResourceAmount_NoNotify(targetEach, false);
        }

        return SetMultiReachEachResourceAmount_NoNotify(targetEach, true);
    }

    /// <summary>
    /// play_time 누적(도달형/누적형 혼용 가능성을 고려해 타입을 넓게 허용).
    /// </summary>
    private bool AddPlayTime_NoNotify(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return false;

        bool changedAny = false;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.rewardClaimed) continue;

            // play_time은 reach/acc/count 어느 쪽으로 정의돼도 누적될 수 있게 허용
            if (m.goalType != TYPE_REACH && m.goalType != TYPE_ACC && m.goalType != TYPE_COUNT)
                continue;

            if (!string.Equals(m.goalKey, goalKey, CMP)) continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = Math.Max(0, m.currentValue + delta);

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) FlushSave(); // FlushSave가 Immediate로 바뀌었으니 OK
    }

    private void OnApplicationQuit()
    {
        FlushSave();
    }

    private void OnDisable()
    {
        // 씬 전환/오브젝트 비활성화에서도 보험
        FlushSave();
    }
}
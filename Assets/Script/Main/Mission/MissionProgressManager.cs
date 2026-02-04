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
    public static MissionProgressManager Instance;

    // 미션 상태 변경 시 UI 등이 구독하는 이벤트
    public static event Action OnMissionStateChanged;

    [Header("Save throttle")]
    [SerializeField] private float saveInterval = 0.5f; // 저장 최소 간격

    [Header("Auto tick (perf)")]
    [SerializeField] private float autoTickInterval = 0.25f; // 자동 체크 주기

    private float nextSaveTime = 0f;   // 다음 저장 가능 시간
    private bool dirty = false;        // 저장 필요 여부

    private float nextAutoTickTime = 0f; // 다음 auto tick 시간

    // 이벤트 폭주 방지(프레임당 1회)
    private bool notifyQueued = false;

    private void Awake()
    {
        // 싱글톤 유지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        /*
            Auto Tick
            - 매 프레임 하지 않고 autoTickInterval 주기로만 실행
        */
        if (Time.unscaledTime >= nextAutoTickTime)
        {
            nextAutoTickTime = Time.unscaledTime + Mathf.Max(0.05f, autoTickInterval);
            TickAutoKeys();
        }

        /*
            저장 쓰로틀
            - dirty 상태이고 저장 시간이 되었을 때만 실제 저장
        */
        if (dirty && Time.time >= nextSaveTime)
            FlushSave();

        /*
            UI 이벤트 디바운스
            - 한 프레임에 여러 번 큐잉되어도 1번만 Invoke
        */
        if (notifyQueued)
        {
            notifyQueued = false;
            OnMissionStateChanged?.Invoke();
        }
    }

    // -------------------------
    // 외부에서 사용하는 API
    // -------------------------

    /*
        누적형 미션(accumulate, count)에 값 추가
        예) gold 획득량, 몬스터 처치 수 등
    */
    public void Add(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance != null
            ? MissionDataManager.Instance.MissionItem
            : null;
        if (list == null) return;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            // 누적형 미션만 처리
            if (m.goalType != "accumulate" && m.goalType != "count")
                continue;

            if (!string.Equals((m.goalKey ?? "").Trim(), inKey, StringComparison.Ordinal))
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            // 값 증가
            m.currentValue = Math.Max(0, m.currentValue + delta);

            // 목표 달성 체크
            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            // 변화 감지
            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 ||
                m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /*
        특정 값 도달형 미션(reach_value)에 값 세팅
        예) 현재 속도, 현재 거리, 현재 골드 등
    */
    public void SetValue(string goalKey, double value)
    {
        var list = MissionDataManager.Instance != null
            ? MissionDataManager.Instance.MissionItem
            : null;
        if (list == null) return;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "reach_value") continue;
            if (!string.Equals((m.goalKey ?? "").Trim(), inKey, StringComparison.Ordinal))
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = value;

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 ||
                m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /*
        해금형 미션(unlock)
        unlocked가 true일 때만 완료 처리
    */
    public void SetUnlocked(string goalKey, bool unlocked)
    {
        if (!unlocked) return;

        var list = MissionDataManager.Instance != null
            ? MissionDataManager.Instance.MissionItem
            : null;
        if (list == null) return;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "unlock") continue;
            if (!string.Equals((m.goalKey ?? "").Trim(), inKey, StringComparison.Ordinal))
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

    /*
        UI만 강제 갱신하고 싶을 때 사용
        (데이터 변경 없이)
    */
    public void NotifyMissionStateChangedUIOnly()
    {
        QueueNotify();
    }

    // -------------------------
    // 저장 관련
    // -------------------------

    // 즉시 저장
    public void FlushSave()
    {
        dirty = false;
        MissionDataManager.Instance?.SaveToJson();
    }

    // dirty 플래그 + 이벤트 큐잉
    private void MarkDirtyAndNotify()
    {
        dirty = true;
        nextSaveTime = Time.time + saveInterval;
        QueueNotify();
    }

    private void QueueNotify()
    {
        notifyQueued = true;
    }

    // -------------------------
    // 자동 Tick 처리
    // -------------------------

    /*
        주기적으로 자동 갱신해야 하는 키들 처리
        - gold
        - player_speed
        - distance_km
        - play_time
        - each_resource_amount (모든 자원이 일정 수 이상인지)
    */
    private void TickAutoKeys()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        bool changedThisTick = false;

        changedThisTick |= SetValue_NoNotify("gold", sm.GetGold());
        changedThisTick |= SetValue_NoNotify("player_speed", sm.GetSpeed());
        changedThisTick |= SetValue_NoNotify("distance_km", sm.GetKm());

        // 플레이타임 누적
        changedThisTick |= AddReachValue_NoNotify("play_time", Time.unscaledDeltaTime);

        var data = sm.Data;
        if (data != null && data.resources != null)
            changedThisTick |= CheckEachResourceAtLeast_NoNotify(data.resources, 500);

        if (changedThisTick)
        {
            dirty = true;
            nextSaveTime = Time.time + saveInterval;
            QueueNotify();
        }
    }

    // -------------------------
    // 내부 NoNotify 버전
    // (이벤트/저장 호출 안 함)
    // -------------------------

    private bool SetValue_NoNotify(string goalKey, double value)
    {
        var list = MissionDataManager.Instance != null
            ? MissionDataManager.Instance.MissionItem
            : null;
        if (list == null) return false;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "reach_value") continue;
            if (!string.Equals((m.goalKey ?? "").Trim(), inKey, StringComparison.Ordinal))
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = value;

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 ||
                m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    private bool AddReachValue_NoNotify(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance != null
            ? MissionDataManager.Instance.MissionItem
            : null;
        if (list == null) return false;

        bool changedAny = false;
        string inKey = (goalKey ?? "").Trim();

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "reach_value") continue;
            if (!string.Equals((m.goalKey ?? "").Trim(), inKey, StringComparison.Ordinal))
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = Math.Max(0, m.currentValue + delta);

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 ||
                m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    private bool SetMultiReachEachResourceAmount_NoNotify(long value, bool ok)
    {
        var list = MissionDataManager.Instance != null
            ? MissionDataManager.Instance.MissionItem
            : null;
        if (list == null) return false;

        bool changedAny = false;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "multi_reach") continue;
            if (m.goalKey != "each_resource_amount") continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = ok ? value : 0;

            if (!m.isCompleted && ok)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 ||
                m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    /*
        resources 배열의 모든 값이 targetEach 이상인지 검사
        (Array 사용 → boxing/Convert 비용 있음, tick 주기로만 호출)
    */
    private bool CheckEachResourceAtLeast_NoNotify(Array resources, int targetEach)
    {
        if (resources == null || resources.Length == 0) return false;

        bool ok = true;
        for (int i = 0; i < resources.Length; i++)
        {
            long v = Convert.ToInt64(resources.GetValue(i));
            if (v < targetEach)
            {
                ok = false;
                break;
            }
        }

        return SetMultiReachEachResourceAmount_NoNotify(targetEach, ok);
    }
}
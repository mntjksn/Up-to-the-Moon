using System;
using UnityEngine;

public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance;

    // ★ 상시 UI(완료!!), 미션창 UI 등이 모두 이 이벤트를 구독
    public static event Action OnMissionStateChanged;

    [Header("Save throttle")]
    [SerializeField] private float saveInterval = 0.5f;

    private float nextSaveTime = 0f;
    private bool dirty = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // reach_value류(골드/속도/거리/플레이타임 등) 자동 갱신
        TickAutoKeys();

        // 일정 주기마다 한번만 저장
        if (dirty && Time.time >= nextSaveTime)
            FlushSave();
    }

    // -----------------------
    // 외부에서 쓰는 API
    // -----------------------

    // 누적(합산)용: accumulate/count
    public void Add(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return;

        bool changedAny = false;

        foreach (var m in list)
        {
            if (m.rewardClaimed) continue;
            if (m.goalKey != goalKey) continue;

            if (m.goalType == "accumulate" || m.goalType == "count")
            {
                double before = m.currentValue;
                m.currentValue = Math.Max(0, m.currentValue + delta);

                if (!m.isCompleted && m.currentValue >= m.goalTarget)
                    m.isCompleted = true;

                if (Math.Abs(m.currentValue - before) > 0.000001 || m.isCompleted)
                    changedAny = true;
            }
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    // 현재값 세팅용: reach_value
    public void SetValue(string goalKey, double value)
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return;

        bool changedAny = false;

        foreach (var m in list)
        {
            if (m.rewardClaimed) continue;
            if (m.goalKey != goalKey) continue;

            if (m.goalType == "reach_value")
            {
                double before = m.currentValue;
                m.currentValue = value;

                if (!m.isCompleted && m.currentValue >= m.goalTarget)
                    m.isCompleted = true;

                if (Math.Abs(m.currentValue - before) > 0.000001 || m.isCompleted)
                    changedAny = true;
            }
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    // 해금용: unlock
    public void SetUnlocked(string goalKey, bool unlocked)
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return;

        bool changedAny = false;

        foreach (var m in list)
        {
            if (m.rewardClaimed) continue;
            if (m.goalType != "unlock") continue;
            if (m.goalKey != goalKey) continue;

            if (!m.isCompleted && unlocked)
            {
                m.currentValue = 1;
                m.isCompleted = true;
                changedAny = true;
            }
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    // multi_reach: 각 자원 n개 이상 (SaveData.resources가 int[]일 때)
    public void CheckEachResourceAtLeast(int[] resources, int targetEach)
    {
        if (resources == null || resources.Length == 0) return;

        bool ok = true;
        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] < targetEach) { ok = false; break; }
        }

        bool changedAny = SetMultiReachEachResourceAmount_NoNotify(ok ? targetEach : 0, ok);
        if (changedAny) MarkDirtyAndNotify();
    }

    // multi_reach: 각 자원 n개 이상 (SaveData.resources가 long[]일 때)
    public void CheckEachResourceAtLeast(long[] resources, long targetEach)
    {
        if (resources == null || resources.Length == 0) return;

        bool ok = true;
        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] < targetEach) { ok = false; break; }
        }

        bool changedAny = SetMultiReachEachResourceAmount_NoNotify(ok ? targetEach : 0, ok);
        if (changedAny) MarkDirtyAndNotify();
    }

    // ★ MissionSlot(보상수령) 같은 곳에서 "UI만" 갱신하고 싶을 때 호출
    // (보상수령은 보통 MissionDataManager.SaveToJson()을 이미 했으니 dirty 필요 없음)
    public void NotifyMissionStateChangedUIOnly()
    {
        RaiseMissionStateChanged();
    }

    public void FlushSave()
    {
        dirty = false;
        nextSaveTime = Time.time + saveInterval;

        MissionDataManager.Instance?.SaveToJson();
    }

    // -----------------------
    // 내부 처리
    // -----------------------

    private void MarkDirtyAndNotify()
    {
        dirty = true;
        nextSaveTime = Time.time + saveInterval;

        RaiseMissionStateChanged();
    }

    private void RaiseMissionStateChanged()
    {
        // 상시 UI(완료!! 등)
        OnMissionStateChanged?.Invoke();

        // 미션창이 열려있으면 슬롯 Refresh 등
        MissionManager.Instance?.OnExternalMissionStateChanged();
    }

    // speed/gold/km/play_time 같은건 “현재값 따라” 계속 체크되는게 좋음
    private void TickAutoKeys()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        bool changedThisTick = false;

        // gold / speed / km (너 프로젝트 getter에 맞춰)
        changedThisTick |= SetValue_NoNotify("gold", sm.GetGold());
        changedThisTick |= SetValue_NoNotify("player_speed", sm.GetSpeed());
        changedThisTick |= SetValue_NoNotify("distance_km", sm.GetKm());

        // play_time 누적(초) : reach_value인데 누적 형태
        changedThisTick |= AddReachValue_NoNotify("play_time", Time.unscaledDeltaTime);

        // 각 자원 500 이상 (resources 타입에 맞춰 자동 처리)
        var data = sm.Data;
        if (data != null && data.resources != null)
        {
            // data.resources 타입이 int[]이면 int[] 오버로드가 호출됨
            // data.resources 타입이 long[]이면 long[] 오버로드가 호출됨
            // 하지만 TickAutoKeys에서 이벤트 중복 방지를 위해 NoNotify로 처리하고 싶으면:
            // - 여기서는 "상태만 계산"하고 changedThisTick에만 반영하는 구조가 깔끔함.
            // 지금은 CheckEachResourceAtLeast가 Notify까지 하므로,
            // TickAutoKeys 중복 이벤트를 막기 위해 아래처럼 NoNotify 처리로 통일한다.

            changedThisTick |= CheckEachResourceAtLeast_NoNotify(data.resources, 500);
        }

        if (changedThisTick)
        {
            dirty = true;
            nextSaveTime = Time.time + saveInterval;
            RaiseMissionStateChanged();
        }
    }

    // reach_value 세팅 (notify 없이 dirty만 켜기) → 변경되면 true
    private bool SetValue_NoNotify(string goalKey, double value)
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return false;

        bool changedAny = false;

        foreach (var m in list)
        {
            if (m.rewardClaimed) continue;
            if (m.goalType != "reach_value") continue;
            if (m.goalKey != goalKey) continue;

            double before = m.currentValue;
            m.currentValue = value;

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - before) > 0.000001 || m.isCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    // play_time 같은 “reach_value인데 누적” (notify 없이) → 변경되면 true
    private bool AddReachValue_NoNotify(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return false;

        bool changedAny = false;

        foreach (var m in list)
        {
            if (m.rewardClaimed) continue;
            if (m.goalType != "reach_value") continue;
            if (m.goalKey != goalKey) continue;

            double before = m.currentValue;
            m.currentValue = Math.Max(0, m.currentValue + delta);

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - before) > 0.000001 || m.isCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    // multi_reach 세팅 (notify 없이) → 변경되면 true
    private bool SetMultiReachEachResourceAmount_NoNotify(long value, bool ok)
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return false;

        bool changedAny = false;

        foreach (var m in list)
        {
            if (m.rewardClaimed) continue;
            if (m.goalType != "multi_reach") continue;
            if (m.goalKey != "each_resource_amount") continue;

            double before = m.currentValue;
            m.currentValue = ok ? value : 0;

            if (!m.isCompleted && ok)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - before) > 0.000001 || m.isCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    // TickAutoKeys용 (resources 타입이 int[]/long[] 둘 다 대응, notify 없이) → 변경되면 true
    private bool CheckEachResourceAtLeast_NoNotify(Array resources, int targetEach)
    {
        if (resources == null || resources.Length == 0) return false;

        bool ok = true;

        // int[] / long[] 모두 안전 처리
        for (int i = 0; i < resources.Length; i++)
        {
            long v = Convert.ToInt64(resources.GetValue(i));
            if (v < targetEach) { ok = false; break; }
        }

        return SetMultiReachEachResourceAmount_NoNotify(targetEach, ok);
    }
}
using System;
using UnityEngine;

public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance;
    public static event Action OnMissionStateChanged;

    [Header("Save throttle")]
    [SerializeField] private float saveInterval = 0.5f;

    [Header("Auto tick (perf)")]
    [SerializeField] private float autoTickInterval = 0.25f; // 핵심: 프레임마다 X

    private float nextSaveTime = 0f;
    private bool dirty = false;

    private float nextAutoTickTime = 0f;

    // 이벤트 폭주 방지(한 프레임 1번만)
    private bool notifyQueued = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Auto tick: 일정 주기만
        if (Time.time >= nextAutoTickTime)
        {
            nextAutoTickTime = Time.time + Mathf.Max(0.05f, autoTickInterval);
            TickAutoKeys();
        }

        // 저장 throttle
        if (dirty && Time.time >= nextSaveTime)
            FlushSave();

        // notify 디바운스(프레임 끝에서 1번)
        if (notifyQueued)
        {
            notifyQueued = false;
            OnMissionStateChanged?.Invoke();
        }
    }

    public void Add(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return;

        bool changedAny = false;
        string inKey = (goalKey != null) ? goalKey.Trim() : "";

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (!string.Equals((m.goalKey != null) ? m.goalKey.Trim() : "", inKey, StringComparison.Ordinal))
                continue;

            if (m.goalType != "accumulate" && m.goalType != "count")
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = Math.Max(0, m.currentValue + delta);

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    public void SetValue(string goalKey, double value)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return;

        bool changedAny = false;
        string inKey = goalKey;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "reach_value") continue;
            if (m.goalKey != inKey) continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            m.currentValue = value;

            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    public void SetUnlocked(string goalKey, bool unlocked)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return;

        bool changedAny = false;
        string inKey = goalKey;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "unlock") continue;
            if (m.goalKey != inKey) continue;

            if (!m.isCompleted && unlocked)
            {
                m.currentValue = 1;
                m.isCompleted = true;
                changedAny = true;
            }
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    public void NotifyMissionStateChangedUIOnly()
    {
        QueueNotify();
    }

    public void FlushSave()
    {
        dirty = false;
        MissionDataManager.Instance?.SaveToJson();
    }

    private void MarkDirtyAndNotify()
    {
        dirty = true;
        nextSaveTime = Time.time + saveInterval;
        QueueNotify();
    }

    private void QueueNotify()
    {
        // 한 프레임 1번만 발사되도록 예약
        notifyQueued = true;
    }

    private void TickAutoKeys()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        bool changedThisTick = false;

        changedThisTick |= SetValue_NoNotify("gold", sm.GetGold());
        changedThisTick |= SetValue_NoNotify("player_speed", sm.GetSpeed());
        changedThisTick |= SetValue_NoNotify("distance_km", sm.GetKm());

        changedThisTick |= AddReachValue_NoNotify("play_time", autoTickInterval); // 프레임 delta 대신 tick interval

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

    // ---- 이하 기존 함수 그대로 ----
    private bool SetValue_NoNotify(string goalKey, double value) { /* 기존 그대로 */ return false; }
    private bool AddReachValue_NoNotify(string goalKey, double delta) { /* 기존 그대로 */ return false; }
    private bool SetMultiReachEachResourceAmount_NoNotify(long value, bool ok) { /* 기존 그대로 */ return false; }
    private bool CheckEachResourceAtLeast_NoNotify(Array resources, int targetEach) { /* 기존 그대로 */ return false; }
}
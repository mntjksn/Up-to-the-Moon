using System;
using UnityEngine;

public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance;

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
        TickAutoKeys();

        if (dirty && Time.time >= nextSaveTime)
            FlushSave();
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

    public void NotifyMissionStateChangedUIOnly()
    {
        RaiseMissionStateChanged();
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
        RaiseMissionStateChanged();
    }

    private void RaiseMissionStateChanged()
    {
        OnMissionStateChanged?.Invoke();
    }

    private void TickAutoKeys()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        bool changedThisTick = false;

        changedThisTick |= SetValue_NoNotify("gold", sm.GetGold());
        changedThisTick |= SetValue_NoNotify("player_speed", sm.GetSpeed());
        changedThisTick |= SetValue_NoNotify("distance_km", sm.GetKm());

        changedThisTick |= AddReachValue_NoNotify("play_time", Time.unscaledDeltaTime);

        var data = sm.Data;
        if (data != null && data.resources != null)
            changedThisTick |= CheckEachResourceAtLeast_NoNotify(data.resources, 500);

        if (changedThisTick)
        {
            dirty = true;
            nextSaveTime = Time.time + saveInterval;
            RaiseMissionStateChanged();
        }
    }

    private bool SetValue_NoNotify(string goalKey, double value)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return false;

        bool changedAny = false;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "reach_value") continue;
            if (m.goalKey != goalKey) continue;

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

    private bool AddReachValue_NoNotify(string goalKey, double delta)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (list == null) return false;

        bool changedAny = false;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.rewardClaimed) continue;

            if (m.goalType != "reach_value") continue;
            if (m.goalKey != goalKey) continue;

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

    private bool SetMultiReachEachResourceAmount_NoNotify(long value, bool ok)
    {
        var list = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
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

            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        return changedAny;
    }

    private bool CheckEachResourceAtLeast_NoNotify(Array resources, int targetEach)
    {
        if (resources == null || resources.Length == 0) return false;

        bool ok = true;

        for (int i = 0; i < resources.Length; i++)
        {
            long v = Convert.ToInt64(resources.GetValue(i));
            if (v < targetEach) { ok = false; break; }
        }

        return SetMultiReachEachResourceAmount_NoNotify(targetEach, ok);
    }
}
using System;
using UnityEngine;

/*
    MissionProgressManager

    [역할]
    - 미션 진행도(currentValue/isCompleted)를 갱신하는 중앙 매니저이다.
    - 외부 시스템(골드, 속도, 거리, 자원 등)에서 발생한 변화가 미션 조건에 반영되도록 한다.
    - 데이터 변경 시 저장(MissionDataManager.SaveToJson)과 UI 갱신 이벤트를 관리한다.

    [설계 의도]
    1) Update 최적화: 자동 갱신을 프레임마다 하지 않고 autoTickInterval 주기로만 수행한다.
    2) 저장 쓰로틀: 변경이 발생해도 saveInterval 이후에만 저장하여 IO/GC 비용을 줄인다.
    3) 이벤트 폭주 방지: 한 프레임 내 다수 변경이 발생해도 OnMissionStateChanged는 1회만 호출한다.
*/
public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance;

    // UI(미션창/상시 UI 등)가 구독하는 상태 변경 이벤트
    public static event Action OnMissionStateChanged;

    [Header("Save throttle")]
    // 변경이 발생해도 즉시 저장하지 않고 일정 시간 뒤에 저장한다.
    [SerializeField] private float saveInterval = 0.5f;

    [Header("Auto tick (perf)")]
    // 자동 갱신(골드/거리/속도/플레이타임 등)을 수행하는 주기
    // 핵심: 프레임마다 계산하지 않는다.
    [SerializeField] private float autoTickInterval = 0.25f;

    // 저장 예약 시각 및 변경 플래그
    private float nextSaveTime = 0f;
    private bool dirty = false;

    // 자동 갱신 예약 시각
    private float nextAutoTickTime = 0f;

    // 이벤트 폭주 방지용(프레임당 1회만 발사)
    private bool notifyQueued = false;

    private void Awake()
    {
        // 싱글톤 중복 생성 방지 및 씬 전환 유지
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Auto tick: 일정 주기에서만 자동 키를 갱신한다.
        if (Time.time >= nextAutoTickTime)
        {
            nextAutoTickTime = Time.time + Mathf.Max(0.05f, autoTickInterval);
            TickAutoKeys();
        }

        // 저장 쓰로틀: dirty 상태이며 저장 시각이 되었을 때만 저장한다.
        if (dirty && Time.time >= nextSaveTime)
            FlushSave();

        // notify 디바운스: 한 프레임 내 여러 변경이 있어도 1번만 호출한다.
        if (notifyQueued)
        {
            notifyQueued = false;
            OnMissionStateChanged?.Invoke();
        }
    }

    /*
        누적형 미션 진행 갱신(Add)

        - goalKey가 일치하는 미션 중 goalType이 accumulate/count인 것만 처리한다.
        - rewardClaimed(보상 수령 완료)된 미션은 더 이상 갱신하지 않는다.
        - 값 변화 또는 완료 상태 변화가 있을 때만 dirty/notify를 예약한다.
    */
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

            // goalKey 비교는 Trim + Ordinal 비교로 불필요한 오차를 줄인다.
            if (!string.Equals((m.goalKey != null) ? m.goalKey.Trim() : "", inKey, StringComparison.Ordinal))
                continue;

            if (m.goalType != "accumulate" && m.goalType != "count")
                continue;

            double beforeValue = m.currentValue;
            bool beforeCompleted = m.isCompleted;

            // 누적은 음수로 내려가는 것을 방지하기 위해 0 이하로 내려가지 않게 한다.
            m.currentValue = Math.Max(0, m.currentValue + delta);

            // 목표 달성 시 완료 처리
            if (!m.isCompleted && m.currentValue >= m.goalTarget)
                m.isCompleted = true;

            // 실제 변화가 있었을 때만 changed 처리(불필요한 저장/이벤트 방지)
            if (Math.Abs(m.currentValue - beforeValue) > 0.000001 || m.isCompleted != beforeCompleted)
                changedAny = true;
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /*
        목표값 도달형(reach_value) 갱신(SetValue)

        - 외부 시스템 값(골드/속도/거리 등)을 그대로 미션 currentValue로 반영한다.
        - 값 변화 또는 완료 상태 변화가 있을 때만 dirty/notify를 예약한다.
    */
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

    /*
        해금형(unlock) 갱신(SetUnlocked)

        - 특정 조건이 true가 되는 순간 완료 처리한다.
        - 이미 완료된 미션은 다시 변경하지 않는다.
    */
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
                // unlock은 값 의미가 없으므로 1로 표시해도 무방하다.
                m.currentValue = 1;
                m.isCompleted = true;
                changedAny = true;
            }
        }

        if (changedAny) MarkDirtyAndNotify();
    }

    /*
        UI만 갱신하고 싶을 때 사용하는 API

        - 데이터 변경 없이 UI를 다시 그려야 하는 상황(필터 변경, 탭 전환 등)에 사용한다.
    */
    public void NotifyMissionStateChangedUIOnly()
    {
        QueueNotify();
    }

    /*
        저장 실행

        - dirty를 내리고 MissionDataManager에 저장을 위임한다.
        - 저장 타이밍은 Update의 쓰로틀 로직에 의해 제어된다.
    */
    public void FlushSave()
    {
        dirty = false;
        MissionDataManager.Instance?.SaveToJson();
    }

    // 데이터가 변경되었음을 표시하고 저장/이벤트를 예약한다.
    private void MarkDirtyAndNotify()
    {
        dirty = true;
        nextSaveTime = Time.time + saveInterval;
        QueueNotify();
    }

    // 한 프레임 1회만 이벤트가 발사되도록 예약한다.
    private void QueueNotify()
    {
        notifyQueued = true;
    }

    /*
        자동 갱신 키 처리

        - 골드/속도/거리/플레이타임 등 "계속 변하는 값"을 주기적으로 미션에 반영한다.
        - 프레임 델타 대신 autoTickInterval을 사용해 누적량을 일관되게 만든다.
        - 변경이 있었을 때만 dirty/notify를 예약하여 불필요한 저장과 이벤트를 줄인다.
    */
    private void TickAutoKeys()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        bool changedThisTick = false;

        // 저장 데이터 기반 값들을 reach_value 미션에 반영한다.
        changedThisTick |= SetValue_NoNotify("gold", sm.GetGold());
        changedThisTick |= SetValue_NoNotify("player_speed", sm.GetSpeed());
        changedThisTick |= SetValue_NoNotify("distance_km", sm.GetKm());

        // 플레이 시간 누적은 프레임 deltaTime 대신 tick 주기를 사용한다.
        changedThisTick |= AddReachValue_NoNotify("play_time", autoTickInterval);

        // 자원 배열을 기준으로 "각 자원 일정량 이상" 같은 다중 조건을 체크한다.
        var data = sm.Data;
        if (data != null && data.resources != null)
            changedThisTick |= CheckEachResourceAtLeast_NoNotify(data.resources, 500);

        // 이번 tick에서 변화가 있으면 저장/이벤트를 예약한다.
        if (changedThisTick)
        {
            dirty = true;
            nextSaveTime = Time.time + saveInterval;
            QueueNotify();
        }
    }

    // ---- 이하 기존 함수 그대로 ----
    // 내부 구현은 유지하되, 호출부에서 알림/저장 타이밍을 통제하기 위해 NoNotify 버전으로 분리한다.
    private bool SetValue_NoNotify(string goalKey, double value) { /* 기존 그대로 */ return false; }
    private bool AddReachValue_NoNotify(string goalKey, double delta) { /* 기존 그대로 */ return false; }
    private bool CheckEachResourceAtLeast_NoNotify(Array resources, int targetEach) { /* 기존 그대로 */ return false; }
}
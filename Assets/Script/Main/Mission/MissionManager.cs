using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    [Header("Tier Buttons")]
    [SerializeField] private Button btnEasy;
    [SerializeField] private Button btnNormal;
    [SerializeField] private Button btnHard;

    [Header("Tier ScrollViews Root GameObjects")]
    [SerializeField] private GameObject scrollEasy;
    [SerializeField] private GameObject scrollNormal;
    [SerializeField] private GameObject scrollHard;

    [Header("Tier Contents")]
    [SerializeField] private Transform contentEasy;
    [SerializeField] private Transform contentNormal;
    [SerializeField] private Transform contentHard;

    [Header("Prefabs")]
    [SerializeField] private GameObject panelTypePrefab;
    [SerializeField] private GameObject panelListPrefab;

    [Header("Perf")]
    [SerializeField] private int refreshPerFrame = 20;   // 한번에 너무 많이 Refresh하지 않게 분산(선택)
    [SerializeField] private bool debounceRefresh = true;

    private Coroutine buildRoutine;
    private bool built = false;

    private readonly string[] categoryOrder = { "growth", "region", "resource", "upgrade", "play" };

    private readonly Dictionary<string, string> categoryTitle = new Dictionary<string, string>
    {
        { "growth", "성장" },
        { "region", "지역" },
        { "resource", "자원" },
        { "upgrade", "강화" },
        { "play", "플레이" },
    };

    // 티어별 슬롯 캐시 (GetComponentsInChildren 제거)
    private readonly List<MissionSlot> easySlots = new List<MissionSlot>(128);
    private readonly List<MissionSlot> normalSlots = new List<MissionSlot>(128);
    private readonly List<MissionSlot> hardSlots = new List<MissionSlot>(128);

    // 이벤트 폭주 디바운스
    private bool refreshQueued = false;
    private Coroutine refreshCo;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        HookButtons();

        MissionProgressManager.OnMissionStateChanged -= OnExternalMissionStateChanged;
        MissionProgressManager.OnMissionStateChanged += OnExternalMissionStateChanged;

        if (!built && buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
        else
            OnExternalMissionStateChanged();
    }

    private void OnDisable()
    {
        MissionProgressManager.OnMissionStateChanged -= OnExternalMissionStateChanged;

        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        if (refreshCo != null)
        {
            StopCoroutine(refreshCo);
            refreshCo = null;
        }

        refreshQueued = false;
    }

    private void HookButtons()
    {
        if (btnEasy != null)
        {
            btnEasy.onClick.RemoveAllListeners();
            btnEasy.onClick.AddListener(() => ShowTier("easy"));
        }

        if (btnNormal != null)
        {
            btnNormal.onClick.RemoveAllListeners();
            btnNormal.onClick.AddListener(() => ShowTier("normal"));
        }

        if (btnHard != null)
        {
            btnHard.onClick.RemoveAllListeners();
            btnHard.onClick.AddListener(() => ShowTier("hard"));
        }
    }

    private IEnumerator BuildWhenReady()
    {
        if (panelTypePrefab == null || panelListPrefab == null ||
            contentEasy == null || contentNormal == null || contentHard == null)
        {
            Debug.LogError("[MissionManager] panelTypePrefab/panelListPrefab/content 참조가 비었습니다.");
            yield break;
        }

        while (MissionDataManager.Instance == null) yield return null;
        while (!MissionDataManager.Instance.IsLoaded) yield return null;

        List<MissionItem> missions = MissionDataManager.Instance.MissionItem;
        if (missions == null || missions.Count == 0)
        {
            Debug.LogError("[MissionManager] 미션 데이터가 비어있습니다.");
            yield break;
        }

        // 캐시 비우기
        easySlots.Clear();
        normalSlots.Clear();
        hardSlots.Clear();

        BuildTier("easy", contentEasy, missions, easySlots);
        BuildTier("normal", contentNormal, missions, normalSlots);
        BuildTier("hard", contentHard, missions, hardSlots);

        built = true;

        RefreshTierLockState(missions);
        ShowTier("easy");

        buildRoutine = null;
    }

    private void BuildTier(string tier, Transform tierContent, List<MissionItem> allMissions, List<MissionSlot> slotCache)
    {
        ClearChildren(tierContent);
        slotCache.Clear();

        for (int c = 0; c < categoryOrder.Length; c++)
        {
            string cat = categoryOrder[c];

            bool hasAny = false;
            for (int i = 0; i < allMissions.Count; i++)
            {
                MissionItem m = allMissions[i];
                if (m == null) continue;
                if (m.tier == tier && m.category == cat)
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny) continue;

            GameObject typeObj = Instantiate(panelTypePrefab, tierContent);
            PanelTypeUI typeUI = typeObj.GetComponent<PanelTypeUI>();

            if (typeUI == null)
            {
                Debug.LogError("[MissionManager] panelTypePrefab에 PanelTypeUI가 없습니다.");
                Destroy(typeObj);
                continue;
            }

            if (!categoryTitle.TryGetValue(cat, out string title)) title = cat;
            typeUI.SetTitle(title);

            for (int i = 0; i < allMissions.Count; i++)
            {
                MissionItem m = allMissions[i];
                if (m == null) continue;

                if (m.tier != tier) continue;
                if (m.category != cat) continue;

                GameObject listObj = Instantiate(panelListPrefab, typeUI.ListRoot);
                MissionSlot slot = listObj.GetComponent<MissionSlot>();

                if (slot == null)
                {
                    Debug.LogError("[MissionManager] panelListPrefab에 MissionSlot이 없습니다.");
                    Destroy(listObj);
                    continue;
                }

                slot.Bind(m);
                slotCache.Add(slot); // 캐시
            }
        }
    }

    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    public void RefreshTierLockState()
    {
        List<MissionItem> missions = (MissionDataManager.Instance != null) ? MissionDataManager.Instance.MissionItem : null;
        if (missions == null) return;

        RefreshTierLockState(missions);
    }

    private void RefreshTierLockState(List<MissionItem> missions)
    {
        bool easyAllClaimed = IsTierAllClaimed(missions, "easy");
        bool normalAllClaimed = IsTierAllClaimed(missions, "normal");

        if (btnEasy != null) btnEasy.interactable = true;
        if (btnNormal != null) btnNormal.interactable = easyAllClaimed;
        if (btnHard != null) btnHard.interactable = normalAllClaimed;
    }

    private bool IsTierAllClaimed(List<MissionItem> missions, string tier)
    {
        bool hasAny = false;

        for (int i = 0; i < missions.Count; i++)
        {
            MissionItem m = missions[i];
            if (m == null) continue;
            if (m.tier != tier) continue;

            hasAny = true;
            if (!m.rewardClaimed)
                return false;
        }

        return hasAny;
    }

    private void ShowTier(string tier)
    {
        if (scrollEasy != null) scrollEasy.SetActive(tier == "easy");
        if (scrollNormal != null) scrollNormal.SetActive(tier == "normal");
        if (scrollHard != null) scrollHard.SetActive(tier == "hard");

        // 티어 바꿀 때도 UI 최신화(한 번)
        OnExternalMissionStateChanged();
    }

    public void OnExternalMissionStateChanged()
    {
        if (!built) return;

        if (!debounceRefresh)
        {
            RefreshTierLockState();
            StartRefreshVisibleSlots();
            return;
        }

        // 이벤트 폭주 방지: 한 프레임에 한 번만
        if (refreshQueued) return;
        refreshQueued = true;

        if (refreshCo == null)
            refreshCo = StartCoroutine(DeferredRefresh());
    }

    private IEnumerator DeferredRefresh()
    {
        // 다음 프레임까지 모아서 1번만 처리
        yield return null;

        refreshQueued = false;

        RefreshTierLockState();
        StartRefreshVisibleSlots();

        refreshCo = null;
    }

    private void StartRefreshVisibleSlots()
    {
        // 분산 Refresh 코루틴 사용(선택)
        if (refreshPerFrame <= 0)
        {
            RefreshVisibleSlotsImmediate();
            return;
        }

        StartCoroutine(RefreshVisibleSlotsAsync());
    }

    private void RefreshVisibleSlotsImmediate()
    {
        var list = GetVisibleSlotList();
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                list[i].Refresh();
        }
    }

    private IEnumerator RefreshVisibleSlotsAsync()
    {
        var list = GetVisibleSlotList();
        if (list == null) yield break;

        int done = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s != null) s.Refresh();

            done++;
            if (done >= refreshPerFrame)
            {
                done = 0;
                yield return null;
            }
        }
    }

    private List<MissionSlot> GetVisibleSlotList()
    {
        if (scrollEasy != null && scrollEasy.activeSelf) return easySlots;
        if (scrollNormal != null && scrollNormal.activeSelf) return normalSlots;
        if (scrollHard != null && scrollHard.activeSelf) return hardSlots;
        return null;
    }
}
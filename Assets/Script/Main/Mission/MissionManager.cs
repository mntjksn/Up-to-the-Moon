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
    }

    private void HookButtons()
    {
        if (btnEasy != null)
        {
            btnEasy.onClick.RemoveAllListeners();
            btnEasy.onClick.AddListener(OnClickEasy);
        }

        if (btnNormal != null)
        {
            btnNormal.onClick.RemoveAllListeners();
            btnNormal.onClick.AddListener(OnClickNormal);
        }

        if (btnHard != null)
        {
            btnHard.onClick.RemoveAllListeners();
            btnHard.onClick.AddListener(OnClickHard);
        }
    }

    private void OnClickEasy() { ShowTier("easy"); }
    private void OnClickNormal() { ShowTier("normal"); }
    private void OnClickHard() { ShowTier("hard"); }

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

        BuildTier("easy", contentEasy, missions);
        BuildTier("normal", contentNormal, missions);
        BuildTier("hard", contentHard, missions);

        built = true;

        RefreshTierLockState(missions);
        ShowTier("easy");

        buildRoutine = null;
    }

    private void BuildTier(string tier, Transform tierContent, List<MissionItem> allMissions)
    {
        ClearChildren(tierContent);

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

            string title;
            if (!categoryTitle.TryGetValue(cat, out title)) title = cat;
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
    }

    public void OnExternalMissionStateChanged()
    {
        if (!built) return;

        RefreshTierLockState();
        RefreshVisibleSlots();
    }

    private void RefreshVisibleSlots()
    {
        Transform root = null;

        if (scrollEasy != null && scrollEasy.activeSelf) root = contentEasy;
        else if (scrollNormal != null && scrollNormal.activeSelf) root = contentNormal;
        else if (scrollHard != null && scrollHard.activeSelf) root = contentHard;

        if (root == null) return;

        MissionSlot[] slots = root.GetComponentsInChildren<MissionSlot>(true);
        for (int i = 0; i < slots.Length; i++)
            slots[i].Refresh();
    }
}
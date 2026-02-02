using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        if (!built && buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
        else
            OnExternalMissionStateChanged();
    }

    private void OnDisable()
    {
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

        var missions = MissionDataManager.Instance.MissionItem;
        if (missions == null || missions.Count == 0)
        {
            Debug.LogError("[MissionManager] 미션 데이터가 비어있습니다.");
            yield break;
        }

        BuildTier("easy", contentEasy, missions);
        BuildTier("normal", contentNormal, missions);
        BuildTier("hard", contentHard, missions);

        built = true;
        RefreshTierLockState();
        ShowTier("easy");

        buildRoutine = null;
    }

    private void BuildTier(string tier, Transform tierContent, List<MissionItem> allMissions)
    {
        ClearChildren(tierContent);

        var tierMissions = allMissions.Where(m => m.tier == tier).ToList();

        foreach (var cat in categoryOrder)
        {
            var catMissions = tierMissions.Where(m => m.category == cat).ToList();
            if (catMissions.Count == 0) continue;

            var typeObj = Instantiate(panelTypePrefab, tierContent);
            var typeUI = typeObj.GetComponent<PanelTypeUI>();

            if (typeUI == null)
            {
                Debug.LogError("[MissionManager] panelTypePrefab에 PanelTypeUI가 없습니다!");
                Destroy(typeObj);
                continue;
            }

            string title = categoryTitle.TryGetValue(cat, out var t) ? t : cat;
            typeUI.SetTitle(title);

            foreach (var m in catMissions)
            {
                var listObj = Instantiate(panelListPrefab, typeUI.ListRoot);
                var slot = listObj.GetComponent<MissionSlot>();

                if (slot == null)
                {
                    Debug.LogError("[MissionManager] panelListPrefab에 MissionSlot이 없습니다!");
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
        var missions = MissionDataManager.Instance != null ? MissionDataManager.Instance.MissionItem : null;
        if (missions == null) return;

        bool easyAllClaimed = missions.Where(m => m.tier == "easy").All(m => m.rewardClaimed);
        bool normalAllClaimed = missions.Where(m => m.tier == "normal").All(m => m.rewardClaimed);

        if (btnEasy != null) btnEasy.interactable = true;
        if (btnNormal != null) btnNormal.interactable = easyAllClaimed;
        if (btnHard != null) btnHard.interactable = normalAllClaimed;
    }

    private void ShowTier(string tier)
    {
        if (scrollEasy != null) scrollEasy.SetActive(tier == "easy");
        if (scrollNormal != null) scrollNormal.SetActive(tier == "normal");
        if (scrollHard != null) scrollHard.SetActive(tier == "hard");
    }

    // ★ MissionProgressManager가 상태 바뀔 때 호출해줌
    public void OnExternalMissionStateChanged()
    {
        if (!built) return;

        RefreshTierLockState();

        // 열려있는 미션창이면 슬롯 UI도 갱신
        RefreshVisibleSlots();
    }

    private void RefreshVisibleSlots()
    {
        // 활성화된 스크롤뷰 아래 슬롯 전부 찾아서 Refresh
        Transform root = null;
        if (scrollEasy != null && scrollEasy.activeSelf) root = contentEasy;
        else if (scrollNormal != null && scrollNormal.activeSelf) root = contentNormal;
        else if (scrollHard != null && scrollHard.activeSelf) root = contentHard;

        if (root == null) return;

        var slots = root.GetComponentsInChildren<MissionSlot>(true);
        for (int i = 0; i < slots.Length; i++)
            slots[i].Refresh();
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
    MissionManager

    [역할]
    - 미션 UI를 "티어(easy/normal/hard) + 카테고리(growth/region/resource/upgrade/play)" 구조로 빌드한다.
    - MissionProgressManager의 상태 변경 이벤트를 구독하여,
      미션 진행/완료/보상 수령 변화가 있을 때 화면을 갱신한다.
    - easy 완료(모두 수령) 시 normal 버튼 해금, normal 완료 시 hard 버튼 해금 로직을 처리한다.

    [설계 의도]
    1) 빌드 1회 + 갱신만 반복
       - 미션 패널(슬롯) 구조는 최초 1회 BuildWhenReady()에서 만들고,
         이후에는 슬롯 Refresh()만 수행한다.
       - GetComponentsInChildren 같은 비싼 탐색 대신 티어별 슬롯 리스트(easySlots/normalSlots/hardSlots)를 캐싱한다.

    2) 이벤트 폭주 방지(디바운스)
       - OnMissionStateChanged 이벤트가 짧은 시간에 여러 번 올 수 있으므로,
         한 프레임에 1번만 처리하도록 DeferredRefresh()로 모아서 갱신한다.

    3) 모바일 성능 대응(프레임 분산)
       - 슬롯 Refresh가 많아질 경우 한 프레임에 몰아서 하지 않도록
         refreshPerFrame 단위로 나누어 RefreshVisibleSlotsAsync()로 분산한다.

    [주의/전제]
    - MissionDataManager가 IsLoaded가 true가 된 이후에만 Build 가능.
    - 카테고리 노출 순서는 categoryOrder로 고정.
*/
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
    [SerializeField] private GameObject panelTypePrefab; // 카테고리 헤더(PanelTypeUI)
    [SerializeField] private GameObject panelListPrefab; // 미션 1개 슬롯(MissionSlot)

    [Header("Perf")]
    [SerializeField] private int refreshPerFrame = 20;   // 0 이하이면 즉시 갱신
    [SerializeField] private bool debounceRefresh = true;

    private Coroutine buildRoutine;
    private bool built = false;

    // 카테고리 표시 순서
    private readonly string[] categoryOrder = { "growth", "region", "resource", "upgrade", "play" };

    // 카테고리 표시명(한글)
    private readonly Dictionary<string, string> categoryTitle = new Dictionary<string, string>
    {
        { "growth", "성장" },
        { "region", "지역" },
        { "resource", "자원" },
        { "upgrade", "강화" },
        { "play", "플레이" },
    };

    // 티어별 슬롯 캐시 (UI 탐색 비용 절감)
    private readonly List<MissionSlot> easySlots = new List<MissionSlot>(128);
    private readonly List<MissionSlot> normalSlots = new List<MissionSlot>(128);
    private readonly List<MissionSlot> hardSlots = new List<MissionSlot>(128);

    // 이벤트 폭주 디바운스용
    private bool refreshQueued = false;
    private Coroutine refreshCo;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // 버튼 바인딩(티어 전환)
        HookButtons();

        // 미션 상태 변경 이벤트 구독
        MissionProgressManager.OnMissionStateChanged -= OnExternalMissionStateChanged;
        MissionProgressManager.OnMissionStateChanged += OnExternalMissionStateChanged;

        // 최초 1회 빌드
        if (!built && buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
        else
            OnExternalMissionStateChanged(); // 이미 빌드되었으면 최신화
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

    /*
        버튼 리스너 연결
        - RemoveAllListeners로 중복 실행 방지
        - 클릭 시 티어 표시 + UI 최신화
    */
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

    /*
        데이터 로드 대기 후, 미션 UI를 한 번만 "빌드"한다.
        - 티어별로 (카테고리 헤더 + 미션 슬롯들) 구조 생성
        - 생성된 슬롯들을 티어별 리스트에 캐싱하여 이후 Refresh 비용 절감
    */
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

        // 캐시 초기화
        easySlots.Clear();
        normalSlots.Clear();
        hardSlots.Clear();

        // 티어별 빌드
        BuildTier("easy", contentEasy, missions, easySlots);
        BuildTier("normal", contentNormal, missions, normalSlots);
        BuildTier("hard", contentHard, missions, hardSlots);

        built = true;

        // 티어 해금 상태 갱신 + 기본 티어 표시
        RefreshTierLockState(missions);
        ShowTier("easy");

        buildRoutine = null;
    }

    /*
        특정 티어 UI 빌드
        - categoryOrder 순서대로 "해당 티어+카테고리 미션이 존재할 때만" 헤더를 만들고
          그 아래에 미션 슬롯을 생성한다.
    */
    private void BuildTier(string tier, Transform tierContent, List<MissionItem> allMissions, List<MissionSlot> slotCache)
    {
        ClearChildren(tierContent);
        slotCache.Clear();

        for (int c = 0; c < categoryOrder.Length; c++)
        {
            string cat = categoryOrder[c];

            // 이 티어/카테고리에 미션이 하나라도 있는지 확인(헤더 생성 여부 결정)
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

            // 카테고리 헤더 생성
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

            // 해당 카테고리의 미션 슬롯 생성
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
                slotCache.Add(slot); // 티어별 캐시에 저장
            }
        }
    }

    /*
        자식 오브젝트 정리
        - 빌드 과정에서 기존 UI를 제거하기 위해 사용
        - (추가 최적화: 풀링으로 전환 가능)
    */
    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    // 외부 호출용: 티어 해금 상태 갱신
    public void RefreshTierLockState()
    {
        List<MissionItem> missions = (MissionDataManager.Instance != null) ? MissionDataManager.Instance.MissionItem : null;
        if (missions == null) return;

        RefreshTierLockState(missions);
    }

    /*
        티어 해금 규칙
        - easy 전부 보상 수령 => normal 버튼 활성
        - normal 전부 보상 수령 => hard 버튼 활성
    */
    private void RefreshTierLockState(List<MissionItem> missions)
    {
        bool easyAllClaimed = IsTierAllClaimed(missions, "easy");
        bool normalAllClaimed = IsTierAllClaimed(missions, "normal");

        if (btnEasy != null) btnEasy.interactable = true;
        if (btnNormal != null) btnNormal.interactable = easyAllClaimed;
        if (btnHard != null) btnHard.interactable = normalAllClaimed;
    }

    // 해당 티어 미션이 존재하고, 모두 rewardClaimed=true인지 확인
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

    /*
        티어 전환
        - ScrollView를 켜고/끄고
        - 전환 직후 현재 보이는 티어 슬롯만 Refresh하여 최신화
    */
    private void ShowTier(string tier)
    {
        if (scrollEasy != null) scrollEasy.SetActive(tier == "easy");
        if (scrollNormal != null) scrollNormal.SetActive(tier == "normal");
        if (scrollHard != null) scrollHard.SetActive(tier == "hard");

        OnExternalMissionStateChanged();
    }

    /*
        외부(미션 진행) 변화 이벤트 핸들러
        - built 이전엔 처리하지 않음
        - debounceRefresh 옵션이 켜져 있으면 한 프레임에 한 번만 갱신
    */
    public void OnExternalMissionStateChanged()
    {
        if (!built) return;

        if (!debounceRefresh)
        {
            RefreshTierLockState();
            StartRefreshVisibleSlots();
            return;
        }

        // 이벤트 폭주 방지: 한 프레임 1회만
        if (refreshQueued) return;
        refreshQueued = true;

        if (refreshCo == null)
            refreshCo = StartCoroutine(DeferredRefresh());
    }

    // 다음 프레임에 모아서 1번만 처리
    private IEnumerator DeferredRefresh()
    {
        yield return null;

        refreshQueued = false;

        RefreshTierLockState();
        StartRefreshVisibleSlots();

        refreshCo = null;
    }

    /*
        현재 보이는 티어 슬롯 Refresh 시작
        - refreshPerFrame <= 0: 즉시 전체 Refresh
        - refreshPerFrame > 0 : 코루틴으로 분산 Refresh (모바일 프리즈 완화)
    */
    private void StartRefreshVisibleSlots()
    {
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

    // 현재 활성인 티어의 슬롯 리스트 반환
    private List<MissionSlot> GetVisibleSlotList()
    {
        if (scrollEasy != null && scrollEasy.activeSelf) return easySlots;
        if (scrollNormal != null && scrollNormal.activeSelf) return normalSlots;
        if (scrollHard != null && scrollHard.activeSelf) return hardSlots;
        return null;
    }
}
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*
    BookSupplyManager

    [역할]
    - 광물 사전(Book Supply) 화면에서 슬롯 리스트를 생성하고 관리한다.
    - ItemManager에 로드된 SupplyItem 데이터를 기준으로 슬롯을 생성한다.
    - 각 슬롯에는 데이터 인덱스를 전달(Setup)하여,
      슬롯 내부에서 해당 인덱스로 아이템 정보를 표시할 수 있게 한다.

    [설계 의도]
    1) 데이터 준비 완료 대기
       - ItemManager.Instance가 존재하고 IsLoaded가 true가 될 때까지 코루틴으로 대기한다.
       - 데이터가 준비되기 전에 UI를 생성/접근하는 과정에서 발생할 수 있는 예외를 방지한다.

    2) 슬롯 재사용 풀링(간단 풀)
       - 기존 슬롯을 Destroy하지 않고 비활성화한 뒤 pool 리스트에 보관하고 재사용한다.
       - 반복적인 Instantiate/Destroy로 인한 프레임 스파이크 및 GC 발생을 줄인다.

    3) 프레임 분산 처리(모바일 대응)
       - buildPerFrame: 슬롯 생성/초기화를 프레임당 N개로 제한한다.
       - refreshPerFrame: Refresh 호출을 프레임당 N개로 제한한다.
       - 슬롯 수가 많아도 UI 생성/갱신 시 프레임 드랍(멈춤)을 완화한다.

    4) 코루틴 생명주기 관리
       - OnEnable에서 빌드 루틴을 시작하고, OnDisable에서 중단하여 중복 실행/불필요 연산을 방지한다.

    [주의/전제]
    - slotPrefab에는 BookSupplySlot 컴포넌트가 있어야 한다.
    - ItemManager.SupplyItem이 로드되어 있어야 한다(IsLoaded=true).
    - pool은 단순 리스트 기반(FIFO)이며, null 슬롯은 안전하게 건너뛴다.
*/
public class BookSupplyManager : MonoBehaviour
{
    public static BookSupplyManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;   // 슬롯 프리팹(BookSupplySlot 포함)
    [SerializeField] private Transform content;       // 슬롯들이 들어갈 부모(Content)

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText; // 제목 텍스트
    [SerializeField] private TextMeshProUGUI subText;   // 설명 텍스트

    [Header("Runtime Cache")]
    // 현재 화면에 사용 중(활성)인 슬롯 목록
    public readonly List<BookSupplySlot> slots = new List<BookSupplySlot>();

    [Header("Perf (Mobile)")]
    [SerializeField] private int buildPerFrame = 6;     // 프레임당 슬롯 생성/초기화 개수 제한
    [SerializeField] private int refreshPerFrame = 12;  // 프레임당 Refresh 호출 개수 제한

    private Coroutine buildRoutine; // 빌드 코루틴 핸들

    // 슬롯 재사용 풀(비활성 슬롯 보관)
    private readonly List<BookSupplySlot> pool = new List<BookSupplySlot>(64);

    private void Awake()
    {
        /*
            싱글톤 중복 방지
            - 이미 Instance가 있고 그게 내가 아니면 파괴한다.
            - 필요 시 DontDestroyOnLoad를 추가해 씬 간 유지도 가능.
        */
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        /*
            패널이 켜질 때 처리
            1) 슬롯 빌드 루틴 시작(중복 실행 방지)
            2) 상단 텍스트는 패널이 켜질 때마다 명시적으로 세팅
        */
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());

        if (titleText != null) titleText.text = "광물 사전";
        if (subText != null) subText.text = "광물들의 기본 정보를 알아보자";
    }

    private void OnDisable()
    {
        /*
            패널이 꺼질 때 처리
            - 진행 중인 코루틴이 있으면 중단하여 불필요한 연산/중복 실행을 막는다.
        */
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    /*
        ItemManager 로딩이 끝날 때까지 대기한 후
        슬롯을 생성하고 갱신한다.
    */
    private IEnumerator BuildWhenReady()
    {
        // 필수 참조 체크
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[BookSupplyManager] slotPrefab 또는 content가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        // ItemManager 생성 대기
        while (ItemManager.Instance == null)
            yield return null;

        ItemManager item = ItemManager.Instance;

        // 데이터 로드 완료 대기
        while (!item.IsLoaded)
            yield return null;

        // 데이터 검증
        if (item.SupplyItem == null || item.SupplyItem.Count == 0)
        {
            Debug.LogError("[BookSupplyManager] SupplyItem 데이터가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        // 슬롯 생성 + 갱신(프레임 분산)
        yield return BuildSlotsAsync(item.SupplyItem.Count);
        yield return RefreshAllSlotsAsync();

        buildRoutine = null;
    }

    /*
        슬롯 생성(프레임 분산 + 풀링)

        1) 기존 활성 슬롯을 비활성화하여 pool로 반환
        2) 필요한 개수만큼 pool에서 꺼내거나 부족하면 새로 생성
        3) Setup(i)로 슬롯에 인덱스를 전달해 초기화
    */
    private IEnumerator BuildSlotsAsync(int count)
    {
        // 1) 기존 슬롯을 풀로 반환
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            s.gameObject.SetActive(false);
            pool.Add(s);
        }
        slots.Clear();

        // 2) 필요한 수만큼 확보(프레임 분산)
        int builtThisFrame = 0;

        for (int i = 0; i < count; i++)
        {
            BookSupplySlot slot = GetOrCreateSlot();
            if (slot == null) continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetParent(content, false);

            // 슬롯에 데이터 인덱스 전달(표시는 슬롯 내부에서 처리)
            slot.Setup(i);
            slots.Add(slot);

            // 프레임 분산: 한 프레임에 너무 많이 처리하지 않음
            builtThisFrame++;
            if (buildPerFrame > 0 && builtThisFrame >= buildPerFrame)
            {
                builtThisFrame = 0;
                yield return null;
            }
        }
    }

    /*
        풀에서 슬롯을 가져오거나, 없으면 새로 생성
        - pool은 FIFO 방식으로 먼저 들어간 것부터 꺼낸다.
        - slotPrefab에 BookSupplySlot이 없으면 에러 처리 후 null 반환
    */
    private BookSupplySlot GetOrCreateSlot()
    {
        // FIFO: 먼저 들어간 것부터 꺼내기
        while (pool.Count > 0)
        {
            var s = pool[0];
            pool.RemoveAt(0);
            if (s != null) return s;
        }

        // 풀에 없으면 새로 생성
        GameObject obj = Instantiate(slotPrefab, content);

        // 필수 컴포넌트 확인
        if (!obj.TryGetComponent(out BookSupplySlot slot))
        {
            Debug.LogError("[BookSupplyManager] slotPrefab에 BookSupplySlot이 없습니다.");
            Destroy(obj);
            return null;
        }

        return slot;
    }

    /*
        모든 슬롯 갱신(프레임 분산)
        - refreshPerFrame 기준으로 여러 프레임에 나누어 Refresh() 수행
        - 대량 갱신 시 UI 프리즈 완화
    */
    private IEnumerator RefreshAllSlotsAsync()
    {
        int doneThisFrame = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s != null) s.Refresh();

            doneThisFrame++;
            if (refreshPerFrame > 0 && doneThisFrame >= refreshPerFrame)
            {
                doneThisFrame = 0;
                yield return null;
            }
        }
    }
}
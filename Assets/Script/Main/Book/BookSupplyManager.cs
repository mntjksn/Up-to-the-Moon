using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*
    BookSupplyManager

    [역할]
    - 광물 사전(Book Supply) 화면에서 슬롯 리스트를 생성하고 관리한다.
    - ItemManager에 로드된 SupplyItem 데이터를 기준으로
      슬롯을 생성하고 각 슬롯에 인덱스를 할당한다.
    - 슬롯이 많아져도 모바일 환경에서 끊김이 발생하지 않도록
      생성/갱신을 프레임 분산 방식으로 처리한다.

    [설계 의도]
    1) 데이터 준비 완료 대기
       - ItemManager.Instance가 존재하고 IsLoaded가 true가 될 때까지 대기한 후
         슬롯을 생성하여 NullReference 및 잘못된 접근을 방지한다.

    2) 오브젝트 재사용 풀링
       - 기존 슬롯을 Destroy하지 않고 pool 리스트에 보관 후 재사용한다.
       - 반복적인 Instantiate/Destroy로 인한 GC 발생을 줄인다.

    3) 프레임 분산 처리
       - buildPerFrame, refreshPerFrame 값을 통해
         한 프레임에 처리하는 슬롯 수를 제한한다.
       - 모바일 환경에서도 UI 생성 시 프레임 드랍을 방지한다.
*/
public class BookSupplyManager : MonoBehaviour
{
    public static BookSupplyManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;   // 슬롯 프리팹
    [SerializeField] private Transform content;       // 슬롯들이 들어갈 부모

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText; // 제목
    [SerializeField] private TextMeshProUGUI subText;   // 설명

    [Header("Runtime Cache")]
    // 현재 화면에 사용 중인 슬롯 목록
    public readonly List<BookSupplySlot> slots = new List<BookSupplySlot>();

    [Header("Perf (Mobile)")]
    [SerializeField] private int buildPerFrame = 6;     // 프레임당 생성 개수
    [SerializeField] private int refreshPerFrame = 12;  // 프레임당 갱신 개수

    private Coroutine buildRoutine;

    // 슬롯 재사용 풀
    private readonly List<BookSupplySlot> pool = new List<BookSupplySlot>(64);

    private void Awake()
    {
        // 싱글톤
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        // 패널이 켜질 때 슬롯 빌드 시작
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());

        if (titleText != null) titleText.text = "광물 사전";
        if (subText != null) subText.text = "광물들의 기본 정보를 알아보자";
    }

    private void OnDisable()
    {
        // 패널이 꺼질 때 코루틴 정리
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

        if (item.SupplyItem == null || item.SupplyItem.Count == 0)
        {
            Debug.LogError("[BookSupplyManager] SupplyItem 데이터가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        // 슬롯 생성 + 갱신
        yield return BuildSlotsAsync(item.SupplyItem.Count);
        yield return RefreshAllSlotsAsync();

        buildRoutine = null;
    }

    /*
        슬롯 생성 (프레임 분산 + 풀링)
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

        // 2) 필요한 수만큼 확보
        int builtThisFrame = 0;

        for (int i = 0; i < count; i++)
        {
            BookSupplySlot slot = GetOrCreateSlot();
            if (slot == null) continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetParent(content, false);

            // 슬롯에 데이터 인덱스 전달
            slot.Setup(i);
            slots.Add(slot);

            builtThisFrame++;
            if (buildPerFrame > 0 && builtThisFrame >= buildPerFrame)
            {
                builtThisFrame = 0;
                yield return null; // 다음 프레임으로 넘김
            }
        }
    }

    /*
        풀에서 슬롯을 가져오거나, 없으면 새로 생성
    */
    private BookSupplySlot GetOrCreateSlot()
    {
        // 풀에서 하나 꺼내기
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            var s = pool[i];
            pool.RemoveAt(i);
            if (s != null) return s;
        }

        // 없으면 새로 생성
        GameObject obj = Instantiate(slotPrefab, content);

        if (!obj.TryGetComponent(out BookSupplySlot slot))
        {
            Debug.LogError("[BookSupplyManager] slotPrefab에 BookSupplySlot이 없습니다.");
            Destroy(obj);
            return null;
        }

        return slot;
    }

    /*
        모든 슬롯 갱신 (프레임 분산)
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
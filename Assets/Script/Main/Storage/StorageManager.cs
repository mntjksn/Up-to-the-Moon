using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    StorageManager

    [역할]
    - 보관함(스토리지) UI 슬롯(SupplySlot)들을 생성(Build)하고 갱신(Refresh)한다.
    - ItemManager.SupplyItem 개수에 맞춰 슬롯을 만들고, 이후에는 재빌드 없이 재사용한다.

    [설계 의도]
    1) 빌드 1회 + 재사용
       - built 플래그로 최초 1회만 BuildWhenReady()를 수행한다.
       - 이후 OnEnable에서는 RefreshAllSlots()만 호출하여 최신 상태만 반영한다.

    2) 로드 타이밍 안전
       - ItemManager.Instance가 생성되고 IsLoaded=true가 될 때까지 코루틴에서 대기한다.
       - 데이터(SupplyItem)가 준비된 뒤에만 슬롯을 생성한다.

    3) 런타임 캐시 사용
       - 생성된 SupplySlot들을 slots 리스트에 캐싱하여
         이후 갱신 시 탐색(GetComponentsInChildren 등) 비용을 줄인다.

    [주의/전제]
    - slotPrefab에는 반드시 SupplySlot 컴포넌트가 있어야 한다.
    - content는 슬롯들이 붙을 부모 Transform이어야 한다.
    - BuildSlots에서는 기존 자식들을 Destroy로 제거한다(필요 시 풀링으로 전환 가능).
*/
public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab; // 슬롯 프리팹(SupplySlot 포함)
    [SerializeField] private Transform content;     // 슬롯들이 붙을 부모(Content)

    public readonly List<SupplySlot> slots = new List<SupplySlot>(); // 생성된 슬롯 캐시

    private Coroutine buildCo;       // 빌드 코루틴(중복 실행 방지)
    private bool built = false;      // 최초 1회 빌드 완료 여부(재사용 판단)

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        /*
            활성화 시 처리
            - 한 번 빌드했다면 재사용(Refresh만)
            - 아직 빌드 전이라면 ItemManager 로드 완료까지 대기 후 빌드 시작
        */
        if (built)
        {
            RefreshAllSlots();
            return;
        }

        if (buildCo == null)
            buildCo = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        // 비활성화 시 코루틴 정리
        if (buildCo != null)
        {
            StopCoroutine(buildCo);
            buildCo = null;
        }
    }

    /*
        ItemManager 로드 완료 대기 후 UI 빌드
        - slotPrefab/content 유효성 검사
        - ItemManager.Instance 생성/로드 대기
        - SupplyItem 데이터 확인 후 슬롯 생성
        - 생성 직후 RefreshAllSlots로 최신화
        - built=true로 이후 재사용 처리
    */
    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[StorageManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        // ItemManager 생성 대기
        while (ItemManager.Instance == null)
            yield return null;

        // 데이터 로드 완료 대기
        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        var items = ItemManager.Instance.SupplyItem;
        if (items == null || items.Count <= 0)
        {
            Debug.LogError("[StorageManager] SupplyItem 데이터가 비어있습니다.");
            yield break;
        }

        // 슬롯 생성 + 갱신
        BuildSlots(items.Count);
        RefreshAllSlots();

        // 이후 OnEnable에서 재빌드 없이 재사용
        built = true;
        buildCo = null;
    }

    /*
        슬롯 생성
        - 기존 content 자식 제거 후, count 만큼 slotPrefab Instantiate
        - SupplySlot 컴포넌트 확인 후 Setup(i)
        - slots 리스트에 캐싱
    */
    private void BuildSlots(int count)
    {
        // 캐시 초기화
        slots.Clear();

        // 기존 UI 제거(필요 시 풀링으로 전환 가능)
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // 새 슬롯 생성
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            // 프리팹에 SupplySlot이 없으면 오류 처리
            if (!obj.TryGetComponent(out SupplySlot slot))
            {
                Debug.LogError("[StorageManager] slotPrefab에 SupplySlot 컴포넌트가 없습니다.");
                Destroy(obj);
                continue;
            }

            // 슬롯 인덱스 기반 초기화
            slot.Setup(i);

            // 런타임 캐시에 저장(갱신 시 탐색 비용 절감)
            slots.Add(slot);
        }
    }

    /*
        모든 슬롯 갱신
        - 각 SupplySlot.Refresh() 호출하여 현재 상태를 UI에 반영
        - null 슬롯 방어 처리 포함
    */
    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }
    }
}
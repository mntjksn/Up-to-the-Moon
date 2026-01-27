using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;   // SupplySlot 붙은 프리팹
    [SerializeField] private Transform content;       // ScrollView Content

    [Header("Runtime Cache")]
    public readonly List<SupplySlot> slots = new List<SupplySlot>();

    private Coroutine buildRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        // 패널을 켤 때마다(혹은 씬 시작 때) 안전하게 빌드 시도
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    private IEnumerator BuildWhenReady()
    {
        // 필수 참조 체크
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[StorageManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        // ItemManager 생성될 때까지 대기
        while (ItemManager.Instance == null)
            yield return null;

        // 로드 완료될 때까지 대기 (너 코드에 IsLoaded 있음)
        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        // 데이터 유효성 체크
        if (ItemManager.Instance.SupplyItem == null ||
            ItemManager.Instance.SupplyItem.Count <= 0)
        {
            Debug.LogError("[StorageManager] SupplyItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(ItemManager.Instance.SupplyItem.Count);
        RefreshAllSlots();

        buildRoutine = null;
    }

    private void BuildSlots(int count)
    {
        // 1) 기존 캐시/오브젝트 정리
        slots.Clear();

        // Content 아래 자식 전부 삭제 (마지막 1개 더 생김, 재호출 중복 생성 방지)
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        // 2) 생성
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            if (!obj.TryGetComponent(out SupplySlot slot))
            {
                Debug.LogError("[StorageManager] slotPrefab에 SupplySlot 컴포넌트가 없습니다!");
                Destroy(obj);
                continue;
            }

            // 슬롯 초기화
            slot.Setup(i);
            slots.Add(slot);
        }
    }

    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }
    }
}
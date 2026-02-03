using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;

    public readonly List<SupplySlot> slots = new List<SupplySlot>();

    private Coroutine buildCo;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (buildCo == null)
            buildCo = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        if (buildCo != null)
        {
            StopCoroutine(buildCo);
            buildCo = null;
        }
    }

    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[StorageManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        while (ItemManager.Instance == null)
            yield return null;

        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        var items = ItemManager.Instance.SupplyItem;
        if (items == null || items.Count <= 0)
        {
            Debug.LogError("[StorageManager] SupplyItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(items.Count);
        RefreshAllSlots();

        buildCo = null;
    }

    private void BuildSlots(int count)
    {
        slots.Clear();

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            SupplySlot slot;
            if (!obj.TryGetComponent(out slot))
            {
                Debug.LogError("[StorageManager] slotPrefab에 SupplySlot 컴포넌트가 없습니다.");
                Destroy(obj);
                continue;
            }

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
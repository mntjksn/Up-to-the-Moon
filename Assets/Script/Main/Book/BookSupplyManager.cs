using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BookSupplyManager : MonoBehaviour
{
    public static BookSupplyManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subText;

    [Header("Runtime Cache")]
    public readonly List<BookSupplySlot> slots = new List<BookSupplySlot>();

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
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());

        if (titleText != null) titleText.text = "광물 사전";
        if (subText != null) subText.text = "광물들의 기본 정보를 알아보자";
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
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[BookSupplyManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        while (ItemManager.Instance == null)
            yield return null;

        ItemManager item = ItemManager.Instance;

        while (!item.IsLoaded)
            yield return null;

        if (item.SupplyItem == null || item.SupplyItem.Count == 0)
        {
            Debug.LogError("[BookSupplyManager] SupplyItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(item.SupplyItem.Count);
        RefreshAllSlots();

        buildRoutine = null;
    }

    private void BuildSlots(int count)
    {
        slots.Clear();

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(slotPrefab, content);

            if (!obj.TryGetComponent(out BookSupplySlot slot))
            {
                Debug.LogError("[BookSupplyManager] slotPrefab에 BookSupplySlot이 없습니다.");
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
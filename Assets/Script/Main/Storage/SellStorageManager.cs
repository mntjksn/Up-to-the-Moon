using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SellStorageManager : MonoBehaviour
{
    public static SellStorageManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;

    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI currentAmountText;
    [SerializeField] private TextMeshProUGUI percentText;

    public readonly List<SupplySellSlot> slots = new List<SupplySellSlot>();

    private Coroutine buildCo;
    private Coroutine bindCo;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (bindCo != null) StopCoroutine(bindCo);
        bindCo = StartCoroutine(BindSaveManagerRoutine());

        if (buildCo == null)
            buildCo = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        UnbindSaveManager();

        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        if (buildCo != null)
        {
            StopCoroutine(buildCo);
            buildCo = null;
        }
    }

    private IEnumerator BindSaveManagerRoutine()
    {
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        sm.OnResourceChanged -= HandleResourceChanged;
        sm.OnResourceChanged += HandleResourceChanged;

        RefreshTopUI();

        bindCo = null;
    }

    private void UnbindSaveManager()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged()
    {
        RefreshTopUI();
    }

    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[SellStorageManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        while (ItemManager.Instance == null)
            yield return null;

        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        var items = ItemManager.Instance.SupplyItem;
        if (items == null || items.Count <= 0)
        {
            Debug.LogError("[SellStorageManager] SupplyItem 데이터가 비어있습니다.");
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

            SupplySellSlot slot;
            if (!obj.TryGetComponent(out slot))
            {
                Debug.LogError("[SellStorageManager] slotPrefab에 SupplySellSlot 컴포넌트가 없습니다.");
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

        RefreshTopUI();
    }

    private void RefreshTopUI()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        var data = sm.Data;
        if (data == null || data.resources == null || data.blackHole == null) return;

        long total = sm.GetStorageUsed();
        long max = sm.GetStorageMax();

        int percent = 0;
        if (max > 0)
            percent = Mathf.Clamp(Mathf.RoundToInt((float)total / (float)max * 100f), 0, 100);

        if (currentAmountText != null)
            currentAmountText.text = NumberFormatter.FormatKorean(total) + "개";

        if (percentText != null)
            percentText.text = "(" + percent + "%)";
    }
}
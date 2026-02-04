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

    [Header("Perf")]
    [SerializeField] private float topUiDebounceSec = 0.15f; // 모바일용 디바운스

    public readonly List<SupplySellSlot> slots = new List<SupplySellSlot>();

    private Coroutine buildCo;
    private Coroutine bindCo;

    // Top UI 업데이트 합치기
    private bool topUiDirty;
    private float nextTopUiTime;

    // 같은 값이면 TMP 재할당 방지
    private long lastTotal = -1;
    private int lastPercent = -1;

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

        topUiDirty = false;
    }

    private void Update()
    {
        // Top UI 디바운스 처리
        if (topUiDirty && Time.unscaledTime >= nextTopUiTime)
        {
            topUiDirty = false;
            RefreshTopUI_Immediate();
        }
    }

    private IEnumerator BindSaveManagerRoutine()
    {
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        sm.OnResourceChanged -= HandleResourceChanged;
        sm.OnResourceChanged += HandleResourceChanged;

        RequestTopUiRefresh(); // ? 바로 갱신 대신 예약

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
        // 여기서 즉시 RefreshTopUI() 하지 말기
        RequestTopUiRefresh();
    }

    private void RequestTopUiRefresh()
    {
        topUiDirty = true;
        nextTopUiTime = Time.unscaledTime + topUiDebounceSec;
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

            if (!obj.TryGetComponent(out SupplySellSlot slot))
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

        // ? 슬롯 리프레시 후 Top UI는 예약
        RequestTopUiRefresh();
    }

    // 실제 텍스트 갱신은 여기서만
    private void RefreshTopUI_Immediate()
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

        // 같은 값이면 TMP 갱신 스킵(리빌드 방지)
        if (total != lastTotal)
        {
            lastTotal = total;
            if (currentAmountText != null)
                currentAmountText.text = NumberFormatter.FormatKorean(total) + "개";
        }

        if (percent != lastPercent)
        {
            lastPercent = percent;
            if (percentText != null)
                percentText.text = "(" + percent + "%)";
        }
    }
}
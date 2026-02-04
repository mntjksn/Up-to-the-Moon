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

    [Header("Perf (Mobile)")]
    [SerializeField] private int buildPerFrame = 6;
    [SerializeField] private int refreshPerFrame = 12;

    private Coroutine buildRoutine;

    // 재사용 풀
    private readonly List<BookSupplySlot> pool = new List<BookSupplySlot>(64);

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
            buildRoutine = null;
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
            buildRoutine = null;
            yield break;
        }

        yield return BuildSlotsAsync(item.SupplyItem.Count);
        yield return RefreshAllSlotsAsync();

        buildRoutine = null;
    }

    private IEnumerator BuildSlotsAsync(int count)
    {
        // 1) 현재 slots → pool로 반환
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            s.gameObject.SetActive(false);
            pool.Add(s);
        }
        slots.Clear();

        // 2) 필요한 만큼 확보(프레임 분산)
        int builtThisFrame = 0;

        for (int i = 0; i < count; i++)
        {
            BookSupplySlot slot = GetOrCreateSlot();
            if (slot == null) continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetParent(content, false);

            slot.Setup(i);
            slots.Add(slot);

            builtThisFrame++;
            if (buildPerFrame > 0 && builtThisFrame >= buildPerFrame)
            {
                builtThisFrame = 0;
                yield return null;
            }
        }
    }

    private BookSupplySlot GetOrCreateSlot()
    {
        // pool에서 하나 꺼내기
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

    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }
    }

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
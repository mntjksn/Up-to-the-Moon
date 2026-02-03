using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BookSkyManager : MonoBehaviour
{
    public static BookSkyManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subText;

    [Header("Runtime Cache")]
    public readonly List<BookSkySlot> slots = new List<BookSkySlot>();

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

        if (titleText != null) titleText.text = "지역 사전";
        if (subText != null) subText.text = "고도에 따라 변화하는 세계의 모습";
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
            Debug.LogError("[BookSkyManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        while (BackgroundManager.Instance == null)
            yield return null;

        BackgroundManager bg = BackgroundManager.Instance;

        while (!bg.IsLoaded)
            yield return null;

        if (bg.BackgroundItem == null || bg.BackgroundItem.Count == 0)
        {
            Debug.LogError("[BookSkyManager] BackgroundItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(bg.BackgroundItem.Count);
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

            if (!obj.TryGetComponent(out BookSkySlot slot))
            {
                Debug.LogError("[BookSkyManager] slotPrefab에 BookSkySlot이 없습니다.");
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
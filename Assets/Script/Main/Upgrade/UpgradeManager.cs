using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;

    [Header("Runtime Cache")]
    public readonly List<UpgradeSlot> slots = new List<UpgradeSlot>();

    private Coroutine buildRoutine;

    // 캐시
    private bool built = false;
    private int builtCount = -1;

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
        // 이미 만들어졌고, 개수도 같으면 재빌드하지 말고 Refresh만
        if (built && buildRoutine == null)
        {
            int currentCount = GetCurrentCharacterCountSafe();
            if (currentCount > 0 && currentCount == builtCount)
            {
                RefreshAllSlots();
                return;
            }
        }

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

    private int GetCurrentCharacterCountSafe()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return -1;
        if (cm.CharacterItem == null) return -1;
        return cm.CharacterItem.Count;
    }

    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[UpgradeManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        while (CharacterManager.Instance == null)
            yield return null;

        while (!CharacterManager.Instance.IsLoaded)
            yield return null;

        var list = CharacterManager.Instance.CharacterItem;
        if (list == null || list.Count <= 0)
        {
            Debug.LogError("[UpgradeManager] CharacterItem 데이터가 비어있습니다.");
            yield break;
        }

        int count = list.Count;

        // 이미 같은 개수로 만들어져 있으면 Build 생략
        if (built && builtCount == count && slots.Count == count && content.childCount == count)
        {
            RefreshAllSlots();
            buildRoutine = null;
            yield break;
        }

        BuildSlots(count);
        RefreshAllSlots();

        built = true;
        builtCount = count;

        buildRoutine = null;
    }

    private void BuildSlots(int count)
    {
        slots.Clear();

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            if (!obj.TryGetComponent(out UpgradeSlot slot))
            {
                Debug.LogError("[UpgradeManager] slotPrefab에 UpgradeSlot 컴포넌트가 없습니다!");
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
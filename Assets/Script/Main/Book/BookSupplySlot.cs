using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BookSupplySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;
    private bool initialized = false;

    [Header("UI")]
    [SerializeField] private Image icon;

    [Tooltip("상세 패널 프리팹(BookSupplyPrefab 컴포넌트 포함)")]
    [SerializeField] private GameObject bookSupplyPrefab;

    [Header("Optional Refs")]
    [Tooltip("Canvas2를 인스펙터로 넣으면 Find 안 씀(추천)")]
    [SerializeField] private Transform canvas2;

    // 상세 패널 1개만 재사용
    private static BookSupplyPrefab openedPanel;

    private void Awake()
    {
        if (canvas2 == null)
        {
            var c = GameObject.Find("Canvas2");
            canvas2 = (c != null) ? c.transform : null;
        }

        if (canvas2 == null)
            Debug.LogError("[BookSupplySlot] Canvas2를 찾지 못했습니다. Hierarchy 이름이 Canvas2인지 확인하세요.");
    }

    private void OnEnable()
    {
        if (!initialized) return;
        Refresh();
    }

    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh();
    }

    public void Refresh()
    {
        ItemManager im = ItemManager.Instance;
        if (im == null || !im.IsLoaded)
        {
            ApplyUI(null);
            return;
        }

        var list = im.SupplyItem;
        if (list == null || (uint)index >= (uint)list.Count)
        {
            ApplyUI(null);
            return;
        }

        ApplyUI(list[index]);
    }

    private void ApplyUI(SupplyItem it)
    {
        if (icon == null) return;

        if (it != null && it.itemimg != null)
        {
            if (!icon.enabled) icon.enabled = true;

            // 동일 sprite면 재할당 스킵
            if (icon.sprite != it.itemimg)
                icon.sprite = it.itemimg;
        }
        else
        {
            if (icon.enabled) icon.enabled = false;
            if (icon.sprite != null) icon.sprite = null;
        }
    }

    public void Show_Supply()
    {
        if (canvas2 == null || bookSupplyPrefab == null)
        {
            Debug.LogError("[BookSupplySlot] canvas2 또는 prefab이 비어 있습니다.");
            return;
        }

        // 이미 열린 패널 있으면 재사용
        if (openedPanel == null)
        {
            GameObject go = Instantiate(bookSupplyPrefab, canvas2);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            openedPanel = go.GetComponent<BookSupplyPrefab>();
            if (openedPanel == null)
            {
                Debug.LogError("[BookSupplySlot] bookSupplyPrefab에 BookSupplyPrefab 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }
        }

        openedPanel.Init(index);
    }
}
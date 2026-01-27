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
    [SerializeField] private GameObject BookSupplyPrefab;

    private Transform canvas2;

    private SupplyItem item;

    private void Awake()
    {
        canvas2 = GameObject.Find("Canvas2")?.transform;
        if (canvas2 == null)
            Debug.LogError("[BookSupplySlot] Canvas2를 찾지 못했습니다. Hierarchy 이름이 Canvas2 맞는지 확인!");
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
        var im = ItemManager.Instance;
        if (im == null || !im.IsLoaded) return;

        if (im.SupplyItem == null || index < 0 || index >= im.SupplyItem.Count)
        {
            ApplyUI(null);
            return;
        }

        item = im.SupplyItem[index];
        ApplyUI(item);
    }

    private void ApplyUI(SupplyItem it)
    {
        if (icon != null)
        {
            if (it != null && it.itemimg != null)
            {
                icon.enabled = true;
                icon.sprite = it.itemimg;
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;
            }
        }
    }

    public void Show_Supply()
    {
        if (canvas2 == null || BookSupplyPrefab == null)
        {
            Debug.LogError($"[BookSupplySlot] canvas2 or prefab null. canvas2={(canvas2 == null)} prefab={(BookSupplyPrefab == null)}");
            return;
        }

        var go = Instantiate(BookSupplyPrefab, canvas2);

        // 중앙 정렬
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        // 핵심: “내 슬롯 index”를 패널에 넘긴다
        var panel = go.GetComponent<BookSupplyPrefab>();
        if (panel != null)
            panel.Init(index);
    }
}
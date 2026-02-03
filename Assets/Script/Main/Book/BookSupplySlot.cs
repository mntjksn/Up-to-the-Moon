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
    [SerializeField] private GameObject bookSupplyPrefab;

    private Transform canvas2;

    private void Awake()
    {
        GameObject c = GameObject.Find("Canvas2");
        canvas2 = (c != null) ? c.transform : null;

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
        if (list == null || index < 0 || index >= list.Count)
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
            icon.enabled = true;
            icon.sprite = it.itemimg;
        }
        else
        {
            icon.enabled = false;
            icon.sprite = null;
        }
    }

    public void Show_Supply()
    {
        if (canvas2 == null || bookSupplyPrefab == null)
        {
            Debug.LogError("[BookSupplySlot] canvas2 또는 prefab이 비어 있습니다.");
            return;
        }

        GameObject go = Instantiate(bookSupplyPrefab, canvas2);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        BookSupplyPrefab panel = go.GetComponent<BookSupplyPrefab>();
        if (panel != null)
            panel.Init(index);
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BookSkySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;
    private bool initialized = false;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private GameObject bookSkyPrefab;

    private Transform canvas2;

    private void Awake()
    {
        canvas2 = GameObject.Find("Canvas2") != null ? GameObject.Find("Canvas2").transform : null;

        if (canvas2 == null)
            Debug.LogError("[BookSkySlot] Canvas2를 찾지 못했습니다. Hierarchy 이름이 Canvas2인지 확인하세요.");
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
        BackgroundManager bg = BackgroundManager.Instance;
        if (bg == null || !bg.IsLoaded)
        {
            ApplyUI(null);
            return;
        }

        var list = bg.BackgroundItem;
        if (list == null || index < 0 || index >= list.Count)
        {
            ApplyUI(null);
            return;
        }

        ApplyUI(list[index]);
    }

    private void ApplyUI(BackgroundItem it)
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
        if (canvas2 == null || bookSkyPrefab == null)
        {
            Debug.LogError("[BookSkySlot] canvas2 또는 prefab이 비어 있습니다.");
            return;
        }

        GameObject go = Instantiate(bookSkyPrefab, canvas2);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        BookSkyPrefab panel = go.GetComponent<BookSkyPrefab>();
        if (panel != null)
            panel.Init(index);
    }
}
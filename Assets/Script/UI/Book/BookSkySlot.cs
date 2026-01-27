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
    [SerializeField] private GameObject BookSkyPrefab;

    private Transform canvas2;

    private BackgroundItem item;

    private void Awake()
    {
        canvas2 = GameObject.Find("Canvas2")?.transform;
        if (canvas2 == null)
            Debug.LogError("[BookSkySlot] Canvas2를 찾지 못했습니다. Hierarchy 이름이 Canvas2 맞는지 확인!");
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
        var im = BackgroundManager.Instance;
        if (im == null || !im.IsLoaded) return;

        if (im.BackgroundItem == null || index < 0 || index >= im.BackgroundItem.Count)
        {
            ApplyUI(null);
            return;
        }

        item = im.BackgroundItem[index];
        ApplyUI(item);
    }

    private void ApplyUI(BackgroundItem it)
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
        if (canvas2 == null || BookSkyPrefab == null)
        {
            Debug.LogError($"[BookSkySlot] canvas2 or prefab null. canvas2={(canvas2 == null)} prefab={(BookSkyPrefab == null)}");
            return;
        }

        var go = Instantiate(BookSkyPrefab, canvas2);

        // 중앙 정렬
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        // 핵심: “내 슬롯 index”를 패널에 넘긴다
        var panel = go.GetComponent<BookSkyPrefab>();
        if (panel != null)
            panel.Init(index);
    }

}

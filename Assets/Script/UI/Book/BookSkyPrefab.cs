using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BookSkyPrefab : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image thisimg;
    [SerializeField] private TextMeshProUGUI chname;
    [SerializeField] private TextMeshProUGUI sub;

    private int bookIndex = -1;

    // 슬롯에서 호출
    public void Init(int index)
    {
        bookIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        var im = BackgroundManager.Instance;
        if (im == null || !im.IsLoaded) return;

        if (bookIndex < 0 || im.BackgroundItem == null || bookIndex >= im.BackgroundItem.Count)
            return;

        var item = im.BackgroundItem[bookIndex];
        if (item == null) return;

        if (thisimg != null) thisimg.sprite = item.itemimg;
        if (chname != null) chname.text = item.name;
        if (sub != null) sub.text = item.sub;
    }
}

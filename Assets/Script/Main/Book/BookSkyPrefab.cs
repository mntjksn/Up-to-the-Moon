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
        BackgroundManager bg = BackgroundManager.Instance;
        if (bg == null || !bg.IsLoaded) return;

        var list = bg.BackgroundItem;
        if (list == null) return;
        if (bookIndex < 0 || bookIndex >= list.Count) return;

        BackgroundItem item = list[bookIndex];
        if (item == null) return;

        if (thisimg != null)
            thisimg.sprite = item.itemimg;

        if (chname != null)
            chname.text = item.name;

        if (sub != null)
            sub.text = item.sub;
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BookSupplyPrefab : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image thisimg;
    [SerializeField] private TextMeshProUGUI chname;
    [SerializeField] private TextMeshProUGUI sub;

    private int bookIndex = -1;

    public void Init(int index)
    {
        bookIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        ItemManager item = ItemManager.Instance;
        if (item == null || !item.IsLoaded) return;

        var list = item.SupplyItem;
        if (list == null) return;
        if (bookIndex < 0 || bookIndex >= list.Count) return;

        SupplyItem it = list[bookIndex];
        if (it == null) return;

        if (thisimg != null)
            thisimg.sprite = it.itemimg;

        if (chname != null)
            chname.text = it.name;

        if (sub != null)
            sub.text = it.sub;
    }
}
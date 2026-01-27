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

    // 슬롯에서 호출
    public void Init(int index)
    {
        bookIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        var im = ItemManager.Instance;
        if (im == null || !im.IsLoaded) return;

        if (bookIndex < 0 || im.SupplyItem == null || bookIndex >= im.SupplyItem.Count)
            return;

        var item = im.SupplyItem[bookIndex];
        if (item == null) return;

        if (thisimg != null) thisimg.sprite = item.itemimg;
        if (chname != null) chname.text = item.name;
        if (sub != null) sub.text = item.sub;
    }
}
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

    // 캐시
    private Sprite lastSprite;
    private string lastName;
    private string lastSub;

    public void Init(int index)
    {
        // index 바뀌면 캐시 무효화
        if (bookIndex != index)
        {
            lastSprite = null;
            lastName = null;
            lastSub = null;
        }

        bookIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        ItemManager item = ItemManager.Instance;
        if (item == null || !item.IsLoaded) return;

        var list = item.SupplyItem;
        if (list == null || (uint)bookIndex >= (uint)list.Count) return;

        SupplyItem it = list[bookIndex];
        if (it == null) return;

        // 스프라이트
        if (thisimg != null && it.itemimg != null && lastSprite != it.itemimg)
        {
            thisimg.sprite = it.itemimg;
            lastSprite = it.itemimg;
        }

        // 이름
        if (chname != null && it.name != null && !string.Equals(lastName, it.name))
        {
            chname.text = it.name;
            lastName = it.name;
        }

        // 설명
        if (sub != null && it.sub != null && !string.Equals(lastSub, it.sub))
        {
            sub.text = it.sub;
            lastSub = it.sub;
        }
    }
}
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

    // 마지막 반영값 캐시 (불필요한 UI rebuild 방지)
    private Sprite lastSprite;
    private string lastName;
    private string lastSub;

    public void Init(int index)
    {
        bookIndex = index;

        // index 바뀌면 캐시 무효화
        lastSprite = null;
        lastName = null;
        lastSub = null;

        Refresh();
    }

    public void Refresh()
    {
        var bg = BackgroundManager.Instance;
        if (bg == null || !bg.IsLoaded) return;

        var list = bg.BackgroundItem;
        if (list == null) return;
        if ((uint)bookIndex >= (uint)list.Count) return;

        var item = list[bookIndex];
        if (item == null) return;

        // 스프라이트
        if (thisimg != null && item.itemimg != null && lastSprite != item.itemimg)
        {
            thisimg.sprite = item.itemimg;
            lastSprite = item.itemimg;
        }

        // 이름 텍스트
        if (chname != null && item.name != null && !string.Equals(lastName, item.name))
        {
            chname.text = item.name;
            lastName = item.name;
        }

        // 서브 텍스트
        if (sub != null && item.sub != null && !string.Equals(lastSub, item.sub))
        {
            sub.text = item.sub;
            lastSub = item.sub;
        }
    }
}
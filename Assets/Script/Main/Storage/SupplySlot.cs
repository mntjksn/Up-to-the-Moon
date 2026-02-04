using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SupplySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;

    private bool initialized = false;
    private SupplyItem item;

    // 캐시
    private int itemId = -1;
    private int lastOwned = int.MinValue;

    private void OnEnable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= OnResourceChanged;
            sm.OnResourceChanged += OnResourceChanged;
        }

        if (initialized)
            RefreshDynamicOnly(force: true); // 전체 Refresh 말고 수량만
    }

    private void OnDisable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged -= OnResourceChanged;
    }

    public void Setup(int idx)
    {
        index = idx;
        initialized = true;

        BindItemStatic();              // 아이콘 1회 세팅
        RefreshDynamicOnly(force: true); // 수량 표시
    }

    private void OnResourceChanged()
    {
        if (!initialized) return;
        RefreshDynamicOnly(force: false); // 변경 있을 때만
    }

    // 아이콘/아이템 연결(고정 UI)
    private void BindItemStatic()
    {
        var im = ItemManager.Instance;
        if (im == null || !im.IsLoaded)
        {
            ApplyEmpty();
            return;
        }

        var list = im.SupplyItem;
        if (list == null || (uint)index >= (uint)list.Count)
        {
            ApplyEmpty();
            return;
        }

        item = list[index];
        if (item == null)
        {
            ApplyEmpty();
            return;
        }

        itemId = item.item_num;

        if (icon != null)
        {
            if (item.itemimg != null)
            {
                icon.enabled = true;
                icon.sprite = item.itemimg;
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;
            }
        }

        // 캐시 초기화
        lastOwned = int.MinValue;
    }

    // 수량 텍스트만 (자주 호출되는 부분)
    private void RefreshDynamicOnly(bool force)
    {
        if (item == null || itemId < 0)
        {
            if (force) ApplyEmpty();
            return;
        }

        var sm = SaveManager.Instance;
        int owned = (sm != null) ? sm.GetResource(itemId) : 0;

        if (!force && owned == lastOwned)
            return;

        lastOwned = owned;

        if (countText != null)
            countText.text = NumberFormatter.FormatKorean(owned) + "개";
    }

    private void ApplyEmpty()
    {
        item = null;
        itemId = -1;
        lastOwned = int.MinValue;

        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        if (countText != null) countText.text = "";
    }

    // 기존 호환: 외부에서 Refresh 호출하면 전체 재바인딩
    public void Refresh()
    {
        BindItemStatic();
        RefreshDynamicOnly(force: true);
    }
}
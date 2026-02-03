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

    private void OnEnable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= OnResourceChanged;
            sm.OnResourceChanged += OnResourceChanged;
        }

        if (initialized)
            Refresh();
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
        Refresh();
    }

    private void OnResourceChanged()
    {
        if (!initialized) return;
        Refresh();
    }

    public void Refresh()
    {
        var im = ItemManager.Instance;
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

        item = list[index];
        ApplyUI(item);
    }

    private void ApplyUI(SupplyItem it)
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

        if (countText != null)
        {
            if (it == null)
            {
                countText.text = "";
                return;
            }

            var sm = SaveManager.Instance;
            int owned = (sm != null) ? sm.GetResource(it.item_num) : 0;

            countText.text = NumberFormatter.FormatKorean(owned) + "°³";
        }
    }
}
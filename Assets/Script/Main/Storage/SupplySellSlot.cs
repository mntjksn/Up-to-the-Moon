using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SupplySellSlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI bypriceText;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Button")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    private bool initialized = false;
    private SupplyItem item;

    private void OnEnable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnResourceChanged += HandleResourceChanged;

            sm.OnGoldChanged -= HandleGoldChanged;
            sm.OnGoldChanged += HandleGoldChanged;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OnClickSellAll);
            button.onClick.AddListener(OnClickSellAll);
        }

        if (initialized)
            Refresh();
    }

    private void OnDisable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnGoldChanged -= HandleGoldChanged;
        }

        if (button != null)
            button.onClick.RemoveListener(OnClickSellAll);
    }

    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
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

        if (it == null)
        {
            if (nameText != null) nameText.text = "";
            if (countText != null) countText.text = "";
            if (bypriceText != null) bypriceText.text = "";
            if (buttonText != null) buttonText.text = "";

            if (button != null) button.interactable = false;
            return;
        }

        string safeName = string.IsNullOrWhiteSpace(it.name) ? ("Item " + index) : it.name;

        if (nameText != null)
            nameText.text = safeName;

        var sm = SaveManager.Instance;
        int owned = (sm != null) ? sm.GetResource(it.item_num) : 0;

        if (countText != null)
            countText.text = NumberFormatter.FormatKorean(owned) + "°³";

        if (bypriceText != null)
            bypriceText.text = NumberFormatter.FormatKorean(it.item_price) + "¿ø";

        long totalPrice = (long)owned * it.item_price;

        if (buttonText != null)
            buttonText.text = NumberFormatter.FormatKorean(totalPrice) + "¿ø";

        if (button != null)
            button.interactable = owned > 0;
    }

    private void OnClickSellAll()
    {
        var sm = SaveManager.Instance;
        if (item == null || sm == null) return;

        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        int owned = sm.GetResource(item.item_num);
        if (owned <= 0) return;

        long totalPrice = (long)owned * item.item_price;

        sm.AddGold(totalPrice);
        sm.AddResource(item.item_num, -owned);

        MissionProgressManager.Instance?.Add("resource_sell_total", owned);

        Refresh();
    }

    private void HandleResourceChanged() { if (initialized) Refresh(); }
    private void HandleGoldChanged() { if (initialized) Refresh(); }
}
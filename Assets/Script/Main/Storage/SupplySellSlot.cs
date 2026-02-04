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

    // 캐시(변경 감지)
    private int itemId = -1;
    private int lastOwned = int.MinValue;
    private long lastTotalPrice = long.MinValue;

    private void OnEnable()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnResourceChanged += HandleResourceChanged;

            // 골드가 바뀌어도 이 슬롯 표시(판매가격)는 owned 기반이라 사실상 필요 없음
            // 그래도 유지하되 "가벼운 갱신"만 하게 만들 거라 OK
            sm.OnGoldChanged -= HandleGoldChanged;
            sm.OnGoldChanged += HandleGoldChanged;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OnClickSellAll);
            button.onClick.AddListener(OnClickSellAll);
        }

        if (initialized)
            RefreshStaticIfNeededAndDynamic(); // 가벼운 갱신
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

        BindItemFromIndex();            // 아이템/아이콘/이름 등 고정 UI 1회 세팅
        RefreshDynamicOnly(force: true); // 수량/버튼/가격만
    }

    // 아이템 고정 UI: icon/name/가격(단가) 등은 여기서만
    private void BindItemFromIndex()
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

        // icon
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

        // name
        string safeName = string.IsNullOrWhiteSpace(item.name) ? ("Item " + index) : item.name;
        if (nameText != null) nameText.text = safeName;

        // unit price
        if (bypriceText != null) bypriceText.text = NumberFormatter.FormatKorean(item.item_price) + "원";

        // 캐시 초기화(다시 계산하도록)
        lastOwned = int.MinValue;
        lastTotalPrice = long.MinValue;
    }

    // 외부에서 전체 Refresh 필요하면 호출(기존 호환)
    public void Refresh()
    {
        BindItemFromIndex();
        RefreshDynamicOnly(force: true);
    }

    private void RefreshStaticIfNeededAndDynamic()
    {
        // item이 아직 없거나 itemId가 비정상일 때만 static 재바인딩
        if (item == null || itemId < 0)
            BindItemFromIndex();

        RefreshDynamicOnly(force: false);
    }

    // 자주 갱신되는 부분만
    private void RefreshDynamicOnly(bool force)
    {
        if (item == null || itemId < 0)
        {
            ApplyEmpty();
            return;
        }

        var sm = SaveManager.Instance;
        int owned = (sm != null) ? sm.GetResource(itemId) : 0;

        if (!force && owned == lastOwned)
            return; // 변화 없으면 끝

        lastOwned = owned;

        if (countText != null)
            countText.text = NumberFormatter.FormatKorean(owned) + "개";

        long totalPrice = (long)owned * item.item_price;

        if (force || totalPrice != lastTotalPrice)
        {
            lastTotalPrice = totalPrice;
            if (buttonText != null)
                buttonText.text = NumberFormatter.FormatKorean(totalPrice) + "원";
        }

        if (button != null)
            button.interactable = owned > 0;
    }

    private void ApplyEmpty()
    {
        item = null;
        itemId = -1;

        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        if (nameText != null) nameText.text = "";
        if (countText != null) countText.text = "";
        if (bypriceText != null) bypriceText.text = "";
        if (buttonText != null) buttonText.text = "";

        if (button != null) button.interactable = false;

        lastOwned = int.MinValue;
        lastTotalPrice = long.MinValue;
    }

    private void OnClickSellAll()
    {
        var sm = SaveManager.Instance;
        if (item == null || sm == null) return;

        if (sfx != null)
        {
            var snd = SoundManager.Instance;
            if (snd != null) sfx.mute = !snd.IsSfxOn();
            sfx.Play();
        }

        int owned = sm.GetResource(itemId);
        if (owned <= 0) return;

        long totalPrice = (long)owned * item.item_price;

        sm.AddGold(totalPrice);
        sm.AddResource(itemId, -owned);

        MissionProgressManager.Instance?.Add("resource_sell_total", owned);

        // 즉시 반영(이 슬롯만)
        RefreshDynamicOnly(force: true);
    }

    private void HandleResourceChanged()
    {
        if (!initialized) return;
        RefreshDynamicOnly(force: false); // 핵심
    }

    private void HandleGoldChanged()
    {
        if (!initialized) return;
        // 골드는 이 슬롯 표시에 직접 영향 없음(판매가는 owned 기반)
        // 필요하면 force=false로 두거나 아예 구독 제거 가능
        // 여기서는 최소 유지:
        RefreshDynamicOnly(force: false);
    }
}
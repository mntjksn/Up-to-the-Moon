using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BlackholeUpgradeUI : MonoBehaviour
{
    [Header("Income UI")]
    [SerializeField] private TextMeshProUGUI incomeValueText;
    [SerializeField] private TextMeshProUGUI incomePriceText;
    [SerializeField] private Button incomeBuyButton;

    [Header("Storage UI")]
    [SerializeField] private TextMeshProUGUI storageValueText;
    [SerializeField] private TextMeshProUGUI storagePriceText;
    [SerializeField] private Button storageBuyButton;

    [Header("Tuning")]
    [SerializeField] private int maxIncomeLv = 50;
    [SerializeField] private int maxStorageLv = 50;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    private void OnEnable()
    {
        if (incomeBuyButton != null) incomeBuyButton.onClick.AddListener(BuyIncome);
        if (storageBuyButton != null) storageBuyButton.onClick.AddListener(BuyStorage);
        RefreshAll();
    }

    private void OnDisable()
    {
        if (incomeBuyButton != null) incomeBuyButton.onClick.RemoveListener(BuyIncome);
        if (storageBuyButton != null) storageBuyButton.onClick.RemoveListener(BuyStorage);
    }

    public void RefreshAll()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null) return;

        RefreshIncome(save);
        RefreshStorage(save);
    }

    private void RefreshIncome(SaveManager save)
    {
        int lv = save.GetIncomeLv();
        bool isMax = lv >= maxIncomeLv;

        float cur = GetIncomeByLevel(lv);
        float next = GetIncomeByLevel(Mathf.Min(lv + 1, maxIncomeLv));

        if (incomeValueText != null)
            incomeValueText.text = isMax
                ? string.Format("{0:0.##}개/s (MAX)", cur)
                : string.Format("{0:0.##}개/s -> {1:0.##}개/s", cur, next);

        long price = GetIncomePrice(lv);
        long gold = save.GetGold();

        if (incomePriceText != null)
            incomePriceText.text = isMax ? "MAX" : NumberFormatter.FormatKorean(price) + "원";

        if (incomeBuyButton != null)
            incomeBuyButton.interactable = !isMax && gold >= price;
    }

    private void BuyIncome()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null) return;

        MissionProgressManager.Instance?.Add("blackhole_income_upgrade_count", 1);

        int lv = save.GetIncomeLv();
        if (lv >= maxIncomeLv) return;

        long price = GetIncomePrice(lv);
        if (save.GetGold() < price) return;

        PlaySfx();

        save.AddGold(-price);
        save.AddIncomeLv(1);

        float income = GetIncomeByLevel(lv + 1);
        save.SetIncome(income);

        RefreshAll();
    }

    public float GetIncomeByLevel(int L)
    {
        if (L <= 3) return 0.5f + 0.5f * L;
        if (L <= 6) return 2.0f + 1f * (L - 3);
        if (L <= 11) return 5.0f + 2f * (L - 6);
        if (L <= 15) return 15.0f + 2.5f * (L - 11);
        if (L <= 18) return 25.0f + 5f * (L - 15);
        return 40.0f + 10f * (L - 18);
    }

    private long GetIncomePrice(int lv)
    {
        double basePrice = 100;
        double mult = 2.25;
        double raw = basePrice * System.Math.Pow(mult, lv);

        if (raw > long.MaxValue) return long.MaxValue;

        long v = (long)raw;
        return CeilTo(v, 100);
    }

    private void RefreshStorage(SaveManager save)
    {
        int lv = save.GetStorageLv();
        bool isMax = lv >= maxStorageLv;

        long cur = GetStorageByLevel(lv);
        long next = GetStorageByLevel(Mathf.Min(lv + 1, maxStorageLv));

        if (storageValueText != null)
            storageValueText.text = isMax
                ? NumberFormatter.FormatKorean(cur) + "개 (MAX)"
                : NumberFormatter.FormatKorean(cur) + "개 -> " + NumberFormatter.FormatKorean(next) + "개";

        long price = GetStoragePrice(lv);
        long gold = save.GetGold();

        if (storagePriceText != null)
            storagePriceText.text = isMax ? "MAX" : NumberFormatter.FormatKorean(price) + "원";

        if (storageBuyButton != null)
            storageBuyButton.interactable = !isMax && gold >= price;
    }

    private void BuyStorage()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null) return;

        int lv = save.GetStorageLv();
        if (lv >= maxStorageLv) return;

        long price = GetStoragePrice(lv);
        if (save.GetGold() < price) return;

        PlaySfx();

        save.AddGold(-price);
        save.AddStorageLv(1);

        // SaveManager에 SetStorageMax 같은 함수가 없어서 현재는 직접 반영
        if (save.Data != null && save.Data.blackHole != null)
        {
            long max = GetStorageByLevel(save.GetStorageLv());
            save.Data.blackHole.BlackHoleStorageMax = max;
            save.Save();
        }

        RefreshAll();
    }

    private long GetStorageByLevel(int lv)
    {
        long baseCap = 100;
        double mult = 2.8;
        double raw = baseCap * System.Math.Pow(mult, lv);

        if (raw > long.MaxValue) return long.MaxValue;

        long v = (long)raw;
        return CeilTo(v, 100);
    }

    private long GetStoragePrice(int lv)
    {
        double basePrice = 500;
        double mult = 5.5;
        double raw = basePrice * System.Math.Pow(mult, lv);

        if (raw > long.MaxValue) return long.MaxValue;

        long v = (long)raw;
        return CeilTo(v, 500);
    }

    private void PlaySfx()
    {
        if (sfx == null) return;

        SoundManager sm = SoundManager.Instance;
        if (sm != null) sfx.mute = !sm.IsSfxOn();

        sfx.Play();
    }

    private long CeilTo(long value, long step)
    {
        if (step <= 0) return value;
        if (value <= 0) return 0;
        return ((value + step - 1) / step) * step;
    }
}
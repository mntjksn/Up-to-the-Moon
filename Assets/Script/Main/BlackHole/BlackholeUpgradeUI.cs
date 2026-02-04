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

    // ---- UI 캐시(같은 값이면 텍스트/버튼 갱신 안 함) ----
    private int lastIncomeLv = int.MinValue;
    private int lastStorageLv = int.MinValue;
    private long lastGold = long.MinValue;

    private bool listenersBound = false;

    private void OnEnable()
    {
        BindListenersOnce();
        // 패널 열릴 때는 강제 갱신
        ForceRefreshAll();
    }

    private void OnDisable()
    {
        // 리스너는 한 번만 바인딩하고, 오브젝트 파괴 때 해제하는 방식도 가능하지만
        // 구조 유지 위해 기존처럼 Enable/Disable에 맞춰 제거
        UnbindListeners();
        listenersBound = false;
    }

    private void BindListenersOnce()
    {
        if (listenersBound) return;

        if (incomeBuyButton != null) incomeBuyButton.onClick.AddListener(BuyIncome);
        if (storageBuyButton != null) storageBuyButton.onClick.AddListener(BuyStorage);

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (incomeBuyButton != null) incomeBuyButton.onClick.RemoveListener(BuyIncome);
        if (storageBuyButton != null) storageBuyButton.onClick.RemoveListener(BuyStorage);
    }

    // 외부에서 호출해도 되지만, “변경된 것만” 반영
    public void RefreshAll()
    {
        var save = SaveManager.Instance;
        if (save == null) return;

        int incomeLv = save.GetIncomeLv();
        int storageLv = save.GetStorageLv();
        long gold = save.GetGold();

        // 전부 같으면 아무것도 안 함
        if (incomeLv == lastIncomeLv && storageLv == lastStorageLv && gold == lastGold)
            return;

        // 부분 갱신
        if (incomeLv != lastIncomeLv || gold != lastGold)
            RefreshIncome(save, incomeLv, gold);

        if (storageLv != lastStorageLv || gold != lastGold)
            RefreshStorage(save, storageLv, gold);

        lastIncomeLv = incomeLv;
        lastStorageLv = storageLv;
        lastGold = gold;
    }

    // 패널 열릴 때는 무조건 갱신(캐시 초기화)
    private void ForceRefreshAll()
    {
        lastIncomeLv = int.MinValue;
        lastStorageLv = int.MinValue;
        lastGold = long.MinValue;
        RefreshAll();
    }

    private void RefreshIncome(SaveManager save, int lv, long gold)
    {
        bool isMax = lv >= maxIncomeLv;

        float cur = GetIncomeByLevel(lv);
        float next = GetIncomeByLevel(Mathf.Min(lv + 1, maxIncomeLv));

        if (incomeValueText != null)
        {
            // string.Format 대신 간단 연결(조금 더 가벼움)
            incomeValueText.text = isMax
                ? $"{cur:0.##}개/s (MAX)"
                : $"{cur:0.##}개/s -> {next:0.##}개/s";
        }

        long price = GetIncomePrice(lv);

        if (incomePriceText != null)
            incomePriceText.text = isMax ? "MAX" : NumberFormatter.FormatKorean(price) + "원";

        if (incomeBuyButton != null)
            incomeBuyButton.interactable = !isMax && gold >= price;
    }

    private void BuyIncome()
    {
        var save = SaveManager.Instance;
        if (save == null) return;

        int lv = save.GetIncomeLv();
        if (lv >= maxIncomeLv) return;

        long price = GetIncomePrice(lv);
        if (save.GetGold() < price) return;

        PlaySfx();

        // 미션 카운트는 성공시에만 올리는 게 보통이라 여기로 이동(원래 위치면 실패해도 카운트됨)
        MissionProgressManager.Instance?.Add("blackhole_income_upgrade_count", 1);

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

    private void RefreshStorage(SaveManager save, int lv, long gold)
    {
        bool isMax = lv >= maxStorageLv;

        long cur = GetStorageByLevel(lv);
        long next = GetStorageByLevel(Mathf.Min(lv + 1, maxStorageLv));

        if (storageValueText != null)
        {
            string curStr = NumberFormatter.FormatKorean(cur);
            storageValueText.text = isMax
                ? curStr + "개 (MAX)"
                : curStr + "개 -> " + NumberFormatter.FormatKorean(next) + "개";
        }

        long price = GetStoragePrice(lv);

        if (storagePriceText != null)
            storagePriceText.text = isMax ? "MAX" : NumberFormatter.FormatKorean(price) + "원";

        if (storageBuyButton != null)
            storageBuyButton.interactable = !isMax && gold >= price;
    }

    private void BuyStorage()
    {
        var save = SaveManager.Instance;
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

            // debounce 저장이면 부담 적음
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
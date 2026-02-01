using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BlackholeUpgradeUI : MonoBehaviour
{
    [Header("Income UI")]
    [SerializeField] private TextMeshProUGUI incomeValueText;   // 노란 글씨: "0.4개/s → 0.5개/s"
    [SerializeField] private TextMeshProUGUI incomePriceText;   // 버튼 가격 텍스트
    [SerializeField] private Button incomeBuyButton;

    [Header("Storage UI")]
    [SerializeField] private TextMeshProUGUI storageValueText;  // 노란 글씨: "1000개 → 5000개"
    [SerializeField] private TextMeshProUGUI storagePriceText;  // 버튼 가격 텍스트
    [SerializeField] private Button storageBuyButton;

    [Header("Tuning")]
    [SerializeField] private int maxIncomeLv = 50;
    [SerializeField] private int maxStorageLv = 50;

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
        if (SaveManager.Instance == null) return;

        RefreshIncome();
        RefreshStorage();
    }

    // =========================
    // Income (초당 흡수량)
    // =========================
    private void RefreshIncome()
    {
        int lv = SaveManager.Instance.GetIncomeLv();

        float cur = GetIncomeByLevel(lv);
        float next = GetIncomeByLevel(Mathf.Min(lv + 1, maxIncomeLv));

        bool isMax = lv >= maxIncomeLv;

        if (incomeValueText != null)
            incomeValueText.text = isMax ? $"{cur:0.##}개/s (MAX)" : $"{cur:0.##}개/s → {next:0.##}개/s";

        long price = GetIncomePrice(lv);

        if (incomeBuyButton != null)
            incomeBuyButton.interactable =
                !isMax && SaveManager.Instance.GetGold() >= price;

        if (incomePriceText != null)
            incomePriceText.text = isMax ? "MAX" : $"{FormatKoreanNumber(price)}원";

        if (incomeBuyButton != null)
            incomeBuyButton.interactable = !isMax && SaveManager.Instance.GetGold() >= price;
    }

    private void BuyIncome()
    {
        int lv = SaveManager.Instance.GetIncomeLv();
        if (lv >= maxIncomeLv) return;

        long price = GetIncomePrice(lv);
        if (SaveManager.Instance.GetGold() < price) return;

        // 돈 차감
        SaveManager.Instance.AddGold(-price);

        // 레벨 업
        SaveManager.Instance.AddIncomeLv(1);

        float cur = GetIncomeByLevel(lv + 1);
        SaveManager.Instance.AddIncome(cur);

        RefreshAll();
    }

    // 네가 만든 표 스타일(구간별) 그대로 넣기
    public float GetIncomeByLevel(int L)
    {
        // 0~3 : 0.5씩
        if (L <= 3)
            return 0.5f + 0.5f * L;

        // 4~6 : 1씩
        if (L <= 6)
            return 2.0f + 1f * (L - 3);

        // 7~11 : 2씩
        if (L <= 11)
            return 5.0f + 2f * (L - 6);

        // 12~15 : 2.5씩
        if (L <= 15)
            return 15.0f + 2.5f * (L - 11);

        // 16~18 : 5씩
        if (L <= 18)
            return 25.0f + 5f * (L - 15);

        // 19 이상 : 10씩
        return 40.0f + 10f * (L - 18);
    }

    // 가격은 예시: 단계가 오를수록 점점 비싸게(원하면 바꿔줄게)
    private long GetIncomePrice(int lv)
    {
        // ex) 1000원 시작, 1.35배씩 증가
        double basePrice = 100;
        double mult = 2.25;
        double v = basePrice * System.Math.Pow(mult, lv);
        if (v > long.MaxValue) return long.MaxValue;
        return (long)v;
    }

    // =========================
    // Storage (최대 적재량)
    // =========================
    private void RefreshStorage()
    {
        int lv = SaveManager.Instance.GetStorageLv();

        long cur = GetStorageByLevel(lv);
        long next = GetStorageByLevel(Mathf.Min(lv + 1, maxStorageLv));

        bool isMax = lv >= maxStorageLv;

        if (storageValueText != null)
            storageValueText.text = isMax ? $"{FormatKoreanNumber(cur)}개 (MAX)" : $"{FormatKoreanNumber(cur)}개 → {FormatKoreanNumber(next)}개";

        long price = GetStoragePrice(lv);

        if (storageBuyButton != null)
            storageBuyButton.interactable =
                !isMax && SaveManager.Instance.GetGold() >= price;

        if (storagePriceText != null)
            storagePriceText.text = isMax ? "MAX" : $"{FormatKoreanNumber(price)}원";

        if (storageBuyButton != null)
            storageBuyButton.interactable = !isMax && SaveManager.Instance.GetGold() >= price;
    }

    private void BuyStorage()
    {
        int lv = SaveManager.Instance.GetStorageLv();
        if (lv >= maxStorageLv) return;

        long price = GetStoragePrice(lv);
        if (SaveManager.Instance.GetGold() < price) return;

        SaveManager.Instance.AddGold(-price);
        SaveManager.Instance.AddStorageLv(1);

        // storageMax는 실제 데이터에도 반영해줘야 저장고 로직이 동작함
        SaveManager.Instance.Data.blackHole.BlackHoleStorageMax = GetStorageByLevel(SaveManager.Instance.GetStorageLv());
        SaveManager.Instance.Save();

        RefreshAll();
    }

    private long GetStorageByLevel(int lv)
    {
        // 예시: 1000에서 시작해서 점점 커지게
        // 원하면 “1000→5000→…” 너가 원하는 표로 바꿔줄게.
        long baseCap = 100;
        double mult = 2.5; // 25%씩 증가
        double v = baseCap * System.Math.Pow(mult, lv);
        if (v > long.MaxValue) return long.MaxValue;
        return (long)v;
    }

    private long GetStoragePrice(int lv)
    {
        // ex) 2000원 시작, 1.4배씩 증가
        double basePrice = 500;
        double mult = 5.5;
        double v = basePrice * System.Math.Pow(mult, lv);
        if (v > long.MaxValue) return long.MaxValue;
        return (long)v;
    }

    // =========================
    // 숫자 포맷
    // =========================
    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";

        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;
        const ulong EOK = 100_000_000UL;
        const ulong JO = 1_000_000_000_000UL;
        const ulong GYEONG = 10_000_000_000_000_000UL;

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(jo).Append("조"); }
        if (eok > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(eok).Append("억"); }
        if (man > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(man).Append("만"); }
        if (rest > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(rest); }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoostManager : MonoBehaviour
{
    [Header("Price")]
    [SerializeField] private long unlockPrice = 5000;

    [Header("Canvas2 Panels (Upgrade&Booster Window)")]
    [SerializeField] private GameObject panelBoost_Locked;   // Canvas2 > Panel_Boost (해금 전)
    [SerializeField] private GameObject panelBoost_Main;     // Canvas2 > Panel_Boost_Main (해금 후)

    [Header("Unlock Button")]
    [SerializeField] private Button buyButton;

    [Header("Upgrade Buttons")]
    [SerializeField] private Button speedUpButton;  // "부스터 속도 업그레이드"
    [SerializeField] private Button timeUpButton;   // "부스터 지속시간 업그레이드"

    [Header("Upgrade Price Text")]
    [SerializeField] private TextMeshProUGUI speedPriceText; // 1000원 텍스트
    [SerializeField] private TextMeshProUGUI timePriceText;  // 500원 텍스트

    [Header("Upgrade Label Text (optional)")]
    [SerializeField] private TextMeshProUGUI speedDescText;  // +25% 증가 같은 설명
    [SerializeField] private TextMeshProUGUI timeDescText;

    private void OnEnable()
    {
        // 해금 버튼
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(BuyBoostUnlock);
            buyButton.onClick.AddListener(BuyBoostUnlock);
        }

        // 업그레이드 버튼
        if (speedUpButton != null)
        {
            speedUpButton.onClick.RemoveListener(UpgradeSpeed);
            speedUpButton.onClick.AddListener(UpgradeSpeed);
        }

        if (timeUpButton != null)
        {
            timeUpButton.onClick.RemoveListener(UpgradeTime);
            timeUpButton.onClick.AddListener(UpgradeTime);
        }

        RefreshFromSave();
    }

    private void OnDisable()
    {
        if (buyButton != null) buyButton.onClick.RemoveListener(BuyBoostUnlock);
        if (speedUpButton != null) speedUpButton.onClick.RemoveListener(UpgradeSpeed);
        if (timeUpButton != null) timeUpButton.onClick.RemoveListener(UpgradeTime);
    }

    private void RefreshFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
        if (SaveManager.Instance.Data.boost == null) return;

        bool unlocked = SaveManager.Instance.Data.boost.boostUnlock;
        ApplyUI(unlocked);
        RefreshUpgradeUI();
    }

    private void ApplyUI(bool unlocked)
    {
        // Canvas2 패널 토글
        if (panelBoost_Locked != null) panelBoost_Locked.SetActive(!unlocked);
        if (panelBoost_Main != null) panelBoost_Main.SetActive(unlocked);

        // 구매 버튼은 해금 후 비활성
        if (buyButton != null) buyButton.interactable = !unlocked;
    }

    private void RefreshUpgradeUI()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
        var b = SaveManager.Instance.Data.boost;
        if (b == null) return;

        long gold = SaveManager.Instance.GetGold();
        bool unlocked = b.boostUnlock;

        // ===== 캡 설정 =====
        const float TIME_CAP = 30f;
        bool timeCapReached = b.boostTime >= TIME_CAP;

        // ===== 가격 표시 =====
        if (speedPriceText != null)
            speedPriceText.text = $"{FormatKoreanNumber(b.boostSpeedPrice)}원";

        // 지속시간이 캡이면 가격 대신 MAX 표기
        if (timePriceText != null)
            timePriceText.text = timeCapReached ? "MAX" : $"{FormatKoreanNumber(b.boostTimePrice)}원";

        // ===== 설명(옵션) =====
        if (speedDescText != null)
            speedDescText.text = $"+25% 증가 (현재: {b.boostSpeed:N0}%)";

        if (timeDescText != null)
            timeDescText.text = $"+25% 증가 (현재: {b.boostTime:0.##}초)";

        // ===== 버튼 잠금 조건 =====
        // 골드 부족하면 잠금 + 해금 전이면 잠금
        if (speedUpButton != null)
            speedUpButton.interactable = unlocked && gold >= b.boostSpeedPrice;

        // 시간 업글: 해금 + 골드 + 캡 미도달일 때만 활성
        if (timeUpButton != null)
            timeUpButton.interactable = unlocked && !timeCapReached && gold >= b.boostTimePrice;

        if (buyButton != null)
        {
            if (unlocked)
                buyButton.interactable = false;
            else
                buyButton.interactable = gold >= unlockPrice;
        }
    }

    private void BuyBoostUnlock()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
        var b = SaveManager.Instance.Data.boost;
        if (b == null) return;

        if (b.boostUnlock) return;
        if (SaveManager.Instance.GetGold() < unlockPrice) return;

        SaveManager.Instance.AddGold(-unlockPrice);

        b.boostUnlock = true;
        SaveManager.Instance.Save();

        ApplyUI(true);
        RefreshUpgradeUI();
    }

    private void UpgradeSpeed()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
        var b = SaveManager.Instance.Data.boost;
        if (b == null || !b.boostUnlock) return;

        if (SaveManager.Instance.GetGold() < b.boostSpeedPrice) return;

        // 돈 차감
        SaveManager.Instance.AddGold(-b.boostSpeedPrice);

        // 수치 25% 증가(무제한)
        b.boostSpeed += 25f;

        // 가격 2배
        b.boostSpeedPrice *= 2;

        SaveManager.Instance.Save();
        RefreshUpgradeUI();
    }

    private void UpgradeTime()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        var b = sm.Data.boost;
        if (!b.boostUnlock) return;

        float maxTime = 30f;   // 최대 지속시간

        // 이미 최대면 더 못 올리게
        if (b.boostTime >= maxTime)
        {
            b.boostTime = maxTime;
            sm.Save();
            RefreshUpgradeUI();
            return;
        }

        if (sm.GetGold() < b.boostTimePrice) return;

        sm.AddGold(-b.boostTimePrice);

        // 25% 증가
        float next = b.boostTime * 1.25f;

        // 45초 초과 방지
        b.boostTime = Mathf.Min(next, maxTime);

        // 가격 2배
        b.boostTimePrice *= 2;

        sm.Save();
        RefreshUpgradeUI();
    }

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
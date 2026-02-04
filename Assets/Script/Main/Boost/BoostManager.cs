using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoostManager : MonoBehaviour
{
    [Header("Price")]
    [SerializeField] private long unlockPrice = 5000;

    [Header("Canvas2 Panels (Upgrade&Booster Window)")]
    [SerializeField] private GameObject panelBoost_Locked;
    [SerializeField] private GameObject panelBoost_Main;

    [Header("Unlock Button")]
    [SerializeField] private Button buyButton;

    [Header("Upgrade Buttons")]
    [SerializeField] private Button speedUpButton;
    [SerializeField] private Button timeUpButton;

    [Header("Upgrade Price Text")]
    [SerializeField] private TextMeshProUGUI speedPriceText;
    [SerializeField] private TextMeshProUGUI timePriceText;

    [Header("Upgrade Label Text (optional)")]
    [SerializeField] private TextMeshProUGUI speedDescText;
    [SerializeField] private TextMeshProUGUI timeDescText;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    private const float TIME_CAP = 30f;

    // UI 캐시(같은 값이면 갱신 스킵)
    private bool lastUnlocked;
    private long lastGold;
    private long lastSpeedPrice;
    private long lastTimePrice;
    private float lastBoostSpeed;
    private float lastBoostTime;

    private bool listenersBound = false;

    private void Awake()
    {
        BindButtonsOnce();
    }

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void BindButtonsOnce()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (buyButton != null) buyButton.onClick.AddListener(BuyBoostUnlock);
        if (speedUpButton != null) speedUpButton.onClick.AddListener(UpgradeSpeed);
        if (timeUpButton != null) timeUpButton.onClick.AddListener(UpgradeTime);
    }

    private void ForceRefresh()
    {
        // 캐시 무효화
        lastUnlocked = !lastUnlocked;
        lastGold = long.MinValue;
        lastSpeedPrice = long.MinValue;
        lastTimePrice = long.MinValue;
        lastBoostSpeed = float.NaN;
        lastBoostTime = float.NaN;

        RefreshFromSave();
    }

    private void RefreshFromSave()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (!TryGetBoost(sm, out var b)) return;

        bool unlocked = sm.IsBoostUnlocked();
        long gold = sm.GetGold();
        float boostSpeed = sm.GetBoostSpeed();
        float boostTime = sm.GetBoostTime();

        // 완전 동일하면 스킵
        if (unlocked == lastUnlocked &&
            gold == lastGold &&
            b.boostSpeedPrice == lastSpeedPrice &&
            b.boostTimePrice == lastTimePrice &&
            Mathf.Approximately(boostSpeed, lastBoostSpeed) &&
            Mathf.Approximately(boostTime, lastBoostTime))
        {
            return;
        }

        ApplyUI(unlocked);
        RefreshUpgradeUI(unlocked, gold, b.boostSpeedPrice, b.boostTimePrice, boostSpeed, boostTime);

        lastUnlocked = unlocked;
        lastGold = gold;
        lastSpeedPrice = b.boostSpeedPrice;
        lastTimePrice = b.boostTimePrice;
        lastBoostSpeed = boostSpeed;
        lastBoostTime = boostTime;
    }

    private void ApplyUI(bool unlocked)
    {
        if (panelBoost_Locked != null && panelBoost_Locked.activeSelf == unlocked)
            panelBoost_Locked.SetActive(!unlocked);

        if (panelBoost_Main != null && panelBoost_Main.activeSelf != unlocked)
            panelBoost_Main.SetActive(unlocked);
    }

    private void RefreshUpgradeUI(bool unlocked, long gold, long speedPrice, long timePrice, float boostSpeed, float boostTime)
    {
        bool timeCapReached = boostTime >= TIME_CAP;

        if (speedPriceText != null)
            speedPriceText.text = NumberFormatter.FormatKorean(speedPrice) + "원";

        if (timePriceText != null)
            timePriceText.text = timeCapReached ? "MAX" : (NumberFormatter.FormatKorean(timePrice) + "원");

        if (speedDescText != null)
            speedDescText.text = $"+25% 증가 (현재: {boostSpeed:N0}%)";

        if (timeDescText != null)
            timeDescText.text = $"+25% 증가 (현재: {boostTime:0.##}초)";

        if (speedUpButton != null)
            speedUpButton.interactable = unlocked && gold >= speedPrice;

        if (timeUpButton != null)
            timeUpButton.interactable = unlocked && !timeCapReached && gold >= timePrice;

        if (buyButton != null)
            buyButton.interactable = !unlocked && gold >= unlockPrice;
    }

    private void BuyBoostUnlock()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;
        if (!TryGetBoost(sm, out var b)) return;

        if (sm.IsBoostUnlocked()) return;
        if (sm.GetGold() < unlockPrice) return;

        PlaySfx();

        sm.AddGold(-unlockPrice);
        sm.SetBoostUnlocked(true);

        if (MissionProgressManager.Instance != null)
            MissionProgressManager.Instance.SetUnlocked("boost_unlock", true);

        RefreshFromSave();
    }

    private void UpgradeSpeed()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;
        if (!sm.IsBoostUnlocked()) return;

        if (!TryGetBoost(sm, out var b)) return;
        if (sm.GetGold() < b.boostSpeedPrice) return;

        PlaySfx();

        sm.AddGold(-b.boostSpeedPrice);

        float newSpeed = sm.GetBoostSpeed() + 25f;
        sm.SetBoostSpeed(newSpeed);

        b.boostSpeedPrice *= 2;
        sm.Save();

        RefreshFromSave();
    }

    private void UpgradeTime()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;
        if (!sm.IsBoostUnlocked()) return;

        if (!TryGetBoost(sm, out var b)) return;

        float cur = sm.GetBoostTime();
        if (cur >= TIME_CAP)
        {
            if (!Mathf.Approximately(cur, TIME_CAP))
            {
                sm.SetBoostTime(TIME_CAP);
                sm.Save();
            }
            RefreshFromSave();
            return;
        }

        if (sm.GetGold() < b.boostTimePrice) return;

        PlaySfx();

        sm.AddGold(-b.boostTimePrice);

        float next = cur * 1.25f;
        float newTime = Mathf.Min(next, TIME_CAP);
        sm.SetBoostTime(newTime);

        b.boostTimePrice *= 2;
        sm.Save();

        RefreshFromSave();
    }

    private bool TryGetBoost(SaveManager sm, out SaveData.Boost boost)
    {
        boost = null;
        if (sm == null || sm.Data == null || sm.Data.boost == null) return false;
        boost = sm.Data.boost;
        return true;
    }

    private void PlaySfx()
    {
        if (sfx == null) return;

        SoundManager snd = SoundManager.Instance;
        if (snd != null) sfx.mute = !snd.IsSfxOn();

        sfx.Play();
    }
}
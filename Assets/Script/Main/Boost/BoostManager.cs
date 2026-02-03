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

    private void OnEnable()
    {
        BindButtons(true);
        RefreshFromSave();
    }

    private void OnDisable()
    {
        BindButtons(false);
    }

    private void BindButtons(bool bind)
    {
        if (buyButton != null)
        {
            if (bind) buyButton.onClick.AddListener(BuyBoostUnlock);
            else buyButton.onClick.RemoveListener(BuyBoostUnlock);
        }

        if (speedUpButton != null)
        {
            if (bind) speedUpButton.onClick.AddListener(UpgradeSpeed);
            else speedUpButton.onClick.RemoveListener(UpgradeSpeed);
        }

        if (timeUpButton != null)
        {
            if (bind) timeUpButton.onClick.AddListener(UpgradeTime);
            else timeUpButton.onClick.RemoveListener(UpgradeTime);
        }
    }

    private void RefreshFromSave()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return;

        bool unlocked = sm.IsBoostUnlocked();
        ApplyUI(unlocked);
        RefreshUpgradeUI(sm, b);
    }

    private void ApplyUI(bool unlocked)
    {
        if (panelBoost_Locked != null) panelBoost_Locked.SetActive(!unlocked);
        if (panelBoost_Main != null) panelBoost_Main.SetActive(unlocked);

        if (buyButton != null) buyButton.interactable = !unlocked;
    }

    private void RefreshUpgradeUI(SaveManager sm, SaveData.Boost b)
    {
        long gold = sm.GetGold();
        bool unlocked = sm.IsBoostUnlocked();

        bool timeCapReached = sm.GetBoostTime() >= TIME_CAP;

        if (speedPriceText != null)
            speedPriceText.text = NumberFormatter.FormatKorean(b.boostSpeedPrice) + "원";

        if (timePriceText != null)
            timePriceText.text = timeCapReached ? "MAX" : (NumberFormatter.FormatKorean(b.boostTimePrice) + "원");

        if (speedDescText != null)
            speedDescText.text = "+25% 증가 (현재: " + sm.GetBoostSpeed().ToString("N0") + "%)";

        if (timeDescText != null)
            timeDescText.text = "+25% 증가 (현재: " + sm.GetBoostTime().ToString("0.##") + "초)";

        if (speedUpButton != null)
            speedUpButton.interactable = unlocked && gold >= b.boostSpeedPrice;

        if (timeUpButton != null)
            timeUpButton.interactable = unlocked && !timeCapReached && gold >= b.boostTimePrice;

        if (buyButton != null)
            buyButton.interactable = !unlocked && gold >= unlockPrice;
    }

    private void BuyBoostUnlock()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return;

        if (sm.IsBoostUnlocked()) return;
        if (sm.GetGold() < unlockPrice) return;

        PlaySfx();

        sm.AddGold(-unlockPrice);
        sm.SetBoostUnlocked(true);

        ApplyUI(true);
        RefreshUpgradeUI(sm, b);

        if (MissionProgressManager.Instance != null)
            MissionProgressManager.Instance.SetUnlocked("boost_unlock", true);
    }

    private void UpgradeSpeed()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;
        if (!sm.IsBoostUnlocked()) return;

        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return;

        if (sm.GetGold() < b.boostSpeedPrice) return;

        PlaySfx();

        sm.AddGold(-b.boostSpeedPrice);

        float newSpeed = sm.GetBoostSpeed() + 25f;
        sm.SetBoostSpeed(newSpeed);

        b.boostSpeedPrice *= 2;
        sm.Save();

        RefreshUpgradeUI(sm, b);
    }

    private void UpgradeTime()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;
        if (!sm.IsBoostUnlocked()) return;

        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return;

        float cur = sm.GetBoostTime();
        if (cur >= TIME_CAP)
        {
            sm.SetBoostTime(TIME_CAP);
            sm.Save();
            RefreshUpgradeUI(sm, b);
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

        RefreshUpgradeUI(sm, b);
    }

    private bool TryGetBoost(SaveManager sm, out SaveData.Boost boost)
    {
        boost = null;
        if (sm == null) return false;
        if (sm.Data == null) return false;
        if (sm.Data.boost == null) return false;

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
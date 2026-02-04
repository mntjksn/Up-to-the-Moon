using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    BoostManager

    [역할]
    - 부스터 기능의 해금/업그레이드 UI를 관리한다.
    - 골드 보유량과 해금 여부에 따라 버튼 활성/비활성을 제어한다.
    - 강화 구매 시 SaveManager의 데이터를 갱신하고, 가격/상한 로직을 적용한다.

    [설계 의도]
    1) UI 갱신 최적화
       - 동일한 값으로 매번 텍스트/버튼 상태를 갱신하면 비용이 발생하므로,
         마지막으로 반영한 값(lastXXX)을 캐시하고 변화가 없으면 갱신을 스킵한다.
    2) 입력 안정성
       - 버튼 이벤트는 Awake에서 1회만 바인딩하여 중복 등록을 방지한다.
    3) 명확한 게임 규칙
       - 부스트 시간은 TIME_CAP까지로 제한한다.
       - 업그레이드 가격은 구매 후 2배로 증가시켜 성장 곡선을 만든다.
*/
public class BoostManager : MonoBehaviour
{
    [Header("Price")]
    // 부스터 최초 해금 가격
    [SerializeField] private long unlockPrice = 5000;

    [Header("Canvas2 Panels (Upgrade&Booster Window)")]
    // 해금 전/후에 보여줄 패널을 분리하여 UI 상태를 명확히 한다.
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
    // 강화 설명(현재 값 표시 등). 없어도 동작하도록 optional로 둔다.
    [SerializeField] private TextMeshProUGUI speedDescText;
    [SerializeField] private TextMeshProUGUI timeDescText;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    // 부스트 시간 상한(초)
    private const float TIME_CAP = 30f;

    // UI 캐시(이전과 동일하면 갱신을 생략한다)
    private bool lastUnlocked;
    private long lastGold;
    private long lastSpeedPrice;
    private long lastTimePrice;
    private float lastBoostSpeed;
    private float lastBoostTime;

    // 버튼 리스너 중복 등록 방지용
    private bool listenersBound = false;

    private void Awake()
    {
        // 버튼 리스너는 1회만 등록한다.
        BindButtonsOnce();
    }

    private void OnEnable()
    {
        // 패널이 다시 켜질 때 현재 저장 데이터를 기준으로 UI를 강제로 갱신한다.
        ForceRefresh();
    }

    /*
        버튼 리스너 1회 바인딩

        - OnEnable에서 AddListener를 반복하면 리스너가 누적될 수 있으므로,
          Awake에서 1회만 등록하도록 한다.
    */
    private void BindButtonsOnce()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (buyButton != null) buyButton.onClick.AddListener(BuyBoostUnlock);
        if (speedUpButton != null) speedUpButton.onClick.AddListener(UpgradeSpeed);
        if (timeUpButton != null) timeUpButton.onClick.AddListener(UpgradeTime);
    }

    /*
        강제 갱신

        - lastXXX 캐시를 무효화하여 RefreshFromSave가 반드시 UI를 갱신하도록 만든다.
        - 패널 재오픈/데이터 리로드 등에서 UI가 최신 상태로 보이도록 한다.
    */
    private void ForceRefresh()
    {
        lastUnlocked = !lastUnlocked;
        lastGold = long.MinValue;
        lastSpeedPrice = long.MinValue;
        lastTimePrice = long.MinValue;
        lastBoostSpeed = float.NaN;
        lastBoostTime = float.NaN;

        RefreshFromSave();
    }

    /*
        저장 데이터 기반 UI 갱신

        - SaveManager의 현재 상태(골드/해금/가격/강화 수치)를 읽는다.
        - 이전에 반영한 값과 완전히 동일하면 UI 갱신을 스킵한다.
    */
    private void RefreshFromSave()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (!TryGetBoost(sm, out var b)) return;

        bool unlocked = sm.IsBoostUnlocked();
        long gold = sm.GetGold();
        float boostSpeed = sm.GetBoostSpeed();
        float boostTime = sm.GetBoostTime();

        // 완전히 동일한 상태면 갱신을 생략한다.
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

        // 다음 비교를 위해 캐시를 업데이트한다.
        lastUnlocked = unlocked;
        lastGold = gold;
        lastSpeedPrice = b.boostSpeedPrice;
        lastTimePrice = b.boostTimePrice;
        lastBoostSpeed = boostSpeed;
        lastBoostTime = boostTime;
    }

    /*
        해금 여부에 따라 패널 표시를 전환한다.
        - Locked: 해금 버튼/안내
        - Main: 강화 UI
    */
    private void ApplyUI(bool unlocked)
    {
        if (panelBoost_Locked != null && panelBoost_Locked.activeSelf == unlocked)
            panelBoost_Locked.SetActive(!unlocked);

        if (panelBoost_Main != null && panelBoost_Main.activeSelf != unlocked)
            panelBoost_Main.SetActive(unlocked);
    }

    /*
        강화 UI 갱신

        - 가격 텍스트 표시
        - 설명 텍스트 표시(현재 값 포함)
        - 버튼 interactable을 "해금 여부 + 골드 충분 + 상한 여부"로 결정한다.
    */
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

        // 속도 강화: 해금 + 골드 충분
        if (speedUpButton != null)
            speedUpButton.interactable = unlocked && gold >= speedPrice;

        // 시간 강화: 해금 + 상한 미도달 + 골드 충분
        if (timeUpButton != null)
            timeUpButton.interactable = unlocked && !timeCapReached && gold >= timePrice;

        // 해금 버튼: 미해금 + 골드 충분
        if (buyButton != null)
            buyButton.interactable = !unlocked && gold >= unlockPrice;
    }

    /*
        부스터 해금 구매

        - 조건: 미해금 상태 + 골드 충분
        - 처리: 골드 차감, 해금 플래그 저장, 관련 미션 조건 완료 처리, UI 갱신
    */
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

        // 미션 시스템과 연동(해금형 미션 완료 처리)
        if (MissionProgressManager.Instance != null)
            MissionProgressManager.Instance.SetUnlocked("boost_unlock", true);

        RefreshFromSave();
    }

    /*
        부스트 속도 강화

        - 조건: 해금 + 골드 충분
        - 처리: 골드 차감 -> boostSpeed + 25 -> 가격 2배 -> 저장 -> UI 갱신
    */
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

        // 다음 업그레이드 가격 증가(성장 곡선)
        b.boostSpeedPrice *= 2;
        sm.Save();

        RefreshFromSave();
    }

    /*
        부스트 시간 강화

        - TIME_CAP까지 증가하되, 상한 도달 시 버튼을 비활성화한다.
        - 증가 방식: 현재 시간의 1.25배, 이후 TIME_CAP로 클램프한다.
    */
    private void UpgradeTime()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;
        if (!sm.IsBoostUnlocked()) return;

        if (!TryGetBoost(sm, out var b)) return;

        float cur = sm.GetBoostTime();

        // 상한에 도달한 경우 값 보정 후 UI만 갱신한다.
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

        // 다음 업그레이드 가격 증가(성장 곡선)
        b.boostTimePrice *= 2;
        sm.Save();

        RefreshFromSave();
    }

    /*
        SaveData 내 Boost 데이터 참조를 안전하게 얻는다.
    */
    private bool TryGetBoost(SaveManager sm, out SaveData.Boost boost)
    {
        boost = null;
        if (sm == null || sm.Data == null || sm.Data.boost == null) return false;
        boost = sm.Data.boost;
        return true;
    }

    /*
        사운드 재생

        - SoundManager의 설정에 따라 mute를 적용한다.
        - 버튼 클릭 시 피드백을 제공한다.
    */
    private void PlaySfx()
    {
        if (sfx == null) return;

        SoundManager snd = SoundManager.Instance;
        if (snd != null) sfx.mute = !snd.IsSfxOn();

        sfx.Play();
    }
}
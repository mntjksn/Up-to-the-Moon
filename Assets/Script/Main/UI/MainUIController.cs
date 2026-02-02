using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Storage Full Blink")]
    [SerializeField] private float storageBlinkInterval = 0.5f;

    [Header("Storage")]
    [SerializeField] private TextMeshProUGUI storageText;

    [Header("Km")]
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private TextMeshProUGUI kmText;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("BalckHole")]
    [SerializeField] private TextMeshProUGUI incomeText;
    [SerializeField] private TextMeshProUGUI storagemaxText;

    [Header("Boost Panel Root")]
    [SerializeField] private GameObject boostPanel;

    [Header("Boost UI")]
    [SerializeField] private TextMeshProUGUI boostSpeedText;     // "부스터 추가 속도 : 150%"
    [SerializeField] private TextMeshProUGUI boostTimeText;      // "부스터 지속 시간 : 35.55초"
    [SerializeField] private TextMeshProUGUI boostCoolPercentText; // "쿨타임 35%"
    [SerializeField] private Slider boostCoolSlider;             // 슬라이더

    [Header("Boost Ref (optional)")]
    [SerializeField] private BoostController boostController;    // 안 넣어도 자동으로 찾게 해둠

    private float speedMultiplier = 1f;
    private float currentSpeed;

    private Coroutine storageBlinkRoutine;
    private Color storageOriginalColor;

    private void Start()
    {
        if (SaveManager.Instance != null)
            currentSpeed = SaveManager.Instance.GetSpeed() * speedMultiplier;

        if (boostController == null)
            boostController = FindObjectOfType<BoostController>(true); // 비활성 포함

        if (storageText != null)
            storageOriginalColor = storageText.color;
    }

    private void Update()
    {
        if (SaveManager.Instance == null) return;

        var data = SaveManager.Instance.Data;
        if (data == null || data.resources == null) return;

        // 목표 속도
        float targetSpeed = SaveManager.Instance.GetSpeed() * speedMultiplier;

        // 부드럽게 변화
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 1.5f);

        // km 누적
        SaveManager.Instance.AddKm(currentSpeed * Time.deltaTime);

        // 값 가져오기
        float km = SaveManager.Instance.GetKm();
        long gold = SaveManager.Instance.GetGold();

        MissionProgressManager.Instance?.SetValue("boost_speed", SaveManager.Instance.GetBoostSpeed());
        MissionProgressManager.Instance?.SetValue("boost_time", SaveManager.Instance.GetBoostTime());

        long total = 0;
        for (int i = 0; i < data.resources.Length; i++)
            total += data.resources[i];


        if (goldText.text != null)
            goldText.text = $"{FormatKoreanNumber(gold)}원";

        if (storageText.text != null)
            storageText.text = $"{FormatKoreanNumber(total)}개";

        if (stateText != null && BackgroundManager.Instance != null)
        {
            var bg = BackgroundManager.Instance.GetBackgroundByKm(km);

            if (bg != null)
                stateText.text = $"현재 지역 : {bg.name}";
            else
                stateText.text = $"현재 지역 : -";
        }

        // 천 단위 콤마 적용
        if (kmText != null)
            kmText.text = $"현재 고도 : {km.ToString("N2")} Km";

        if (speedText != null)
            speedText.text = $"현재 속도 : {currentSpeed.ToString("N2")} Km / s";

        if (incomeText.text != null)
            incomeText.text = $"현재 수급 속도 : {SaveManager.Instance.GetIncome().ToString("N1")}개 / s";

        if (storagemaxText.text != null)
            storagemaxText.text = $"최대 적재량 : {FormatKoreanNumber(SaveManager.Instance.Data.blackHole.BlackHoleStorageMax)}개";
        CheckStorageBlink(total);

        RefreshBoostUI();
    }

    private void RefreshBoostUI()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null || sm.Data.boost == null)
        {
            if (boostPanel != null) boostPanel.SetActive(false);
            return;
        }

        var b = sm.Data.boost;
        bool unlocked = b.boostUnlock;

        if (boostPanel != null) boostPanel.SetActive(unlocked);
        if (!unlocked) return;

        // 상단 고정 정보
        if (boostSpeedText != null)
            boostSpeedText.text = $"부스터 추가 속도 : {b.boostSpeed:N0}%";

        if (boostTimeText != null)
            boostTimeText.text = $"부스터 지속 시간 : {b.boostTime:0.##}초";

        // 컨트롤러 없으면 기본 UI만
        if (boostController == null)
            boostController = FindObjectOfType<BoostController>(true);

        bool boosting = (boostController != null) && boostController.IsBoosting();

        if (boosting)
        {
            // ===== 부스트 지속시간 모드 =====
            float totalDur = Mathf.Max(0.01f, Mathf.Clamp(b.boostTime, 0f, 45f));
            float remainDur = boostController.GetBoostRemaining();
            remainDur = Mathf.Clamp(remainDur, 0f, totalDur);

            // 슬라이더: 남은 시간이 줄어드는 게이지
            if (boostCoolSlider != null)
            {
                boostCoolSlider.minValue = 0f;
                boostCoolSlider.maxValue = totalDur;
                boostCoolSlider.value = remainDur;
            }

            // 퍼센트 텍스트도 "지속"으로 바꾸고 싶으면
            if (boostCoolPercentText != null)
            {
                float percent = (remainDur / totalDur) * 100f;
                boostCoolPercentText.text = $"지속 {percent:0}%";
            }
        }
        else
        {
            // ===== 쿨타임 모드 =====
            float totalCool = Mathf.Max(0.01f, b.boostCoolTime);
            float remainCool = 0f;

            if (boostController != null)
                remainCool = boostController.GetCooldownRemaining();

            remainCool = Mathf.Clamp(remainCool, 0f, totalCool);

            // 슬라이더: 남은 쿨타임이 줄어드는 게이지
            if (boostCoolSlider != null)
            {
                boostCoolSlider.minValue = 0f;
                boostCoolSlider.maxValue = totalCool;
                boostCoolSlider.value = remainCool;
            }

            if (boostCoolPercentText != null)
            {
                float percent = (remainCool / totalCool) * 100f;
                boostCoolPercentText.text = $"쿨타임 {percent:0}%";
            }
        }
    }

    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";

        // 음수도 안전하게
        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;                 // 10^4
        const ulong EOK = 100_000_000UL;            // 10^8
        const ulong JO = 1_000_000_000_000UL;      // 10^12
        const ulong GYEONG = 10_000_000_000_000_000UL; // 10^16

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(jo).Append("조");
        }
        if (eok > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(eok).Append("억");
        }
        if (man > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(man).Append("만");
        }
        if (rest > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(rest);
        }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }

    private void CheckStorageBlink(long totalStorage)
    {
        long maxCap = SaveManager.Instance.Data.blackHole.BlackHoleStorageMax;
        bool isFull = totalStorage >= maxCap;

        if (isFull)
            StartStorageBlink();
        else
            StopStorageBlink();
    }

    private void StartStorageBlink()
    {
        if (storageText == null) return;

        if (storageBlinkRoutine == null)
            storageBlinkRoutine = StartCoroutine(StorageBlink());
    }

    private void StopStorageBlink()
    {
        if (storageBlinkRoutine != null)
        {
            StopCoroutine(storageBlinkRoutine);
            storageBlinkRoutine = null;
        }

        if (storageText != null)
        {
            storageText.enabled = true;
            storageText.color = storageOriginalColor; // 원래색 복구
        }
    }

    private IEnumerator StorageBlink()
    {
        while (true)
        {
            storageText.color = Color.red;
            storageText.enabled = true;
            yield return new WaitForSeconds(storageBlinkInterval);

            storageText.color = storageOriginalColor;
            storageText.enabled = true;
            yield return new WaitForSeconds(storageBlinkInterval);
        }
    }
}

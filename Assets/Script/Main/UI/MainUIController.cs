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

    [Header("BlackHole")]
    [SerializeField] private TextMeshProUGUI incomeText;
    [SerializeField] private TextMeshProUGUI storagemaxText;

    [Header("Boost Panel Root")]
    [SerializeField] private GameObject boostPanel;

    [Header("Boost UI")]
    [SerializeField] private TextMeshProUGUI boostSpeedText;
    [SerializeField] private TextMeshProUGUI boostTimeText;
    [SerializeField] private TextMeshProUGUI boostCoolPercentText;
    [SerializeField] private Slider boostCoolSlider;

    [Header("Boost Ref (optional)")]
    [SerializeField] private BoostController boostController;

    private Coroutine storageBlinkRoutine;
    private Color storageOriginalColor;

    private void Start()
    {
        if (boostController == null)
            boostController = FindObjectOfType<BoostController>(true);

        if (storageText != null)
            storageOriginalColor = storageText.color;

        RefreshStaticUIOnce();
        RefreshDynamicUI(); // 시작 프레임에 바로 표시
    }

    private void Update()
    {
        RefreshDynamicUI();
        RefreshBoostUI();
    }

    private void RefreshStaticUIOnce()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null) return;

        if (storagemaxText != null)
            storagemaxText.text = $"최대 적재량 : {NumberFormatter.FormatKorean(sm.GetStorageMax())}개";
    }

    private void RefreshDynamicUI()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null) return;

        float km = sm.GetKm();
        long gold = sm.GetGold();

        long totalStorage = sm.GetStorageUsed();
        long maxStorage = sm.GetStorageMax();

        if (goldText != null)
            goldText.text = $"{NumberFormatter.FormatKorean(gold)}원";

        if (storageText != null)
            storageText.text = $"{NumberFormatter.FormatKorean(totalStorage)}개";

        if (stateText != null && BackgroundManager.Instance != null && BackgroundManager.Instance.IsLoaded)
        {
            var bg = BackgroundManager.Instance.GetByKm(km);
            stateText.text = (bg != null) ? $"현재 지역 : {bg.name}" : "현재 지역 : -";
        }

        if (kmText != null)
            kmText.text = $"현재 고도 : {km:N2} Km";

        if (speedText != null)
            speedText.text = $"현재 속도 : {sm.GetSpeed():N2} Km / s";

        if (incomeText != null)
            incomeText.text = $"현재 수급 속도 : {sm.GetIncome():N1}개 / s";

        if (storagemaxText != null)
            storagemaxText.text = $"최대 적재량 : {NumberFormatter.FormatKorean(maxStorage)}개";

        CheckStorageBlink(totalStorage, maxStorage);

        // boost_speed / boost_time는 SaveManager SetBoostSpeed/SetBoostTime에서 이미 미션 반영 중이었지?
        // 여기서 매프레임 SetValue 하면 불필요해서 제거하는 게 맞음.
    }

    private void RefreshBoostUI()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data?.boost == null)
        {
            if (boostPanel != null) boostPanel.SetActive(false);
            return;
        }

        var b = sm.Data.boost;
        bool unlocked = b.boostUnlock;

        if (boostPanel != null) boostPanel.SetActive(unlocked);
        if (!unlocked) return;

        if (boostSpeedText != null)
            boostSpeedText.text = $"부스터 추가 속도 : {b.boostSpeed:N0}%";

        if (boostTimeText != null)
            boostTimeText.text = $"부스터 지속 시간 : {b.boostTime:0.##}초";

        if (boostController == null)
            boostController = FindObjectOfType<BoostController>(true);

        bool boosting = (boostController != null) && boostController.IsBoosting();

        if (boosting)
        {
            float totalDur = Mathf.Max(0.01f, Mathf.Clamp(b.boostTime, 0f, 45f));
            float remainDur = Mathf.Clamp(boostController.GetBoostRemaining(), 0f, totalDur);

            if (boostCoolSlider != null)
            {
                boostCoolSlider.minValue = 0f;
                boostCoolSlider.maxValue = totalDur;
                boostCoolSlider.value = remainDur;
            }

            if (boostCoolPercentText != null)
            {
                float percent = (remainDur / totalDur) * 100f;
                boostCoolPercentText.text = $"지속 {percent:0}%";
            }
        }
        else
        {
            float totalCool = Mathf.Max(0.01f, b.boostCoolTime);
            float remainCool = 0f;

            if (boostController != null)
                remainCool = boostController.GetCooldownRemaining();

            remainCool = Mathf.Clamp(remainCool, 0f, totalCool);

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

    private void CheckStorageBlink(long totalStorage, long maxStorage)
    {
        bool isFull = (maxStorage > 0) && (totalStorage >= maxStorage);

        if (isFull) StartStorageBlink();
        else StopStorageBlink();
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
            storageText.color = storageOriginalColor;
        }
    }

    private IEnumerator StorageBlink()
    {
        while (true)
        {
            if (storageText == null) yield break;

            storageText.color = Color.red;
            storageText.enabled = true;
            yield return new WaitForSeconds(storageBlinkInterval);

            storageText.color = storageOriginalColor;
            storageText.enabled = true;
            yield return new WaitForSeconds(storageBlinkInterval);
        }
    }
}
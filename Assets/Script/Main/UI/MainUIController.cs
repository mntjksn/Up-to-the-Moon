using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    MainUIController

    [역할]
    - 메인 화면의 주요 UI(골드/거리/속도/저장소/블랙홀/부스트)를 갱신한다.
    - 플레이 진행(거리 증가)을 관리하고, 일정 주기로만 SaveManager에 반영하여 저장 비용을 줄인다.

    [설계 의도]
    1) UI 표시 보간
       - 실제 속도 값 변화가 즉시 반영되면 화면이 튀어 보일 수 있어 Lerp로 부드럽게 표시한다.
    2) 저장 최적화
       - km를 매 프레임 SaveManager에 반영하면 Save() 호출이 과도해질 수 있어,
         로컬 누적(kmAcc) 후 kmSaveInterval 주기로만 AddKm를 호출한다.
    3) 상태 기반 UI
       - 저장소가 가득 차면 깜빡임 경고로 사용자에게 상태를 명확히 전달한다.
       - 부스트 중/쿨타임 상태에 따라 슬라이더와 텍스트 표시를 분기한다.
*/
public class MainUIController : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Storage Full Blink")]
    // 저장소가 가득 찼을 때 깜빡이는 주기(초)
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
    // 부스트가 해금되지 않았으면 패널 자체를 숨긴다.
    [SerializeField] private GameObject boostPanel;

    [Header("Boost UI")]
    [SerializeField] private TextMeshProUGUI boostSpeedText;
    [SerializeField] private TextMeshProUGUI boostTimeText;
    [SerializeField] private TextMeshProUGUI boostCoolPercentText;
    [SerializeField] private Slider boostCoolSlider;

    [Header("Boost Ref (optional)")]
    // 씬에 존재하는 BoostController를 참조한다(없으면 런타임에 탐색).
    [SerializeField] private BoostController boostController;

    [Header("Move/Save")]
    // 실제 이동에 곱할 배율(게임 밸런스/연출용)
    [SerializeField] private float speedMultiplier = 1f;

    // UI 표시 보간 속도(값이 커질수록 목표값에 빨리 수렴한다)
    [SerializeField] private float uiLerpSpeed = 3f;

    // km를 SaveManager에 반영하는 주기(초): 저장 호출 빈도를 줄이기 위한 옵션
    [SerializeField] private float kmSaveInterval = 0.25f;

    // UI에 표시할 속도(보간된 값)
    private float currentSpeed;

    // SaveManager 반영 전까지 누적하는 거리
    private float kmAcc;

    // km 저장 타이머
    private float kmSaveTimer;

    // 저장소 가득 참 경고(깜빡임) 코루틴
    private Coroutine storageBlinkRoutine;
    private Color storageOriginalColor;

    private void Start()
    {
        // 인스펙터에서 지정되지 않았으면 런타임에 1회 탐색한다.
        if (boostController == null)
            boostController = FindObjectOfType<BoostController>(true);

        // 깜빡임 연출 종료 시 원래 색으로 복귀하기 위해 기본 색을 저장한다.
        if (storageText != null)
            storageOriginalColor = storageText.color;

        var sm = SaveManager.Instance;
        if (sm == null) return;

        // Start에서 저장된 speed를 다시 덮어쓰지 않고, 표시 기준만 맞춘다.
        currentSpeed = sm.GetSpeed() * speedMultiplier;

        // 변하지 않는 값은 1회만 갱신한다.
        RefreshStaticUIOnce();

        // 시작 프레임에 UI를 즉시 한번 갱신한다.
        RefreshDynamicUI(currentSpeed);
        RefreshBoostUI();
    }

    private void Update()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        // 목표 속도(저장된 speed * 배율)
        float targetSpeed = sm.GetSpeed() * speedMultiplier;

        // UI 표시만 부드럽게(게임 플레이 값 자체를 변경하지 않는다)
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * uiLerpSpeed);

        // 이동 거리는 로컬에서 누적하고, SaveManager에는 주기적으로만 반영한다.
        float deltaKm = currentSpeed * Time.deltaTime;
        kmAcc += deltaKm;

        kmSaveTimer += Time.deltaTime;
        if (kmSaveTimer >= kmSaveInterval)
        {
            // 여기서만 SaveManager에 반영되므로 Save() 호출 빈도를 줄일 수 있다.
            sm.AddKm(kmAcc);
            kmAcc = 0f;
            kmSaveTimer = 0f;
        }

        RefreshDynamicUI(currentSpeed);
        RefreshBoostUI();
    }

    private void OnDisable()
    {
        // 패널이 꺼질 때 남은 누적 km를 반영하여 거리 손실을 방지한다.
        var sm = SaveManager.Instance;
        if (sm != null && kmAcc != 0f)
        {
            sm.AddKm(kmAcc);
            kmAcc = 0f;
            kmSaveTimer = 0f;
        }

        // 비활성화 시 깜빡임이 남지 않도록 정리한다.
        StopStorageBlink();
    }

    /*
        변하지 않는 UI(정적 값) 1회 갱신

        - 예: 최대 적재량 같이 자주 변하지 않는 값은 매 프레임 갱신하지 않는다.
        - 필요 시 업그레이드 이벤트가 있을 때만 다시 갱신하도록 확장할 수 있다.
    */
    private void RefreshStaticUIOnce()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null) return;

        if (storagemaxText != null)
            storagemaxText.text = $"최대 적재량 : {NumberFormatter.FormatKorean(sm.GetStorageMax())}개";
    }

    /*
        매 프레임 갱신되는 UI

        - 골드/저장소/지역명/거리/속도/수급량 등을 표시한다.
        - 저장소 가득 참 상태를 체크하여 경고 연출을 제어한다.
    */
    private void RefreshDynamicUI(float displaySpeed)
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null) return;

        float km = sm.GetKm();     // 저장된 km 기준(주기적으로만 업데이트됨)
        long gold = sm.GetGold();

        long totalStorage = sm.GetStorageUsed();
        long maxStorage = sm.GetStorageMax();

        if (goldText != null)
            goldText.text = $"{NumberFormatter.FormatKorean(gold)}원";

        if (storageText != null)
            storageText.text = $"{NumberFormatter.FormatKorean(totalStorage)}개";

        // 지역명은 BackgroundManager의 km 구간 데이터를 사용한다.
        if (stateText != null && BackgroundManager.Instance != null && BackgroundManager.Instance.IsLoaded)
        {
            var bg = BackgroundManager.Instance.GetByKm(km);
            stateText.text = (bg != null) ? $"현재 지역 : {bg.name}" : "현재 지역 : -";
        }

        if (kmText != null)
            kmText.text = $"현재 고도 : {km:N2} Km";

        if (speedText != null)
            speedText.text = $"현재 속도 : {displaySpeed:N2} Km / s";

        if (incomeText != null)
            incomeText.text = $"현재 수급 속도 : {sm.GetIncome():N1}개 / s";

        // 최대 적재량은 업그레이드로 바뀔 수 있으므로 동적 갱신에 포함한다.
        if (storagemaxText != null)
            storagemaxText.text = $"최대 적재량 : {NumberFormatter.FormatKorean(maxStorage)}개";

        CheckStorageBlink(totalStorage, maxStorage);
    }

    /*
        부스트 UI 갱신

        - 부스트 해금 여부에 따라 패널을 표시/숨김 처리한다.
        - 부스트가 진행 중이면 "지속 시간"을, 아니면 "쿨타임"을 슬라이더로 표시한다.
        - BoostController에서 남은 시간을 읽어와 실시간 진행률을 표현한다.
    */
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

        // 참조가 비어있으면 필요 시점에만 탐색한다.
        if (boostController == null)
            boostController = FindObjectOfType<BoostController>(true);

        bool boosting = (boostController != null) && boostController.IsBoosting();

        if (boosting)
        {
            // 부스트 진행 중: 남은 지속 시간을 표시한다.
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
            // 부스트 미사용/쿨타임: 남은 쿨타임을 표시한다.
            float totalCool = Mathf.Max(0.01f, b.boostCoolTime);
            float remainCool = (boostController != null) ? boostController.GetCooldownRemaining() : 0f;
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

    /*
        저장소 경고 연출 제어

        - 저장소가 최대치에 도달하면 깜빡임 코루틴을 시작한다.
        - 여유가 생기면 코루틴을 중단하고 원래 상태로 복구한다.
    */
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

    /*
        저장소 가득 참 깜빡임 연출

        - 색상을 빨강/원래색으로 교차하여 경고를 강조한다.
        - 텍스트 enable은 항상 true로 유지하여 표시 누락을 방지한다.
    */
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
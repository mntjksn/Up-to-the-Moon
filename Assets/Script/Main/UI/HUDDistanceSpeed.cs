using TMPro;
using UnityEngine;

public class HUDDistanceSpeed : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI kmText;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Speed")]
    [SerializeField] private float baseSpeed = 0.05f;
    [SerializeField] private float speedMultiplier = 1f;

    private float currentSpeed;

    private void Start()
    {
        // 초기 목표 속도 저장
        float initSpeed = baseSpeed * speedMultiplier;
        currentSpeed = initSpeed;

        if (SaveManager.Instance != null)
            SaveManager.Instance.SetSpeed(initSpeed);
    }

    private void Update()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        // 목표 속도 계산
        float targetSpeed = baseSpeed * speedMultiplier;

        // SaveManager에 목표 속도 반영
        sm.SetSpeed(targetSpeed);

        // 부드럽게 표시용 속도 보간
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 3f);

        // 이동 거리 누적
        sm.AddKm(currentSpeed * Time.deltaTime);

        float km = sm.GetKm();

        // 미션용 reach_value 갱신
        MissionProgressManager.Instance?.SetValue("player_speed", targetSpeed);
        MissionProgressManager.Instance?.SetValue("distance_km", km);

        // UI 표시
        if (kmText != null)
            kmText.text = $"현재 고도 : {km:N0} Km";

        if (speedText != null)
            speedText.text = $"현재 속도 : {currentSpeed:N2} Km / s";
    }
}
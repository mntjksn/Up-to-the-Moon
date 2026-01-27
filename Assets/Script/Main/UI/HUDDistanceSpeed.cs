using TMPro;
using UnityEngine;

public class HUDDistanceSpeed : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI kmText;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Speed")]
    [SerializeField] private float baseSpeed = 0.05f;
    [SerializeField] private float speedMultiplier = 1f;

    private float currentSpeed;

    private void Start()
    {
        currentSpeed = baseSpeed * speedMultiplier;
    }

    private void Update()
    {
        if (SaveManager.Instance == null) return;

        // 목표 속도
        float targetSpeed = baseSpeed * speedMultiplier;

        // 부드럽게 변화
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 3f);

        // km 누적
        SaveManager.Instance.AddKm(currentSpeed * Time.deltaTime);

        // 값 가져오기
        float km = SaveManager.Instance.GetKm();

        // 천 단위 콤마 적용
        if (kmText != null)
            kmText.text = $"현재 고도 : {km.ToString("N0")} Km";

        if (speedText != null)
            speedText.text = $"현재 속도 : {currentSpeed.ToString("N2")} Km / s";
    }

    // 업그레이드에서 호출
    public void SetSpeedMultiplier(float m)
    {
        speedMultiplier = Mathf.Max(0f, m);
    }
}
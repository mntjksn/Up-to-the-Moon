using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MainUIController : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Storage")]
    [SerializeField] private TextMeshProUGUI storageText;

    [Header("Km")]
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private TextMeshProUGUI kmText;
    [SerializeField] private TextMeshProUGUI speedText;

    private float speedMultiplier = 1f;
    private float currentSpeed;

    private void Start()
    {
        currentSpeed = SaveManager.Instance.GetSpeed() * speedMultiplier;
    }

    private void Update()
    {
        if (SaveManager.Instance == null) return;

        var data = SaveManager.Instance.Data;
        if (data == null || data.resources == null) return;

        // 목표 속도
        float targetSpeed = SaveManager.Instance.GetSpeed() * speedMultiplier;

        // 부드럽게 변화
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 3f);

        // km 누적
        SaveManager.Instance.AddKm(currentSpeed * Time.deltaTime);

        // 값 가져오기
        float km = SaveManager.Instance.GetKm();

        long gold = SaveManager.Instance.GetGold();

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
            kmText.text = $"현재 고도 : {km.ToString("N0")} Km";

        if (speedText != null)
            speedText.text = $"현재 속도 : {currentSpeed.ToString("N2")} Km / s";
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

    // 업그레이드에서 호출
    public void SetSpeedMultiplier(float m)
    {
        speedMultiplier = Mathf.Max(0f, m);
    }
}

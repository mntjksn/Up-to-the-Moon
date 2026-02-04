using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MissionClearBlink : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI clearText;

    [Header("Fade Blink")]
    [SerializeField] private float cycleSeconds = 1.2f;
    [SerializeField] private float minAlpha = 0.15f;
    [SerializeField] private float maxAlpha = 1f;

    private Coroutine blinkRoutine;

    private void Awake()
    {
        if (clearText == null)
            clearText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        MissionProgressManager.OnMissionStateChanged -= Refresh;
        MissionProgressManager.OnMissionStateChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        MissionProgressManager.OnMissionStateChanged -= Refresh;
        StopBlinkAndHide();
    }

    private void OnDestroy()
    {
        MissionProgressManager.OnMissionStateChanged -= Refresh;
    }

    public void Refresh()
    {
        if (HasAnyClaimableMission())
            StartBlinkAndShow();
        else
            StopBlinkAndHide();
    }

    private bool HasAnyClaimableMission()
    {
        MissionDataManager mdm = MissionDataManager.Instance;
        if (mdm == null) return false;

        List<MissionItem> list = mdm.MissionItem;
        if (list == null || list.Count == 0) return false;

        // 한 번의 루프로:
        // - easy/normal 존재 여부 + 전부 보상수령 여부 계산
        // - tier 허용 범위 결정 후 claimable 탐색까지 처리
        bool hasEasy = false;
        bool easyAllClaimed = true;

        bool hasNormal = false;
        bool normalAllClaimed = true;

        // 1차: easy/normal 상태 파악
        for (int i = 0; i < list.Count; i++)
        {
            MissionItem m = list[i];
            if (m == null) continue;

            if (m.tier == "easy")
            {
                hasEasy = true;
                if (!m.rewardClaimed) easyAllClaimed = false;
            }
            else if (m.tier == "normal")
            {
                hasNormal = true;
                if (!m.rewardClaimed) normalAllClaimed = false;
            }
        }

        int maxTier;
        if (hasEasy && !easyAllClaimed) maxTier = 0;
        else if (hasNormal && !normalAllClaimed) maxTier = 1;
        else maxTier = 2;

        // 2차: claimable 탐색
        for (int i = 0; i < list.Count; i++)
        {
            MissionItem m = list[i];
            if (m == null) continue;

            if (!IsTierAllowed(m.tier, maxTier)) continue;

            if (m.isCompleted && !m.rewardClaimed)
                return true;
        }

        return false;
    }

    private bool IsTierAllowed(string tier, int maxTier)
    {
        if (tier == "easy") return true;
        if (tier == "normal") return maxTier >= 1;
        if (tier == "hard") return maxTier >= 2;
        return false;
    }

    private void StartBlinkAndShow()
    {
        if (clearText == null) return;

        if (!clearText.enabled)
            clearText.enabled = true;

        SetAlpha(maxAlpha);

        if (blinkRoutine == null)
            blinkRoutine = StartCoroutine(FadeBlink());
    }

    private void StopBlinkAndHide()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (clearText != null)
        {
            clearText.enabled = false;
            SetAlpha(maxAlpha);
        }
    }

    private IEnumerator FadeBlink()
    {
        float t = 0f;
        float cycle = Mathf.Max(0.01f, cycleSeconds);

        while (true)
        {
            t += Time.unscaledDeltaTime / cycle;
            float p = Mathf.PingPong(t, 1f);

            float a = Mathf.Lerp(minAlpha, maxAlpha, p);
            SetAlpha(a);

            yield return null;
        }
    }

    private void SetAlpha(float a)
    {
        if (clearText == null) return;

        // TMP는 alpha만 바꾸는 헬퍼가 있음(가독성)
        clearText.alpha = a;
    }
}
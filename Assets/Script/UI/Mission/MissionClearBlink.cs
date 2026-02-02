using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class MissionClearBlink : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI clearText;

    [Header("Fade Blink")]
    [SerializeField] private float cycleSeconds = 1.2f; // 한 번 숨쉬는 주기(느리게=값 크게)
    [SerializeField] private float minAlpha = 0.15f;    // 완전 꺼지지 않게
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
        bool shouldBlink = HasAnyClaimableMission();
        if (shouldBlink) StartBlinkAndShow();
        else StopBlinkAndHide();
    }

    private bool HasAnyClaimableMission()
    {
        var list = MissionDataManager.Instance?.MissionItem;
        if (list == null) return false;

        int maxTier = GetMaxUnlockedTier(list);
        // 0=easy만, 1=easy+normal, 2=easy+normal+hard

        foreach (var m in list)
        {
            if (!IsTierAllowed(m.tier, maxTier)) continue;

            if (m.isCompleted && !m.rewardClaimed)
                return true;
        }
        return false;
    }

    private int GetMaxUnlockedTier(List<MissionItem> missions)
    {
        // 너 MissionManager의 잠금 규칙 그대로 복제
        bool easyAllClaimed = missions.Where(m => m.tier == "easy").All(m => m.rewardClaimed);
        bool normalAllClaimed = missions.Where(m => m.tier == "normal").All(m => m.rewardClaimed);

        if (!easyAllClaimed) return 0;          // 초급만
        if (!normalAllClaimed) return 1;        // 중급까지
        return 2;                                // 고급까지
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
            SetAlpha(maxAlpha); // 다음에 켤 때 이상한 알파로 시작 방지
        }
    }

    private IEnumerator FadeBlink()
    {
        float t = 0f;

        while (true)
        {
            // 0~1 왕복
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, cycleSeconds);
            float p = Mathf.PingPong(t, 1f);

            // minAlpha~maxAlpha로 보간
            float a = Mathf.Lerp(minAlpha, maxAlpha, p);
            SetAlpha(a);

            yield return null; // 매 프레임 부드럽게
        }
    }

    private void SetAlpha(float a)
    {
        if (clearText == null) return;
        var c = clearText.color;
        c.a = a;
        clearText.color = c;
    }
}
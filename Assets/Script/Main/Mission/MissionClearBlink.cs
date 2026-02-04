using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*
    MissionClearBlink

    [역할]
    - 수령 가능한 미션이 존재할 경우
      "CLEAR" 텍스트를 깜빡이는 애니메이션으로 표시한다.
    - 미션 상태 변경 이벤트를 구독하여
      UI를 실시간으로 갱신한다.

    [설계 의도]
    1) 이벤트 기반 UI 갱신
       - Update에서 매 프레임 체크하지 않고
         MissionProgressManager.OnMissionStateChanged 이벤트에 반응하여
         필요한 경우에만 UI를 갱신한다.

    2) 단계(Tier) 제한 구조
       - easy → normal → hard 순서로 해금되는 구조를 고려하여
         현재 허용된 티어 범위 내에서만
         수령 가능한 미션을 검사한다.

    3) 코루틴 기반 깜빡임 효과
       - 알파값을 PingPong으로 보간하여
         부드러운 페이드 인/아웃 효과를 구현한다.
*/
public class MissionClearBlink : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI clearText;   // CLEAR 텍스트

    [Header("Fade Blink")]
    [SerializeField] private float cycleSeconds = 1.2f;  // 한 번 깜빡임 주기
    [SerializeField] private float minAlpha = 0.15f;     // 최소 알파
    [SerializeField] private float maxAlpha = 1f;        // 최대 알파

    private Coroutine blinkRoutine;

    private void Awake()
    {
        // 인스펙터에서 지정 안 했을 경우 자동 탐색
        if (clearText == null)
            clearText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // 중복 구독 방지
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

    /*
        미션 상태 변경 시 호출

        - 수령 가능한 미션이 있으면 깜빡임 시작
        - 없으면 숨김
    */
    public void Refresh()
    {
        if (HasAnyClaimableMission())
            StartBlinkAndShow();
        else
            StopBlinkAndHide();
    }

    /*
        현재 수령 가능한 미션이 하나라도 있는지 검사
    */
    private bool HasAnyClaimableMission()
    {
        MissionDataManager mdm = MissionDataManager.Instance;
        if (mdm == null) return false;

        List<MissionItem> list = mdm.MissionItem;
        if (list == null || list.Count == 0) return false;

        // easy / normal 티어 상태 파악
        bool hasEasy = false;
        bool easyAllClaimed = true;

        bool hasNormal = false;
        bool normalAllClaimed = true;

        // 1차: 티어별 보상 수령 여부 확인
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

        // 현재 허용 가능한 최대 티어 결정
        int maxTier;
        if (hasEasy && !easyAllClaimed) maxTier = 0;
        else if (hasNormal && !normalAllClaimed) maxTier = 1;
        else maxTier = 2;

        // 2차: 허용 티어 내에서 수령 가능 미션 탐색
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

    /*
        티어 허용 여부 검사
    */
    private bool IsTierAllowed(string tier, int maxTier)
    {
        if (tier == "easy") return true;
        if (tier == "normal") return maxTier >= 1;
        if (tier == "hard") return maxTier >= 2;
        return false;
    }

    /*
        깜빡임 시작 및 텍스트 표시
    */
    private void StartBlinkAndShow()
    {
        if (clearText == null) return;

        if (!clearText.enabled)
            clearText.enabled = true;

        SetAlpha(maxAlpha);

        if (blinkRoutine == null)
            blinkRoutine = StartCoroutine(FadeBlink());
    }

    /*
        깜빡임 중지 및 텍스트 숨김
    */
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

    /*
        알파값을 반복 보간하는 코루틴
    */
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

    /*
        TMP 전용 알파 설정
    */
    private void SetAlpha(float a)
    {
        if (clearText == null) return;
        clearText.alpha = a;
    }
}
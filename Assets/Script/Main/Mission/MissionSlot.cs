using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    MissionSlot

    [역할]
    - MissionItem 1개를 UI 슬롯 1개에 바인딩하고,
      진행 상태(완료/보상 수령 여부)에 따라 UI(텍스트/버튼)를 갱신한다.
    - 보상 버튼 클릭 시 골드 지급 + rewardClaimed 처리 + 저장 + UI/이벤트 갱신을 수행한다.

    [설계 의도]
    1) UI 갱신 비용 최소화
       - title/desc/reward 텍스트와 버튼 interactable을 "값이 바뀐 경우에만" 업데이트한다.
       - TMP 텍스트 재할당은 레이아웃/메쉬 리빌드 비용이 크므로 캐시(lastTitle/lastDesc/lastRewardClaimed 등)를 둔다.
       - 상태 갱신은 RefreshStateOnly()로 분리해, 필요한 부분만 갱신한다.

    2) 바인딩 시 1회 초기화 + 이후 Refresh
       - Bind()에서 버튼 리스너를 1회 연결하고, 고정 텍스트를 먼저 세팅한다.
       - 이후 MissionManager에서 Refresh()만 호출해 상태만 빠르게 반영한다.

    3) 안전한 보상 수령 처리
       - (완료 && 미수령) 조건을 통과했을 때만 지급한다.
       - 지급은 SaveManager.AddGold()로 처리하여 저장/이벤트 흐름을 일원화한다.
       - 미션 데이터는 MissionDataManager.SaveToJson()로 저장한다.

    [주의/전제]
    - bound(MissionItem)는 MissionDataManager가 유지하는 런타임 리스트의 참조라고 가정한다.
    - rewardClaimed 변경 후 UI만 새로고침하고, 상위 UI 갱신은 NotifyMissionStateChangedUIOnly()로 트리거한다.
*/
public class MissionSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button rewardButton;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    // 현재 슬롯에 바인딩된 미션 데이터(참조)
    private MissionItem bound;

    // ---- UI 캐시(불필요한 TMP/버튼 갱신 방지) ----
    private bool lastRewardClaimed;
    private bool lastCanClaim;
    private long lastRewardGold;
    private string lastTitle;
    private string lastDesc;

    /*
        미션 바인딩
        - 버튼 리스너는 중복 방지를 위해 RemoveAllListeners 후 AddListener
        - title/desc 같은 "거의 고정" 텍스트는 여기서 1회 세팅
        - 상태는 ForceStateRefresh로 강제 반영
    */
    public void Bind(MissionItem mission)
    {
        bound = mission;

        if (rewardButton != null)
        {
            rewardButton.onClick.RemoveAllListeners();
            rewardButton.onClick.AddListener(ClaimReward);
        }

        ApplyStaticTexts();
        ForceStateRefresh();
    }

    /*
        고정 텍스트 반영
        - title/desc/rewardGold는 일반적으로 런타임 중 바뀌지 않으므로
          캐시 비교 후 변경될 때만 반영한다.
    */
    private void ApplyStaticTexts()
    {
        if (bound == null) return;

        if (titleText != null && !string.Equals(lastTitle, bound.title))
        {
            titleText.text = bound.title;
            lastTitle = bound.title;
        }

        if (descText != null && !string.Equals(lastDesc, bound.desc))
        {
            descText.text = bound.desc;
            lastDesc = bound.desc;
        }

        // 보상 금액 캐시(보통 고정)
        lastRewardGold = bound.rewardGold;
    }

    /*
        외부에서 호출되는 Refresh
        - MissionManager가 티어 전환/상태 변경 시 호출
        - 고정 텍스트는 거의 안 바뀌지만, 안전하게 유지(비용 작음)
        - 실제 상태 갱신은 RefreshStateOnly()에서 처리
    */
    public void Refresh()
    {
        if (bound == null) return;

        ApplyStaticTexts();
        RefreshStateOnly();
    }

    /*
        바인딩 직후 강제 상태 갱신
        - 캐시를 의도적으로 무효화해서 UI가 반드시 현재 상태로 맞춰지게 한다.
    */
    private void ForceStateRefresh()
    {
        lastRewardClaimed = !lastRewardClaimed;
        lastCanClaim = !lastCanClaim;
        RefreshStateOnly();
    }

    /*
        상태(UI)만 갱신
        - rewardText: (수령 완료) / (보상 금액)
        - rewardButton: (완료 && 미수령)일 때만 누를 수 있음
    */
    private void RefreshStateOnly()
    {
        if (bound == null) return;

        bool rewardClaimed = bound.rewardClaimed;
        bool canClaim = bound.isCompleted && !bound.rewardClaimed;

        // 보상 텍스트 갱신(변경될 때만)
        if (rewardText != null)
        {
            if (rewardClaimed != lastRewardClaimed)
            {
                if (rewardClaimed)
                    rewardText.text = "수령 완료";
                else
                    rewardText.text = NumberFormatter.FormatKorean(bound.rewardGold) + "원";

                lastRewardClaimed = rewardClaimed;
            }
            else if (!rewardClaimed && bound.rewardGold != lastRewardGold)
            {
                // (예외 대비) 런타임 중 보상 금액이 바뀌는 경우
                rewardText.text = NumberFormatter.FormatKorean(bound.rewardGold) + "원";
                lastRewardGold = bound.rewardGold;
            }
        }

        // 버튼 interactable 갱신(변경될 때만)
        if (rewardButton != null && canClaim != lastCanClaim)
        {
            rewardButton.interactable = canClaim;
            lastCanClaim = canClaim;
        }
    }

    /*
        보상 수령 처리
        - 조건 검사 후 지급/저장/상태 변경 수행
        - SaveManager.AddGold() 내부에서 Save() 및 관련 이벤트 호출을 처리하므로 추가 Save는 생략
        - MissionDataManager.SaveToJson()로 미션 데이터 저장
        - 마지막에 UI 및 상위 UI 갱신 이벤트를 트리거
    */
    private void ClaimReward()
    {
        if (bound == null) return;
        if (!bound.isCompleted) return;
        if (bound.rewardClaimed) return;

        PlaySfx();

        SaveManager sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.AddGold(bound.rewardGold);
            // AddGold 내부 Save면 추가 Save 불필요
        }

        bound.rewardClaimed = true;

        MissionDataManager mdm = MissionDataManager.Instance;
        if (mdm != null) mdm.SaveToJson();

        // 상태만 갱신(전체 Refresh보다 가벼움)
        RefreshStateOnly();

        // 미션창/상단 "완료!!" 같은 UI에 반영되도록 이벤트 발사(디바운스는 ProgressManager가 처리)
        MissionProgressManager mpm = MissionProgressManager.Instance;
        if (mpm != null) mpm.NotifyMissionStateChangedUIOnly();
    }

    /*
        효과음 재생
        - SoundManager 설정에 따라 mute 적용
    */
    private void PlaySfx()
    {
        if (sfx == null) return;

        SoundManager snd = SoundManager.Instance;
        if (snd != null) sfx.mute = !snd.IsSfxOn();

        sfx.Play();
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button rewardButton;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    private MissionItem bound;

    // 캐시(불필요한 TMP/버튼 갱신 방지)
    private bool lastRewardClaimed;
    private bool lastCanClaim;
    private long lastRewardGold;
    private string lastTitle;
    private string lastDesc;

    public void Bind(MissionItem mission)
    {
        bound = mission;

        if (rewardButton != null)
        {
            rewardButton.onClick.RemoveAllListeners();
            rewardButton.onClick.AddListener(ClaimReward);
        }

        // 고정 텍스트는 Bind 때 세팅(Refresh 폭 줄이기)
        ApplyStaticTexts();
        ForceStateRefresh();
    }

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

        // 보상 금액은 대부분 고정이라 캐시
        lastRewardGold = bound.rewardGold;
    }

    // 외부에서 호출되는 Refresh
    public void Refresh()
    {
        if (bound == null) return;

        // title/desc가 런타임에 바뀌는 케이스가 있으면 유지
        // (일반적으로 안 바뀌므로 비용 거의 없음)
        ApplyStaticTexts();
        RefreshStateOnly();
    }

    private void ForceStateRefresh()
    {
        // 캐시 무효화 후 상태 갱신
        lastRewardClaimed = !lastRewardClaimed;
        lastCanClaim = !lastCanClaim;
        RefreshStateOnly();
    }

    private void RefreshStateOnly()
    {
        if (bound == null) return;

        bool rewardClaimed = bound.rewardClaimed;
        bool canClaim = bound.isCompleted && !bound.rewardClaimed;

        // rewardText: 상태에 따라 2가지 중 하나
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
                // 혹시 런타임에 rewardGold가 바뀌는 구조면 대비
                rewardText.text = NumberFormatter.FormatKorean(bound.rewardGold) + "원";
                lastRewardGold = bound.rewardGold;
            }
        }

        if (rewardButton != null && canClaim != lastCanClaim)
        {
            rewardButton.interactable = canClaim;
            lastCanClaim = canClaim;
        }
    }

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

        // 상태만 갱신
        RefreshStateOnly();

        MissionProgressManager mpm = MissionProgressManager.Instance;
        if (mpm != null) mpm.NotifyMissionStateChangedUIOnly();
    }

    private void PlaySfx()
    {
        if (sfx == null) return;

        SoundManager snd = SoundManager.Instance;
        if (snd != null) sfx.mute = !snd.IsSfxOn();

        sfx.Play();
    }
}
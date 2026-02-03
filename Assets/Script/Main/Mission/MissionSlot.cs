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

    public void Bind(MissionItem mission)
    {
        bound = mission;

        if (rewardButton != null)
        {
            rewardButton.onClick.RemoveAllListeners();
            rewardButton.onClick.AddListener(ClaimReward);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (bound == null) return;

        if (titleText != null) titleText.text = bound.title;
        if (descText != null) descText.text = bound.desc;

        bool canClaim = bound.isCompleted && !bound.rewardClaimed;

        if (rewardText != null)
        {
            if (bound.rewardClaimed)
                rewardText.text = "수령 완료";
            else
                rewardText.text = NumberFormatter.FormatKorean(bound.rewardGold) + "원";
        }

        if (rewardButton != null)
            rewardButton.interactable = canClaim;
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
            // AddGold 안에서 Save 하는 구조면 여기서 sm.Save()는 중복
        }

        bound.rewardClaimed = true;

        MissionDataManager mdm = MissionDataManager.Instance;
        if (mdm != null) mdm.SaveToJson();

        Refresh();

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
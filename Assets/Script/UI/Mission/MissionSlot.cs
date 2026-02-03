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
        bool alreadyClaimed = bound.rewardClaimed;

        if (rewardText != null)
            rewardText.text = alreadyClaimed ? "수령 완료" : $"{FormatKoreanNumber(bound.rewardGold)}원";

        if (rewardButton != null)
            rewardButton.interactable = canClaim;
    }

    private void ClaimReward()
    {
        if (bound == null) return;
        if (bound.rewardClaimed) return;
        if (!bound.isCompleted) return;

        sfx.mute = !SoundManager.Instance.IsSfxOn();
        sfx.Play();

        // 1) 골드 지급
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.AddGold(bound.rewardGold);
            SaveManager.Instance.Save();
        }

        // 2) 미션 상태 변경
        bound.rewardClaimed = true;

        // 3) 미션 저장
        MissionDataManager.Instance?.SaveToJson();

        // 4) 이 슬롯 UI 갱신
        Refresh();

        // 5) ★ 상시 UI(완료!!) + 미션창 UI 갱신 이벤트 발행
        MissionProgressManager.Instance?.NotifyMissionStateChangedUIOnly();
    }

    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";
        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;
        const ulong EOK = 100_000_000UL;
        const ulong JO = 1_000_000_000_000UL;
        const ulong GYEONG = 10_000_000_000_000_000UL;

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(jo).Append("조"); }
        if (eok > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(eok).Append("억"); }
        if (man > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(man).Append("만"); }
        if (rest > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(rest); }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }
}
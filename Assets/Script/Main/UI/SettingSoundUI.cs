using UnityEngine;
using UnityEngine.UI;

// 설정창에서 BGM / SFX On-Off를 제어하는 UI
public class SettingSoundUI : MonoBehaviour
{
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;

    // 코드로 토글값 세팅할 때 이벤트 무시용
    private bool ignoreEvent;

    private void OnEnable()
    {
        // 현재 사운드 상태를 토글에 반영
        SyncFromSoundManager();

        // 리스너 연결
        if (bgmToggle != null)
            bgmToggle.onValueChanged.AddListener(OnBgmChanged);

        if (sfxToggle != null)
            sfxToggle.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnDisable()
    {
        if (bgmToggle != null)
            bgmToggle.onValueChanged.RemoveListener(OnBgmChanged);

        if (sfxToggle != null)
            sfxToggle.onValueChanged.RemoveListener(OnSfxChanged);
    }

    // SoundManager 상태 -> 토글 UI 반영
    private void SyncFromSoundManager()
    {
        var sm = SoundManager.Instance;
        if (sm == null) return;

        ignoreEvent = true;

        if (bgmToggle != null)
            bgmToggle.SetIsOnWithoutNotify(sm.IsBgmOn());

        if (sfxToggle != null)
            sfxToggle.SetIsOnWithoutNotify(sm.IsSfxOn());

        ignoreEvent = false;
    }

    private void OnBgmChanged(bool on)
    {
        if (ignoreEvent) return;

        SoundManager.Instance?.SetBgm(on);
    }

    private void OnSfxChanged(bool on)
    {
        if (ignoreEvent) return;

        SoundManager.Instance?.SetSfx(on);
    }
}
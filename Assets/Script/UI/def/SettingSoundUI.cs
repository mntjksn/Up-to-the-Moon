using UnityEngine;
using UnityEngine.UI;

public class SettingSoundUI : MonoBehaviour
{
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;

    private bool ignore;

    private void OnEnable()
    {
        // 설정창 열릴 때 현재 상태를 토글에 반영
        SyncFromSoundManager();

        // 리스너 연결
        if (bgmToggle != null) bgmToggle.onValueChanged.AddListener(OnBgmChanged);
        if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnDisable()
    {
        if (bgmToggle != null) bgmToggle.onValueChanged.RemoveListener(OnBgmChanged);
        if (sfxToggle != null) sfxToggle.onValueChanged.RemoveListener(OnSfxChanged);
    }

    private void SyncFromSoundManager()
    {
        var sm = SoundManager.Instance;
        if (sm == null) return;

        ignore = true;

        if (bgmToggle != null) bgmToggle.SetIsOnWithoutNotify(sm.IsBgmOn());
        if (sfxToggle != null) sfxToggle.SetIsOnWithoutNotify(sm.IsSfxOn());

        ignore = false;
    }

    private void OnBgmChanged(bool on)
    {
        if (ignore) return;
        SoundManager.Instance?.SetBgm(on);
    }

    private void OnSfxChanged(bool on)
    {
        if (ignore) return;
        SoundManager.Instance?.SetSfx(on);
    }
}
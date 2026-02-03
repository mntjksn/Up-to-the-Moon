using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("BGM")]
    public AudioSource bgmSource;

    private bool bgmOn = true;
    private bool sfxOn = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ApplyBgmState();
    }

    // =====================
    // BGM
    // =====================

    public void SetBgm(bool on)
    {
        bgmOn = on;
        ApplyBgmState();
    }

    private void ApplyBgmState()
    {
        if (bgmSource == null) return;

        if (bgmOn)
        {
            bgmSource.mute = false;

            if (!bgmSource.isPlaying && bgmSource.clip != null)
                bgmSource.Play();
        }
        else
        {
            bgmSource.Pause();
            bgmSource.mute = true;
        }
    }

    public bool IsBgmOn()
    {
        return bgmOn;
    }

    // =====================
    // SFX (상태만 관리)
    // =====================

    public void SetSfx(bool on)
    {
        sfxOn = on;
    }

    public bool IsSfxOn()
    {
        return sfxOn;
    }
}
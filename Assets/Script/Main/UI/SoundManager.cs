using UnityEngine;

/*
    SoundManager

    [역할]
    - 게임 전체의 사운드 상태(BGM, SFX On/Off)를 관리하는 싱글톤 매니저.
    - BGM은 실제 AudioSource를 제어한다.
    - SFX는 “켜짐/꺼짐 상태 값”만 보관하며,
      각 효과음 재생 시점에서 IsSfxOn()을 확인해 mute 여부를 결정한다.

    [설계 의도]
    1) 전역 접근 가능한 싱글톤
       - Instance로 어디서든 접근 가능
       - DontDestroyOnLoad로 씬 전환 시에도 유지

    2) BGM 제어는 중앙집중식
       - SetBgm(bool) 호출 시 내부 상태(bgmOn) 변경
       - ApplyBgmState()에서 실제 AudioSource 상태 반영

    3) SFX는 개별 AudioSource에서 mute 처리
       - SoundManager는 sfxOn 값만 저장
       - 각 SFX 재생 코드에서
         audioSource.mute = !SoundManager.Instance.IsSfxOn();
         같은 형태로 사용

    [주의/전제]
    - bgmSource에는 BGM용 AudioSource가 연결되어 있어야 한다.
    - bgmSource.clip이 없다면 Play()는 호출되지 않는다.
    - SFX용 AudioSource들은 별도로 씬에 존재하거나 프리팹에 포함되어야 한다.
*/
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource; // 배경음 재생용 AudioSource

    private bool bgmOn = true; // BGM On/Off 상태
    private bool sfxOn = true; // SFX On/Off 상태

    private void Awake()
    {
        // 싱글톤 유지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 씬이 바뀌어도 유지
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 시작 시 현재 bgmOn 상태를 AudioSource에 반영
        ApplyBgmState();
    }

    // =====================
    // BGM
    // =====================

    /*
        BGM On/Off 설정
        - 내부 상태 저장 후 ApplyBgmState() 호출
    */
    public void SetBgm(bool on)
    {
        bgmOn = on;
        ApplyBgmState();
    }

    /*
        현재 bgmOn 상태를 bgmSource에 적용
        - 켜짐: mute 해제 + 재생 중 아니면 Play()
        - 꺼짐: Pause() + mute
    */
    private void ApplyBgmState()
    {
        if (bgmSource == null) return;

        if (bgmOn)
        {
            bgmSource.mute = false;

            // 클립이 있고 재생 중이 아니면 재생
            if (!bgmSource.isPlaying && bgmSource.clip != null)
                bgmSource.Play();
        }
        else
        {
            // 일시정지 + 음소거
            bgmSource.Pause();
            bgmSource.mute = true;
        }
    }

    /*
        현재 BGM 상태 반환
    */
    public bool IsBgmOn()
    {
        return bgmOn;
    }

    // =====================
    // SFX (상태만 관리)
    // =====================

    /*
        SFX On/Off 설정
        - 실제 AudioSource는 건드리지 않고 상태 값만 저장
    */
    public void SetSfx(bool on)
    {
        sfxOn = on;
    }

    /*
        현재 SFX 상태 반환
    */
    public bool IsSfxOn()
    {
        return sfxOn;
    }
}
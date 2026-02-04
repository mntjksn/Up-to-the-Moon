using UnityEngine;
using UnityEngine.UI;

/*
    SettingSoundUI

    [역할]
    - 설정창에서 BGM / SFX On-Off 토글 UI를 제어한다.
    - 토글 값이 바뀌면 SoundManager에 전달하여 실제 사운드 상태를 변경한다.
    - 설정창이 열릴 때 현재 SoundManager 상태를 토글 UI에 반영한다.

    [설계 의도]
    1) UI ↔ 사운드 상태 동기화
       - OnEnable 시 SoundManager의 현재 상태를 읽어 토글에 반영한다.
       - 토글 조작 시 SoundManager에 값을 전달한다.

    2) 이벤트 루프 방지(ignoreEvent)
       - 코드로 토글 값을 세팅할 때
         onValueChanged 이벤트가 다시 호출되는 것을 막기 위해
         ignoreEvent 플래그를 사용한다.

    3) null-safe 처리
       - Toggle이나 SoundManager가 연결되지 않은 경우에도
         에러 없이 안전하게 동작하도록 null 체크를 한다.

    [주의/전제]
    - bgmToggle / sfxToggle은 인스펙터에서 연결되어 있어야 한다.
    - SoundManager.Instance가 싱글톤으로 존재해야 한다.
*/
public class SettingSoundUI : MonoBehaviour
{
    [SerializeField] private Toggle bgmToggle; // BGM On/Off 토글
    [SerializeField] private Toggle sfxToggle; // SFX On/Off 토글

    // 코드로 토글값 세팅할 때 이벤트 무시용 플래그
    private bool ignoreEvent;

    private void OnEnable()
    {
        // 현재 사운드 상태를 토글 UI에 반영
        SyncFromSoundManager();

        // 리스너 연결
        if (bgmToggle != null)
            bgmToggle.onValueChanged.AddListener(OnBgmChanged);

        if (sfxToggle != null)
            sfxToggle.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnDisable()
    {
        // 리스너 해제
        if (bgmToggle != null)
            bgmToggle.onValueChanged.RemoveListener(OnBgmChanged);

        if (sfxToggle != null)
            sfxToggle.onValueChanged.RemoveListener(OnSfxChanged);
    }

    /*
        SoundManager 상태 -> 토글 UI 반영
        - SetIsOnWithoutNotify를 사용해
          토글 값 세팅 시 이벤트가 발생하지 않도록 한다.
    */
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

    /*
        BGM 토글 변경 시 호출
    */
    private void OnBgmChanged(bool on)
    {
        if (ignoreEvent) return;

        SoundManager.Instance?.SetBgm(on);
    }

    /*
        SFX 토글 변경 시 호출
    */
    private void OnSfxChanged(bool on)
    {
        if (ignoreEvent) return;

        SoundManager.Instance?.SetSfx(on);
    }
}
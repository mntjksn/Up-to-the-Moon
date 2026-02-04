using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
    OutroButtons

    [역할]
    - 엔딩 화면에서 "종료"와 "재시작" 버튼 동작을 담당한다.
    - 재시작 시 저장 데이터를 초기화하고 메인 씬으로 돌아간다.

    [설계 의도]
    - 버튼 연타로 인한 중복 실행(데이터 초기화/씬 로드)을 방지하기 위해 busy 플래그를 사용한다.
    - 에디터에서는 Application.Quit()이 동작하지 않으므로 전처리기를 통해 종료 동작을 분기한다.
*/
public class OutroButtons : MonoBehaviour
{
    [Header("Restart Target Scene")]
    // 재시작 시 로드할 씬 이름
    [SerializeField] private string restartScene = "Main";

    // 버튼 연타로 중복 실행되는 것을 방지한다.
    private bool busy = false;

    /*
        여정 끝내기

        - 유니티 에디터에서는 Application.Quit()이 동작하지 않으므로
          플레이 모드를 종료하도록 분기 처리한다.
        - 빌드에서는 정상적으로 앱을 종료한다.
    */
    public void OnClickEnd()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /*
        다시 도전

        - 연타 방지를 위해 busy 상태를 확인한다.
        - 저장 데이터 초기화 후 씬을 로드하는 코루틴을 실행한다.
    */
    public void OnClickRestart()
    {
        if (busy) return;
        busy = true;

        StartCoroutine(RestartRoutine());
    }

    /*
        재시작 루틴

        - 저장 데이터 및 관련 JSON을 초기화하고, 관련 매니저를 Reload한다.
        - 파일 IO 직후 즉시 씬을 로드하면 일부 기기에서 프리즈가 체감될 수 있어
          한 프레임 양보 후 씬을 전환한다.
    */
    private IEnumerator RestartRoutine()
    {
        var save = SaveManager.Instance;
        if (save != null)
            save.ResetAllData();

        // 파일 IO/리로드 후 한 프레임 양보하여 프리즈 체감을 완화한다.
        yield return null;

        SceneManager.LoadScene(restartScene);
    }
}
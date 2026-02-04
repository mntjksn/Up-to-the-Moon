using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OutroButtons : MonoBehaviour
{
    [Header("Restart Target Scene")]
    [SerializeField] private string restartScene = "Main";

    private bool busy = false;

    // 여정 끝내기
    public void OnClickEnd()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 다시 도전
    public void OnClickRestart()
    {
        if (busy) return;
        busy = true;

        StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        var save = SaveManager.Instance;
        if (save != null)
            save.ResetAllData();

        // 파일 IO/리로드 후 한 프레임 양보(모바일 프리즈 완화)
        yield return null;

        SceneManager.LoadScene(restartScene);
    }
}
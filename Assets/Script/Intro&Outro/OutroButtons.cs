using UnityEngine;

public class OutroButtons : MonoBehaviour
{
    [Header("Restart Target Scene")]
    [SerializeField] private string restartScene = "Main"; // 또는 "Intro"

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
        // 1) 세이브 초기화
        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetAllData();

        UnityEngine.SceneManagement.SceneManager.LoadScene(restartScene);
    }
}
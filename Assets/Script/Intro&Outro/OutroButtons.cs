using UnityEngine;
using UnityEngine.SceneManagement;

public class OutroButtons : MonoBehaviour
{
    [Header("Restart Target Scene")]
    [SerializeField] private string restartScene = "Main";

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
        SaveManager save = SaveManager.Instance;
        if (save != null)
            save.ResetAllData();

        SceneManager.LoadScene(restartScene);
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartButton : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Scene Name")]
    [SerializeField] private string sceneName = "Main";

    private bool busy = false;

    private void Start()
    {
        InitFadePanel();
    }

    // 메인 씬으로 이동
    public void GoMain()
    {
        if (busy) return;
        busy = true;

        StartCoroutine(FadeAndLoad());
    }

    private void InitFadePanel()
    {
        if (fadePanel == null) return;

        fadePanel.gameObject.SetActive(false);

        Color c = fadePanel.color;
        c.a = 0f;
        fadePanel.color = c;
    }

    private IEnumerator FadeAndLoad()
    {
        Image panel = fadePanel; // 로컬 캐시

        if (panel != null)
        {
            panel.gameObject.SetActive(true);

            Color c = panel.color;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                panel.color = c;
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.05f);

        SceneManager.LoadScene(sceneName);
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartButton : MonoBehaviour
{
    [Header("Fade")]
    // 페이드 패널(알파값 조절)
    [SerializeField] private Image fadePanel;
    // 페이드 시간(초)
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Scene Name")]
    [SerializeField] private string sceneName = "Main";

    private void Start()
    {
        InitFadePanel();
    }

    // 메인 씬으로 이동
    public void GoMain()
    {
        StartCoroutine(FadeAndLoad());
    }

    // 페이드 패널을 투명 상태로 초기화
    private void InitFadePanel()
    {
        if (fadePanel == null) return;

        Color c = fadePanel.color;
        c.a = 0f;
        fadePanel.color = c;
        fadePanel.gameObject.SetActive(false);
    }

    private IEnumerator FadeAndLoad()
    {
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);

            Color c = fadePanel.color;
            float elapsed = 0f;

            // 알파값 0 -> 1
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadePanel.color = c;
                yield return null;
            }
        }

        // 전환 연출용 짧은 대기
        yield return new WaitForSeconds(0.05f);

        // 씬 로드
        SceneManager.LoadScene(sceneName);
    }
}
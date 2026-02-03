using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartButton : MonoBehaviour
{
    [Header("Fade")]
    // 페이드 패널(알파값 조절)
    [SerializeField] private Image FadePanel;
    // 페이드 시간(초)
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("SceneName")]
    [SerializeField] private string sceneName = "Main";

    public void GoMain()
    {
        StartCoroutine("FadeAndLoad");
    }

    private IEnumerator FadeAndLoad()
    {
        // 페이드 패널이 있으면 알파를 0 -> 1로 증가
        if (FadePanel != null)
        {
            FadePanel.gameObject.SetActive(true);

            Color c = FadePanel.color;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / fadeDuration;
                c.a = Mathf.Clamp01(t);
                FadePanel.color = c;
                yield return null;
            }
        }

        // 전환 연출용 짧은 대기
        yield return new WaitForSeconds(0.05f);

        // 씬 로드
        SceneManager.LoadScene(sceneName);
    }
}
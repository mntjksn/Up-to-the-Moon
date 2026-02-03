using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameComplete : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("SceneName")]
    [SerializeField] private string sceneName = "Outro";

    [Header("Goal")]
    [SerializeField] private float goalKm = 388440f;

    private bool triggered = false;

    private void Start()
    {
        // 시작할 때 투명 상태로 보장
        if (fadePanel != null)
        {
            Color c = fadePanel.color;
            c.a = 0f;
            fadePanel.color = c;
            fadePanel.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (triggered) return;

        float km = SaveManager.Instance != null ? SaveManager.Instance.GetKm() : 0f;

        // float 비교는 >= 로
        if (km >= goalKm)
        {
            triggered = true;
            StartCoroutine(FadeAndLoad());
        }
    }

    private IEnumerator FadeAndLoad()
    {
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);

            Color c = fadePanel.color;
            float elapsed = 0f;

            // 페이드 중간에 오브젝트가 파괴될 수도 있으니 매 프레임 체크
            while (elapsed < fadeDuration)
            {
                if (fadePanel == null) yield break;

                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadePanel.color = c;
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.05f);

        SceneManager.LoadScene(sceneName);
    }
}
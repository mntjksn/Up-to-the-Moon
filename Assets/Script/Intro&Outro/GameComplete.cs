using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameComplete : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Scene Name")]
    [SerializeField] private string sceneName = "Outro";

    [Header("Goal")]
    [SerializeField] private float goalKm = 388440f;

    private bool triggered = false;

    private void Start()
    {
        InitFadePanel();
    }

    private void Update()
    {
        if (triggered) return;

        SaveManager save = SaveManager.Instance;
        float km = (save != null) ? save.GetKm() : 0f;

        // 목표 거리 도달 시 엔딩 처리
        if (km >= goalKm)
        {
            triggered = true;
            StartCoroutine(FadeAndLoad());
        }
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

            // 지정 시간 동안 알파값 증가
            while (elapsed < fadeDuration)
            {
                if (fadePanel == null) yield break;

                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadePanel.color = c;

                yield return null;
            }
        }

        // 페이드 완료 후 약간의 텀
        yield return new WaitForSeconds(0.05f);

        SceneManager.LoadScene(sceneName);
    }
}
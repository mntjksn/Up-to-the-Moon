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

    [Header("Optimization")]
    [SerializeField] private float checkInterval = 0.2f; // 목표 체크 주기(초)

    private bool triggered = false;
    private SaveManager saveCached;
    private float nextCheckTime = 0f;

    private void Start()
    {
        saveCached = SaveManager.Instance;
        InitFadePanel();
    }

    private void Update()
    {
        if (triggered) return;

        // 매 프레임 체크 대신 주기 체크
        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + checkInterval;

        if (saveCached == null)
            saveCached = SaveManager.Instance;

        float km = (saveCached != null) ? saveCached.GetKm() : 0f;

        if (km >= goalKm)
        {
            triggered = true;
            StartCoroutine(FadeAndLoad());
        }
    }

    private void InitFadePanel()
    {
        if (fadePanel == null) return;

        var go = fadePanel.gameObject;
        if (go != null) go.SetActive(false);

        Color c = fadePanel.color;
        c.a = 0f;
        fadePanel.color = c;
    }

    private IEnumerator FadeAndLoad()
    {
        Image panel = fadePanel; // 로컬 캐시(루프 중 참조 비용/안정)
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
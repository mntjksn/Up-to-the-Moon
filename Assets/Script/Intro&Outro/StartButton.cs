using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    StartButton

    [역할]
    - 시작 화면에서 "시작" 버튼 클릭 시 메인 씬으로 전환한다.
    - 씬 전환 전에 페이드 아웃 연출을 수행한다.

    [설계 의도]
    - 버튼 연타로 중복 코루틴/씬 로드를 방지하기 위해 busy 플래그를 사용한다.
    - 페이드 패널의 초기 상태를 코드에서 통일하여
      씬/프리팹 초기 설정에 의존하지 않도록 한다.
*/
public class StartButton : MonoBehaviour
{
    [Header("Fade")]
    // 화면 전환 연출에 사용할 페이드 패널
    [SerializeField] private Image fadePanel;

    // 페이드 진행 시간(초)
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Scene Name")]
    // 시작 버튼 클릭 시 이동할 씬 이름
    [SerializeField] private string sceneName = "Main";

    // 버튼 연타로 인한 중복 실행 방지용 플래그
    private bool busy = false;

    private void Start()
    {
        // 시작 시 페이드 패널 상태를 통일한다.
        InitFadePanel();
    }

    /*
        메인 씬으로 이동

        - 이미 전환 중이면 추가 입력을 무시한다.
        - 페이드 연출 후 씬 전환 코루틴을 실행한다.
    */
    public void GoMain()
    {
        if (busy) return;
        busy = true;

        StartCoroutine(FadeAndLoad());
    }

    /*
        페이드 패널 초기화

        - 알파값을 0으로 설정하고 비활성화하여
          화면이 갑자기 가려지는 문제를 방지한다.
    */
    private void InitFadePanel()
    {
        if (fadePanel == null) return;

        fadePanel.gameObject.SetActive(false);

        Color c = fadePanel.color;
        c.a = 0f;
        fadePanel.color = c;
    }

    /*
        페이드 연출 후 씬 전환

        - 패널 참조를 로컬 변수로 캐시하여
          루프 중 접근 비용과 Null 리스크를 줄인다.
        - fadeDuration 동안 알파 값을 0 -> 1로 증가시킨다.
    */
    private IEnumerator FadeAndLoad()
    {
        Image panel = fadePanel;

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

        // 마지막 프레임이 반영될 시간을 짧게 확보한다.
        yield return new WaitForSeconds(0.05f);

        SceneManager.LoadScene(sceneName);
    }
}
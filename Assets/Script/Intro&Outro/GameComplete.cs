using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    GameComplete

    [역할]
    - 플레이어 이동 거리(km)가 목표(goalKm)에 도달했는지 감지한다.
    - 목표 달성 시 페이드 연출을 수행한 뒤 지정된 씬(Outro)으로 전환한다.

    [설계 의도]
    - Update에서 매 프레임 조건을 검사하면 불필요한 연산이 발생할 수 있어,
      checkInterval 주기로만 목표를 확인하여 비용을 줄인다.
    - 목표 달성 후 중복 호출을 방지하기 위해 triggered 플래그를 사용한다.
*/
public class GameComplete : MonoBehaviour
{
    [Header("Fade")]
    // 화면 전환 연출에 사용할 페이드 패널
    [SerializeField] private Image fadePanel;

    // 페이드 진행 시간(초)
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Scene Name")]
    // 목표 달성 시 전환할 씬 이름
    [SerializeField] private string sceneName = "Outro";

    [Header("Goal")]
    // 목표 거리(km). 예: 달-지구 거리(약 384,400km)와 같은 특정 목표치를 사용한다.
    [SerializeField] private float goalKm = 388440f;

    [Header("Optimization")]
    // 목표 체크 주기(초). 짧을수록 반응은 빠르나, 검사 비용은 증가한다.
    [SerializeField] private float checkInterval = 0.2f;

    // 목표 달성 후 중복 처리 방지
    private bool triggered = false;

    // SaveManager 참조 캐시(매번 Instance 조회를 줄이기 위함)
    private SaveManager saveCached;

    // 다음 체크 시각(주기 체크용)
    private float nextCheckTime = 0f;

    private void Start()
    {
        // 싱글톤 참조를 캐시하여 접근 비용을 줄인다.
        saveCached = SaveManager.Instance;

        // 시작 시 페이드 패널 상태를 통일하여 씬/프리팹 초기 상태 의존을 줄인다.
        InitFadePanel();
    }

    private void Update()
    {
        // 이미 트리거가 발동되었으면 추가 처리를 하지 않는다.
        if (triggered) return;

        // 매 프레임 체크 대신 주기 체크로 비용을 줄인다.
        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + checkInterval;

        // 캐시가 비어 있으면 다시 획득한다(씬 전환/로드 순서 차이에 대비).
        if (saveCached == null)
            saveCached = SaveManager.Instance;

        float km = (saveCached != null) ? saveCached.GetKm() : 0f;

        // 목표 달성 시 페이드 후 씬 전환을 시작한다.
        if (km >= goalKm)
        {
            triggered = true;
            StartCoroutine(FadeAndLoad());
        }
    }

    /*
        페이드 패널 초기화

        - 패널이 씬에서 켜져 있을 수 있으므로 시작 상태를 강제로 맞춘다.
        - 알파를 0으로 설정하고 비활성화하여 화면이 갑자기 가려지는 문제를 방지한다.
    */
    private void InitFadePanel()
    {
        if (fadePanel == null) return;

        var go = fadePanel.gameObject;
        if (go != null) go.SetActive(false);

        Color c = fadePanel.color;
        c.a = 0f;
        fadePanel.color = c;
    }

    /*
        페이드 연출 후 씬 전환

        - 페이드 진행 중에는 패널 참조를 로컬 변수로 캐시하여 안정성과 가독성을 확보한다.
        - 페이드가 끝난 뒤 씬을 로드한다.
    */
    private IEnumerator FadeAndLoad()
    {
        Image panel = fadePanel;
        if (panel != null)
        {
            panel.gameObject.SetActive(true);

            Color c = panel.color;
            float elapsed = 0f;

            // fadeDuration 동안 알파 값을 0 -> 1로 증가시킨다.
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                panel.color = c;
                yield return null;
            }
        }

        // UI가 마지막 프레임을 반영할 시간을 아주 짧게 확보한다.
        yield return new WaitForSeconds(0.05f);

        SceneManager.LoadScene(sceneName);
    }
}
using UnityEngine;
using UnityEngine.UI;

/*
    BackgroundApply

    [역할]
    - 플레이어 이동 거리(km)에 따라 현재 배경 스프라이트를 자동으로 교체한다.
    - BackgroundManager가 제공하는 거리 구간별 배경 데이터를 사용한다.

    [설계 의도]
    - 매 프레임 배경을 계산/교체하면 불필요한 연산과 UI 갱신이 발생할 수 있어
      checkInterval 주기로만 갱신하여 비용을 줄인다.
    - SaveManager/BackgroundManager 참조를 캐시하여 반복 접근 비용을 줄인다.
    - 이미 같은 배경이 적용되어 있으면 스프라이트 교체를 생략하여 UI 리빌드 비용을 줄인다.
*/
public class BackgroundApply : MonoBehaviour
{
    // 배경을 표시할 UI 이미지
    [SerializeField] private Image backgroundImage;

    [Header("Optimization")]
    // 배경 갱신 체크 주기(초). 짧을수록 반응은 빠르나 검사 비용은 증가한다.
    [SerializeField] private float checkInterval = 0.25f;

    // 현재 적용 중인 배경(중복 교체 방지용 캐시)
    private BackgroundItem currentItem;

    // 싱글톤 참조 캐시(매번 Instance 조회 비용을 줄이기 위함)
    private SaveManager saveCached;
    private BackgroundManager bgManagerCached;

    // 다음 체크 시각(주기 체크용)
    private float nextCheckTime;

    private void Awake()
    {
        saveCached = SaveManager.Instance;
        bgManagerCached = BackgroundManager.Instance;
        nextCheckTime = 0f;
    }

    private void OnEnable()
    {
        // 패널이 다시 켜질 때는 즉시 1회 갱신하여 현재 상태를 바로 반영한다.
        nextCheckTime = 0f;
        TryUpdateBackground(force: true);
    }

    private void Update()
    {
        if (backgroundImage == null) return;
        if (!isActiveAndEnabled) return;

        // 매 프레임 갱신 대신 주기 체크로 비용을 줄인다.
        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + checkInterval;

        TryUpdateBackground(force: false);
    }

    /*
        배경 갱신 시도

        - 매니저 로딩 상태(IsLoaded)를 확인하여 데이터 준비 전 접근을 방지한다.
        - 같은 배경이면 교체를 생략하여 불필요한 UI 갱신을 줄인다.
        - force=true면 현재 캐시와 무관하게 갱신을 시도한다(OnEnable 등에서 사용).
    */
    private void TryUpdateBackground(bool force)
    {
        // 씬 전환/로드 순서 차이로 캐시가 비어 있을 수 있어 재획득한다.
        if (saveCached == null) saveCached = SaveManager.Instance;
        if (bgManagerCached == null) bgManagerCached = BackgroundManager.Instance;

        if (saveCached == null) return;
        if (bgManagerCached == null) return;
        if (!bgManagerCached.IsLoaded) return;

        float km = saveCached.GetKm();

        // km에 해당하는 배경을 조회한다(BackgroundManager 내부에서 최적화된 조회를 수행한다).
        BackgroundItem bg = bgManagerCached.GetByKm(km);
        if (bg == null) return;

        // 이미 같은 배경이 적용되어 있으면 교체를 생략한다.
        if (!force && currentItem == bg) return;

        // 스프라이트가 로드되어 있고, 현재 스프라이트와 다를 때만 교체한다.
        if (bg.itemimg != null && backgroundImage.sprite != bg.itemimg)
        {
            backgroundImage.sprite = bg.itemimg;
        }

        currentItem = bg;
    }
}
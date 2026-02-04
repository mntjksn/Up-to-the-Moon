using UnityEngine;
using UnityEngine.UI;

public class BackgroundApply : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;

    [Header("Optimization")]
    [SerializeField] private float checkInterval = 0.25f; // 배경 체크 주기(초)

    // 현재 적용 중인 배경
    private BackgroundItem currentItem;

    private SaveManager saveCached;
    private BackgroundManager bgManagerCached;
    private float nextCheckTime;

    private void Awake()
    {
        saveCached = SaveManager.Instance;
        bgManagerCached = BackgroundManager.Instance;
        nextCheckTime = 0f;
    }

    private void OnEnable()
    {
        // 패널 다시 켜질 때 즉시 1번 갱신하고 싶으면
        nextCheckTime = 0f;
        TryUpdateBackground(force: true);
    }

    private void Update()
    {
        if (backgroundImage == null) return;
        if (!isActiveAndEnabled) return;

        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + checkInterval;

        TryUpdateBackground(force: false);
    }

    private void TryUpdateBackground(bool force)
    {
        if (saveCached == null) saveCached = SaveManager.Instance;
        if (bgManagerCached == null) bgManagerCached = BackgroundManager.Instance;

        if (saveCached == null) return;
        if (bgManagerCached == null) return;
        if (!bgManagerCached.IsLoaded) return;

        float km = saveCached.GetKm();

        BackgroundItem bg = bgManagerCached.GetByKm(km);
        if (bg == null) return;

        // 이미 같은 배경이면 스킵
        if (!force && currentItem == bg) return;

        // 이미지 교체
        if (bg.itemimg != null && backgroundImage.sprite != bg.itemimg)
        {
            backgroundImage.sprite = bg.itemimg;
        }

        currentItem = bg;
    }
}
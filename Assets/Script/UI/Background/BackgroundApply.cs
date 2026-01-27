using UnityEngine;
using UnityEngine.UI;

public class BackgroundApply : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;

    private BackgroundItem currentItem;   // 현재 적용중인 배경

    void Update()
    {
        if (SaveManager.Instance == null) return;
        if (BackgroundManager.Instance == null) return;
        if (!BackgroundManager.Instance.IsLoaded) return;

        float km = SaveManager.Instance.GetKm();

        // 현재 km에 맞는 배경 하나만 가져오기
        var bg = BackgroundManager.Instance.GetCurrentByKm(km);
        if (bg == null) return;

        // 이미 같은 배경이면 스킵
        if (currentItem == bg) return;

        // 이미지 교체
        if (bg.itemimg != null)
        {
            backgroundImage.sprite = bg.itemimg;
            currentItem = bg;
        }
    }
}
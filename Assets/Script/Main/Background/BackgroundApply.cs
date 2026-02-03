using UnityEngine;
using UnityEngine.UI;

public class BackgroundApply : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;

    // 현재 적용 중인 배경
    private BackgroundItem currentItem;

    private void Update()
    {
        if (backgroundImage == null) return;

        SaveManager save = SaveManager.Instance;
        BackgroundManager bgManager = BackgroundManager.Instance;

        if (save == null) return;
        if (bgManager == null) return;
        if (!bgManager.IsLoaded) return;

        float km = save.GetKm();

        // 현재 km에 맞는 배경 하나만 가져오기
        BackgroundItem bg = bgManager.GetByKm(km);
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
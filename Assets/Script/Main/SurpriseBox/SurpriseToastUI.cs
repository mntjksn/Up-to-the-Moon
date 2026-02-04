using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SurpriseToastUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI messageText;

    public void Set(Sprite icon, string msg)
    {
        if (messageText != null)
            messageText.text = msg;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = (icon != null);
        }
    }
}
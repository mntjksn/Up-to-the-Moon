using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SurpriseToastUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI messageText;

    // 기존 방식(SurpriseToastManager.Show에서 호출)
    public void Set(Sprite icon, string msg)
    {
        if (iconImage != null)
        {
            iconImage.enabled = (icon != null);
            iconImage.sprite = icon;
        }

        if (messageText != null)
            messageText.text = msg;
    }

    // itemNum 방식 (UI가 ItemManager에서 이미지 찾아서 교체)
    public void SetByItemNum(int itemNum, string msg)
    {
        if (messageText != null)
            messageText.text = msg;

        StartCoroutine(SetIconRoutine(itemNum));
    }

    private IEnumerator SetIconRoutine(int itemNum)
    {
        while (ItemManager.Instance == null) yield return null;
        while (!ItemManager.Instance.IsLoaded) yield return null;

        Sprite sprite = null;

        var list = ItemManager.Instance.SupplyItem;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].item_num == itemNum)
                {
                    sprite = list[i].itemimg;
                    break;
                }
            }
        }

        if (iconImage != null)
        {
            iconImage.enabled = (sprite != null);
            iconImage.sprite = sprite;
        }
    }
}

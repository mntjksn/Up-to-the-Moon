using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SurpriseToastUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI messageText;

    private Coroutine iconCo;

    // 스프라이트를 직접 넘겨서 세팅
    public void Set(Sprite icon, string msg)
    {
        if (iconCo != null)
        {
            StopCoroutine(iconCo);
            iconCo = null;
        }

        ApplyMessage(msg);
        ApplyIcon(icon);
    }

    // itemNum으로 아이콘을 나중에 찾아서 세팅
    public void SetByItemNum(int itemNum, string msg)
    {
        if (iconCo != null)
        {
            StopCoroutine(iconCo);
            iconCo = null;
        }

        ApplyMessage(msg);

        // 로드 전에는 일단 아이콘 숨김
        ApplyIcon(null);

        iconCo = StartCoroutine(LoadAndApplyIconRoutine(itemNum));
    }

    private IEnumerator LoadAndApplyIconRoutine(int itemNum)
    {
        while (ItemManager.Instance == null)
            yield return null;

        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        Sprite sprite = FindSpriteByItemNum(itemNum);
        ApplyIcon(sprite);

        iconCo = null;
    }

    private Sprite FindSpriteByItemNum(int itemNum)
    {
        var list = ItemManager.Instance.SupplyItem;
        if (list == null) return null;

        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it != null && it.item_num == itemNum)
                return it.itemimg;
        }

        return null;
    }

    private void ApplyMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;
    }

    private void ApplyIcon(Sprite sprite)
    {
        if (iconImage == null) return;

        iconImage.sprite = sprite;
        iconImage.enabled = (sprite != null);
    }
}
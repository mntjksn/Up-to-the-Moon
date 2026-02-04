using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SupplyCostRowLiveUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText; // "1000 / 1500개"

    private int need;

    private Coroutine blinkCo;
    private Color baseColor;

    public int ItemId { get; private set; }

    public void Setup(int itemId, Sprite sprite, int needCount)
    {
        ItemId = itemId;
        need = Mathf.Max(0, needCount);

        if (icon != null)
        {
            icon.enabled = (sprite != null);
            icon.sprite = sprite;
        }

        SetHave(0);
    }

    public void SetHave(int have)
    {
        have = Mathf.Max(0, have);

        if (countText == null) return;

        // 처음 색 저장
        if (baseColor == default)
            baseColor = countText.color;

        // 필요 수량 이상 모였으면
        if (have >= need)
        {
            countText.text = "수집 완료";

            // 깜빡임 시작
            if (blinkCo == null)
                blinkCo = StartCoroutine(BlinkRed());
        }
        else
        {
            // 깜빡임 중지
            if (blinkCo != null)
            {
                StopCoroutine(blinkCo);
                blinkCo = null;
            }

            countText.color = baseColor;

            countText.text =
                $"{NumberFormatter.FormatKorean(have)} / {NumberFormatter.FormatKorean(need)}개";
        }
    }

    public void SetUpgradeCompleted()
    {
        // 깜빡임 중지
        if (blinkCo != null)
        {
            StopCoroutine(blinkCo);
            blinkCo = null;
        }

        // 원래색 복구
        if (countText != null)
        {
            if (baseColor == default) baseColor = countText.color; // 안전
            countText.color = baseColor;
            countText.text = "업그레이드 완료";
        }
    }

    private IEnumerator BlinkRed()
    {
        while (true)
        {
            countText.color = Color.red;
            yield return new WaitForSeconds(0.4f);

            countText.color = baseColor;
            yield return new WaitForSeconds(0.4f);
        }
    }
}
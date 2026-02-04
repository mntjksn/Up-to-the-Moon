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
    private bool baseColorCached = false;

    // ★ 상태 캐시(중복 갱신 방지)
    private int lastHave = -1;
    private int lastNeed = -1;
    private RowState lastState = RowState.None;

    private enum RowState { None, Progress, Collected, Upgraded }

    // ★ WaitForSeconds 캐싱(할당/GC 방지)
    private static readonly WaitForSeconds waitBlink = new WaitForSeconds(0.4f);

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

        // ★ 상태 초기화
        lastHave = -1;
        lastNeed = need;
        lastState = RowState.None;

        SetHave(0);
    }

    public void SetHave(int have)
    {
        have = Mathf.Max(0, have);
        if (countText == null) return;

        // ★ baseColor는 한 번만 저장 (default 비교 대신 bool)
        if (!baseColorCached)
        {
            baseColor = countText.color;
            baseColorCached = true;
        }

        // ★ 완전 동일 입력이면 아무것도 안 함(가장 큰 최적화)
        if (have == lastHave && need == lastNeed && lastState != RowState.Upgraded)
            return;

        lastHave = have;
        lastNeed = need;

        if (have >= need)
        {
            // 이미 수집완료 상태면 텍스트/코루틴 재실행 안 함
            if (lastState != RowState.Collected)
            {
                lastState = RowState.Collected;
                countText.text = "수집 완료";

                if (blinkCo == null)
                    blinkCo = StartCoroutine(BlinkRed());
            }
            return;
        }

        // 진행중 상태
        if (lastState != RowState.Progress)
        {
            lastState = RowState.Progress;

            // 깜빡임 중지
            StopBlinkIfRunning();
            countText.color = baseColor;
        }
        else
        {
            // Progress 상태에서 have만 바뀐 경우: 코루틴/색은 건드릴 필요 없음
        }

        // ★ string interpolation 대신 SetText 사용(내부 최적화, GC 감소)
        // NumberFormatter가 string을 만든다면 그건 어쩔 수 없지만, 최소한 조합 GC는 줄임
        countText.text =
            $"{NumberFormatter.FormatKorean(have)} / {NumberFormatter.FormatKorean(need)}개";
    }

    public void SetUpgradeCompleted()
    {
        if (countText == null) return;

        // 이미 업그레이드 완료면 중복 실행 방지
        if (lastState == RowState.Upgraded) return;
        lastState = RowState.Upgraded;

        StopBlinkIfRunning();

        if (!baseColorCached)
        {
            baseColor = countText.color;
            baseColorCached = true;
        }

        countText.color = baseColor;
        countText.text = "업그레이드 완료";
    }

    private void StopBlinkIfRunning()
    {
        if (blinkCo != null)
        {
            StopCoroutine(blinkCo);
            blinkCo = null;
        }
    }

    private IEnumerator BlinkRed()
    {
        // countText가 사라졌거나 비활성화되면 안전하게 종료되도록
        while (countText != null && isActiveAndEnabled)
        {
            countText.color = Color.red;
            yield return waitBlink;

            countText.color = baseColor;
            yield return waitBlink;
        }

        blinkCo = null;
    }
}
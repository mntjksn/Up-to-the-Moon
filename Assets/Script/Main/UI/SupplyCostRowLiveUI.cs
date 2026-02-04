using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    SupplyCostRowLiveUI

    [역할]
    - “필요 재료 1줄” UI를 담당한다.
      (아이콘 + 텍스트)
    - 보유 수량(have)과 필요 수량(need)을 비교해
        1) 진행중: "have / need개"
        2) 수집 완료: "수집 완료" + 빨간색 깜빡임
        3) 업그레이드 완료: "업그레이드 완료" (깜빡임 종료)
      상태를 표시한다.

    [설계 의도 / 최적화 포인트]
    1) 상태 캐시로 중복 갱신 방지
       - lastHave/lastNeed/lastState를 저장해
         완전히 동일한 입력이면 텍스트/코루틴/색상 변경을 스킵한다.
       - Collected/Upgraded 상태에서는 불필요한 재실행을 막는다.

    2) baseColor 1회 캐싱
       - countText.color의 기본값을 baseColor에 1번만 저장하고
         이후에는 baseColorCached 플래그로 재조회/재할당을 방지한다.
       - (이전처럼 “default(Color) 비교” 대신 bool 플래그 방식)

    3) 깜빡임 코루틴 최소화
       - 수집 완료 상태가 되었을 때만 BlinkRed 코루틴을 시작한다.
       - 진행중/업그레이드 완료로 바뀌면 StopBlinkIfRunning()으로 종료한다.
       - waitBlink를 static readonly로 캐싱해 WaitForSeconds 할당/GC를 줄인다.

    4) 모바일/GC 고려
       - countText.text 갱신은 변화가 있을 때만 수행한다.
       - 문자열 포맷 자체(NumberFormatter.FormatKorean)는 문자열을 만들지만,
         그 외의 불필요한 재조합/재할당은 줄이도록 구성되어 있다.

    [주의/전제]
    - Setup(itemId, sprite, needCount) 호출로 먼저 초기화되어야 한다.
    - needCount는 0 이상으로 보정된다.
    - 외부에서 SetHave(have)를 반복 호출(예: 리소스 변경 이벤트)하는 구조에 맞춰
      최대한 “가벼운 갱신”만 하도록 설계되어 있다.
*/
public class SupplyCostRowLiveUI : MonoBehaviour
{
    [SerializeField] private Image icon;                 // 재료 아이콘
    [SerializeField] private TextMeshProUGUI countText;  // "1000 / 1500개" / "수집 완료" / "업그레이드 완료"

    private int need; // 필요 개수

    private Coroutine blinkCo;       // 깜빡임 코루틴 핸들
    private Color baseColor;         // 기본 텍스트 컬러 캐시
    private bool baseColorCached = false; // 기본 컬러 캐싱 여부

    // 상태 캐시(중복 갱신 방지)
    private int lastHave = -1;
    private int lastNeed = -1;
    private RowState lastState = RowState.None;

    private enum RowState
    {
        None,      // 초기 상태(아직 아무 UI 표시 전)
        Progress,  // 진행중: have/need 표시
        Collected, // 수집 완료: "수집 완료" + 깜빡임
        Upgraded   // 업그레이드 완료: "업그레이드 완료"
    }

    // WaitForSeconds 캐싱(할당/GC 방지)
    private static readonly WaitForSeconds waitBlink = new WaitForSeconds(0.4f);

    public int ItemId { get; private set; } // 이 row가 나타내는 아이템 id

    /*
        row 초기화
        - 아이콘/need 설정
        - 상태 캐시 초기화
        - 기본값으로 SetHave(0) 호출(첫 표시 보장)
    */
    public void Setup(int itemId, Sprite sprite, int needCount)
    {
        ItemId = itemId;
        need = Mathf.Max(0, needCount);

        // 아이콘 표시
        if (icon != null)
        {
            icon.enabled = (sprite != null);
            icon.sprite = sprite;
        }

        // 상태 초기화
        lastHave = -1;
        lastNeed = need;
        lastState = RowState.None;

        // 최초 1회 표시(외부에서 바로 have를 넣어주면 이 호출은 의미만 남지만 안전)
        SetHave(0);
    }

    /*
        보유 수량(have) 반영
        - have >= need: "수집 완료" + 깜빡임 시작(중복 시작 방지)
        - have < need : "have / need개" 표시 + 깜빡임 종료
    */
    public void SetHave(int have)
    {
        have = Mathf.Max(0, have);
        if (countText == null) return;

        // baseColor는 1번만 저장
        if (!baseColorCached)
        {
            baseColor = countText.color;
            baseColorCached = true;
        }

        // 완전 동일 입력이면 아무것도 안 함(업그레이드 완료가 아닌 상태에서만)
        if (have == lastHave && need == lastNeed && lastState != RowState.Upgraded)
            return;

        lastHave = have;
        lastNeed = need;

        // 수집 완료 상태
        if (have >= need)
        {
            // 이미 Collected면 텍스트/코루틴 재실행 안 함
            if (lastState != RowState.Collected)
            {
                lastState = RowState.Collected;
                countText.text = "수집 완료";

                // 코루틴 중복 실행 방지
                if (blinkCo == null)
                    blinkCo = StartCoroutine(BlinkRed());
            }
            return;
        }

        // 진행중 상태로 전환
        if (lastState != RowState.Progress)
        {
            lastState = RowState.Progress;

            // 깜빡임 중지 + 기본 색 복구
            StopBlinkIfRunning();
            countText.color = baseColor;
        }
        else
        {
            // Progress 상태에서 have만 바뀐 경우:
            // 코루틴/색은 건드릴 필요 없음
        }

        // 진행중 텍스트 갱신
        // (주석엔 SetText라고 되어 있지만 현재 코드는 text 할당 방식 유지)
        countText.text =
            $"{NumberFormatter.FormatKorean(have)} / {NumberFormatter.FormatKorean(need)}개";
    }

    /*
        업그레이드 완료 상태 표시
        - 이미 Upgraded면 중복 실행 방지
        - 깜빡임 중지 + 기본색으로 복구 + 텍스트 변경
    */
    public void SetUpgradeCompleted()
    {
        if (countText == null) return;

        // 이미 업그레이드 완료면 중복 실행 방지
        if (lastState == RowState.Upgraded) return;
        lastState = RowState.Upgraded;

        // 깜빡임 종료
        StopBlinkIfRunning();

        // baseColor 확보(혹시 SetHave 전에 호출될 수도 있으니)
        if (!baseColorCached)
        {
            baseColor = countText.color;
            baseColorCached = true;
        }

        countText.color = baseColor;
        countText.text = "업그레이드 완료";
    }

    /*
        깜빡임 코루틴이 돌고 있으면 중지
        - 코루틴 핸들 null로 정리
    */
    private void StopBlinkIfRunning()
    {
        if (blinkCo != null)
        {
            StopCoroutine(blinkCo);
            blinkCo = null;
        }
    }

    /*
        수집 완료 상태에서 빨간색 깜빡임
        - countText가 유효하고, 오브젝트가 활성 상태인 동안 반복
        - 비활성/파괴 등 상황에서 안전하게 종료되도록 while 조건으로 방어
    */
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
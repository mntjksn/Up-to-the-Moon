using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    SupplySellSlot

    [역할]
    - 판매 UI에서 “아이템 1종”을 나타내는 슬롯 UI를 담당한다.
      (아이콘/이름/단가/보유 개수/판매 버튼(전체 판매) + 버튼에 총 판매가 표시)
    - SaveManager의 자원 변화 이벤트를 구독하여,
      보유 개수(owned)가 바뀔 때만 “자주 바뀌는 부분(수량/총가격/버튼)”을 가볍게 갱신한다.
    - 버튼 클릭 시 해당 아이템을 “전량 판매” 처리하고 즉시 UI에 반영한다.

    [설계 의도]
    1) 고정 UI와 동적 UI 분리
       - BindItemFromIndex(): 아이콘/이름/단가처럼 거의 안 바뀌는 “고정 UI”는 여기서만 세팅
       - RefreshDynamicOnly(): 보유 개수/총 판매가/버튼 상태처럼 자주 바뀌는 “동적 UI”만 갱신

    2) 변경 감지로 TMP 갱신 최소화
       - lastOwned / lastTotalPrice 캐시를 두고 값이 바뀌었을 때만 TMP.text 재할당
       - force=true일 때는 캐시 무시하고 강제 갱신(초기 Setup/판매 직후 등)

    3) 이벤트 기반 경량 갱신
       - OnResourceChanged/OnGoldChanged 이벤트에서 전체 Refresh 대신 RefreshDynamicOnly(force:false)만 호출
       - “필요한 최소 UI”만 갱신하여 모바일에서도 프레임 드랍/리빌드 부담을 줄인다.

    [주의/전제]
    - index는 ItemManager.SupplyItem 리스트 인덱스 기준이다.
    - ItemManager가 IsLoaded=true 상태여야 아이템 바인딩이 가능하다.
    - buttonText는 “총 판매가”를 표시하는 용도이며, 단가는 bypriceText로 표시한다.
*/
public class SupplySellSlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0; // ItemManager.SupplyItem에서 참조할 인덱스

    [Header("UI")]
    [SerializeField] private Image icon;                 // 아이템 아이콘
    [SerializeField] private TextMeshProUGUI nameText;   // 아이템 이름
    [SerializeField] private TextMeshProUGUI bypriceText; // 단가 텍스트
    [SerializeField] private TextMeshProUGUI countText;  // 보유 개수 텍스트

    [Header("Button")]
    [SerializeField] private Button button;              // “전량 판매” 버튼
    [SerializeField] private TextMeshProUGUI buttonText; // 버튼에 표시되는 총 판매가 텍스트

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;            // 클릭 효과음

    private bool initialized = false; // Setup 호출 여부(초기화 완료 플래그)
    private SupplyItem item;          // 현재 슬롯이 참조 중인 아이템 데이터

    // 캐시(변경 감지)
    private int itemId = -1;                 // SaveManager 리소스 키로 사용할 item_num 캐시
    private int lastOwned = int.MinValue;    // 마지막으로 표시한 보유 개수
    private long lastTotalPrice = long.MinValue; // 마지막으로 표시한 총 판매가

    private void OnEnable()
    {
        /*
            활성화 시 처리
            - SaveManager 이벤트 구독(자원/골드 변경)
            - 버튼 리스너 연결(전량 판매)
            - 이미 Setup 완료된 슬롯이면 "가벼운 갱신" 수행
        */
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            // 자원 변경 이벤트(보유 개수/판매가 갱신에 필요)
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnResourceChanged += HandleResourceChanged;

            // 골드가 바뀌어도 이 슬롯 표시(판매가격)는 owned 기반이라 사실상 필요 없음
            // 그래도 유지하되 "가벼운 갱신"만 하게 만들 거라 OK
            sm.OnGoldChanged -= HandleGoldChanged;
            sm.OnGoldChanged += HandleGoldChanged;
        }

        // 버튼 리스너 바인딩
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickSellAll);
            button.onClick.AddListener(OnClickSellAll);
        }

        // Setup 완료된 슬롯이면 동적 부분만 갱신(필요 시 static 재바인딩 포함)
        if (initialized)
            RefreshStaticIfNeededAndDynamic();
    }

    private void OnDisable()
    {
        // 이벤트/리스너 해제
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= HandleResourceChanged;
            sm.OnGoldChanged -= HandleGoldChanged;
        }

        if (button != null)
            button.onClick.RemoveListener(OnClickSellAll);
    }

    /*
        슬롯 초기화
        - SellStorageManager에서 슬롯 생성 시 호출
        - 고정 UI는 1회 바인딩, 동적 UI는 강제 갱신
    */
    public void Setup(int idx)
    {
        index = idx;
        initialized = true;

        BindItemFromIndex();             // 아이템/아이콘/이름/단가 등 고정 UI 1회 세팅
        RefreshDynamicOnly(force: true); // 수량/버튼/총가격만 강제 갱신
    }

    /*
        아이템 고정 UI 바인딩
        - icon/name/단가(bypriceText)처럼 거의 변하지 않는 UI는 여기서만 처리
        - ItemManager 로드 상태/인덱스 범위/데이터 null에 대한 방어 처리 포함
    */
    private void BindItemFromIndex()
    {
        var im = ItemManager.Instance;
        if (im == null || !im.IsLoaded)
        {
            ApplyEmpty();
            return;
        }

        var list = im.SupplyItem;
        if (list == null || (uint)index >= (uint)list.Count)
        {
            ApplyEmpty();
            return;
        }

        item = list[index];
        if (item == null)
        {
            ApplyEmpty();
            return;
        }

        // SaveManager 리소스 키(아이템 번호) 캐시
        itemId = item.item_num;

        // icon
        if (icon != null)
        {
            if (item.itemimg != null)
            {
                icon.enabled = true;
                icon.sprite = item.itemimg;
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;
            }
        }

        // name(빈 문자열 방어)
        string safeName = string.IsNullOrWhiteSpace(item.name) ? ("Item " + index) : item.name;
        if (nameText != null) nameText.text = safeName;

        // unit price(단가)
        if (bypriceText != null) bypriceText.text = NumberFormatter.FormatKorean(item.item_price) + "원";

        // 캐시 초기화(다음 동적 갱신에서 다시 계산하도록)
        lastOwned = int.MinValue;
        lastTotalPrice = long.MinValue;
    }

    /*
        외부에서 “전체 Refresh”가 필요할 때 호출(기존 호환)
        - 고정 UI 재바인딩 + 동적 UI 강제 갱신
    */
    public void Refresh()
    {
        BindItemFromIndex();
        RefreshDynamicOnly(force: true);
    }

    /*
        필요 시 고정 UI 재바인딩 + 동적 UI 갱신
        - item이 아직 없거나 itemId가 비정상일 때만 BindItemFromIndex()를 호출
        - 이후 동적 UI만 갱신
    */
    private void RefreshStaticIfNeededAndDynamic()
    {
        // item이 아직 없거나 itemId가 비정상일 때만 static 재바인딩
        if (item == null || itemId < 0)
            BindItemFromIndex();

        RefreshDynamicOnly(force: false);
    }

    /*
        동적 UI만 갱신(자주 변하는 부분)
        - owned(보유 개수), 총 판매가, 버튼 활성/비활성
        - force=false일 때 owned가 같으면 즉시 return하여 TMP 갱신을 최소화
    */
    private void RefreshDynamicOnly(bool force)
    {
        if (item == null || itemId < 0)
        {
            ApplyEmpty();
            return;
        }

        var sm = SaveManager.Instance;
        int owned = (sm != null) ? sm.GetResource(itemId) : 0;

        // 변화 없으면(그리고 강제 갱신이 아니면) 끝
        if (!force && owned == lastOwned)
            return;

        lastOwned = owned;

        // 보유 개수 표시
        if (countText != null)
            countText.text = NumberFormatter.FormatKorean(owned) + "개";

        // 총 판매가 = 보유 개수 * 단가
        long totalPrice = (long)owned * item.item_price;

        // 총 판매가가 바뀌었을 때만 버튼 텍스트 갱신
        if (force || totalPrice != lastTotalPrice)
        {
            lastTotalPrice = totalPrice;
            if (buttonText != null)
                buttonText.text = NumberFormatter.FormatKorean(totalPrice) + "원";
        }

        // 보유가 1개 이상일 때만 판매 버튼 활성
        if (button != null)
            button.interactable = owned > 0;
    }

    /*
        데이터/바인딩 실패 시 UI 초기화
        - 아이콘/텍스트 비우고 버튼 비활성
        - 캐시도 초기화하여 다음 바인딩 시 정상 갱신되도록 함
    */
    private void ApplyEmpty()
    {
        item = null;
        itemId = -1;

        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        if (nameText != null) nameText.text = "";
        if (countText != null) countText.text = "";
        if (bypriceText != null) bypriceText.text = "";
        if (buttonText != null) buttonText.text = "";

        if (button != null) button.interactable = false;

        lastOwned = int.MinValue;
        lastTotalPrice = long.MinValue;
    }

    /*
        “전량 판매” 버튼 클릭 처리
        - 보유 개수(owned) 확인
        - totalPrice 계산 후 골드 추가, 자원 차감
        - 미션 진행도 반영(판매 총량)
        - 이 슬롯 UI만 즉시 강제 갱신
    */
    private void OnClickSellAll()
    {
        var sm = SaveManager.Instance;
        if (item == null || sm == null) return;

        // 효과음 재생(설정에 따라 mute)
        if (sfx != null)
        {
            var snd = SoundManager.Instance;
            if (snd != null) sfx.mute = !snd.IsSfxOn();
            sfx.Play();
        }

        int owned = sm.GetResource(itemId);
        if (owned <= 0) return;

        long totalPrice = (long)owned * item.item_price;

        // 판매 처리: 골드 획득 + 자원 차감
        sm.AddGold(totalPrice);
        sm.AddResource(itemId, -owned);

        // 미션 진행 반영(판매량 누적)
        MissionProgressManager.Instance?.Add("resource_sell_total", owned);

        // 즉시 반영(이 슬롯만)
        RefreshDynamicOnly(force: true);
    }

    /*
        자원 변화 이벤트 핸들러
        - 전체 Refresh 대신 동적 UI만 갱신(핵심 최적화 포인트)
    */
    private void HandleResourceChanged()
    {
        if (!initialized) return;
        RefreshDynamicOnly(force: false);
    }

    /*
        골드 변화 이벤트 핸들러
        - 골드는 이 슬롯의 “판매가 표시”에 직접 영향이 없지만,
          이벤트를 유지하더라도 동적 갱신은 캐시로 스킵될 수 있어 부담이 크지 않음
        - 필요 시 구독 제거 가능(현재는 최소 유지)
    */
    private void HandleGoldChanged()
    {
        if (!initialized) return;
        RefreshDynamicOnly(force: false);
    }
}
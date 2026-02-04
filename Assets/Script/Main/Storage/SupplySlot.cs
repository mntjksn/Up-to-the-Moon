using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    SupplySlot

    [역할]
    - 보관함(Storage) UI에서 “아이템 1종”의 보유 개수를 표시하는 슬롯 UI를 담당한다.
      (아이콘 + 수량 텍스트)
    - SaveManager의 자원 변화 이벤트를 구독하여,
      자원 수량이 바뀔 때만 수량 텍스트를 가볍게 갱신한다.

    [설계 의도]
    1) 고정 UI / 동적 UI 분리
       - BindItemStatic(): 아이콘/아이템 바인딩처럼 거의 안 바뀌는 “고정 UI”를 1회 세팅
       - RefreshDynamicOnly(): 보유 개수 텍스트처럼 자주 바뀌는 “동적 UI”만 갱신

    2) 변경 감지로 TMP 갱신 최소화
       - lastOwned 캐시로 보유 개수가 동일하면 TMP.text 재할당을 스킵한다.
       - force=true일 때는 캐시 무시하고 강제 갱신(초기 Setup/외부 Refresh 호출 시)

    3) 이벤트 기반 경량 갱신
       - OnResourceChanged 이벤트에서 전체 Refresh 대신 RefreshDynamicOnly(force:false)만 수행하여
         모바일에서도 리빌드/프레임 드랍 부담을 줄인다.

    [주의/전제]
    - index는 ItemManager.SupplyItem 리스트 인덱스 기준이다.
    - ItemManager가 IsLoaded=true 상태여야 아이콘/아이템 바인딩이 가능하다.
    - NumberFormatter.FormatKorean(int)가 존재해야 한다.
*/
public class SupplySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0; // ItemManager.SupplyItem에서 참조할 인덱스

    [Header("UI")]
    [SerializeField] private Image icon;                 // 아이템 아이콘
    [SerializeField] private TextMeshProUGUI countText;  // 보유 개수 텍스트

    private bool initialized = false; // Setup 호출 여부(초기화 완료 플래그)
    private SupplyItem item;          // 현재 슬롯이 참조 중인 아이템 데이터

    // 캐시(변경 감지)
    private int itemId = -1;                 // SaveManager 리소스 키(item_num) 캐시
    private int lastOwned = int.MinValue;    // 마지막으로 표시한 보유 개수

    private void OnEnable()
    {
        /*
            활성화 시 처리
            - SaveManager 자원 변화 이벤트 구독
            - Setup 완료된 슬롯이면 전체 Refresh 대신 "수량만" 갱신
        */
        var sm = SaveManager.Instance;
        if (sm != null)
        {
            sm.OnResourceChanged -= OnResourceChanged;
            sm.OnResourceChanged += OnResourceChanged;
        }

        // 이미 초기화된 슬롯은 동적 UI만 강제 갱신(전체 재바인딩 X)
        if (initialized)
            RefreshDynamicOnly(force: true);
    }

    private void OnDisable()
    {
        // 이벤트 해제(중복 구독/메모리 누수 방지)
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged -= OnResourceChanged;
    }

    /*
        슬롯 초기화
        - StorageManager에서 슬롯 생성 시 호출
        - 고정 UI(아이콘) 1회 바인딩 + 동적 UI(수량) 강제 갱신
    */
    public void Setup(int idx)
    {
        index = idx;
        initialized = true;

        BindItemStatic();                // 아이콘 1회 세팅
        RefreshDynamicOnly(force: true); // 수량 표시
    }

    /*
        자원 변화 이벤트 핸들러
        - 전체 Refresh 대신 수량 텍스트만 갱신(변경 감지로 스킵 가능)
    */
    private void OnResourceChanged()
    {
        if (!initialized) return;
        RefreshDynamicOnly(force: false);
    }

    /*
        아이콘/아이템 연결(고정 UI)
        - ItemManager 로드 상태/인덱스 범위/데이터 null 방어
        - itemId 캐시 및 아이콘 표시
        - lastOwned 초기화로 다음 동적 갱신이 정상적으로 일어나도록 함
    */
    private void BindItemStatic()
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

        // SaveManager 리소스 키 캐시
        itemId = item.item_num;

        // 아이콘 표시
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

        // 캐시 초기화(다음 RefreshDynamicOnly에서 다시 표시 갱신되도록)
        lastOwned = int.MinValue;
    }

    /*
        수량 텍스트만 갱신(자주 호출되는 부분)
        - force=false면 owned가 lastOwned와 같을 때 텍스트 갱신 스킵
        - item 바인딩이 없으면(force일 때만) ApplyEmpty로 UI 초기화
    */
    private void RefreshDynamicOnly(bool force)
    {
        if (item == null || itemId < 0)
        {
            // 강제 갱신 요청인데 바인딩이 깨져 있으면 UI 초기화
            if (force) ApplyEmpty();
            return;
        }

        var sm = SaveManager.Instance;
        int owned = (sm != null) ? sm.GetResource(itemId) : 0;

        // 변화 없으면(그리고 강제 갱신이 아니면) 끝
        if (!force && owned == lastOwned)
            return;

        lastOwned = owned;

        if (countText != null)
            countText.text = NumberFormatter.FormatKorean(owned) + "개";
    }

    /*
        바인딩 실패/데이터 없음 상태 UI 초기화
        - 아이콘 숨김, 텍스트 비움, 캐시 리셋
    */
    private void ApplyEmpty()
    {
        item = null;
        itemId = -1;
        lastOwned = int.MinValue;

        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        if (countText != null) countText.text = "";
    }

    /*
        기존 호환: 외부에서 Refresh 호출하면 전체 재바인딩
        - 아이콘/아이템을 다시 바인딩하고 수량을 강제 갱신한다.
    */
    public void Refresh()
    {
        BindItemStatic();
        RefreshDynamicOnly(force: true);
    }
}
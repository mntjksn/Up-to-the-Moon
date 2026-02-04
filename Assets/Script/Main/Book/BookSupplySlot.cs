using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    BookSupplySlot

    [역할]
    - 광물 사전(Book Supply) 목록에 표시되는 슬롯 하나를 담당한다.
    - 해당 인덱스의 SupplyItem 데이터를 기반으로 아이콘을 표시한다.
    - 슬롯 클릭 시, 상세 패널(BookSupplyPrefab)을 생성 또는 재사용하여
      선택된 광물의 상세 정보를 표시한다.

    [설계 의도]
    1) 단일 상세 패널 재사용
       - static openedPanel을 사용하여
         상세 패널을 여러 개 생성하지 않고 하나만 유지한다.
       - 메모리 사용량과 Instantiate 비용을 줄인다.

    2) 안전한 데이터 접근
       - ItemManager 존재 여부, 로드 완료 여부, 인덱스 범위를 검사하여
         런타임 오류를 방지한다.

    3) UI 변경 최소화
       - 동일한 스프라이트일 경우 재할당을 하지 않아
         불필요한 Canvas 리빌드를 줄인다.

    4) 참조 캐싱
       - Canvas2를 인스펙터에서 지정하면 Find를 사용하지 않도록 하여
         성능 비용을 최소화한다.
*/
public class BookSupplySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;   // 이 슬롯이 참조하는 SupplyItem 인덱스
    private bool initialized = false;         // 초기화 여부

    [Header("UI")]
    [SerializeField] private Image icon;      // 슬롯 아이콘

    [Tooltip("상세 패널 프리팹(BookSupplyPrefab 컴포넌트 포함)")]
    [SerializeField] private GameObject bookSupplyPrefab;

    [Header("Optional Refs")]
    [Tooltip("Canvas2를 인스펙터로 넣으면 Find를 사용하지 않음(권장)")]
    [SerializeField] private Transform canvas2;

    // 상세 패널 1개만 유지 (모든 슬롯에서 공유)
    private static BookSupplyPrefab openedPanel;

    private void Awake()
    {
        // 인스펙터에 지정되지 않았으면 1회만 Find
        if (canvas2 == null)
        {
            var c = GameObject.Find("Canvas2");
            canvas2 = (c != null) ? c.transform : null;
        }

        if (canvas2 == null)
            Debug.LogError("[BookSupplySlot] Canvas2를 찾지 못했습니다. Hierarchy 이름이 Canvas2인지 확인하세요.");
    }

    private void OnEnable()
    {
        // Setup 이후에만 갱신
        if (!initialized) return;
        Refresh();
    }

    /*
        슬롯 초기화

        - 외부(BookSupplyManager)에서 인덱스를 전달받아 설정한다.
    */
    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh();
    }

    /*
        슬롯 UI 갱신

        - ItemManager의 SupplyItem 데이터를 기준으로 아이콘을 갱신한다.
    */
    public void Refresh()
    {
        ItemManager im = ItemManager.Instance;
        if (im == null || !im.IsLoaded)
        {
            ApplyUI(null);
            return;
        }

        var list = im.SupplyItem;
        if (list == null || (uint)index >= (uint)list.Count)
        {
            ApplyUI(null);
            return;
        }

        ApplyUI(list[index]);
    }

    /*
        아이콘 적용

        - 데이터가 있으면 아이콘 표시
        - 없으면 아이콘 비활성화
    */
    private void ApplyUI(SupplyItem it)
    {
        if (icon == null) return;

        if (it != null && it.itemimg != null)
        {
            if (!icon.enabled) icon.enabled = true;

            // 동일 스프라이트면 재할당하지 않음
            if (icon.sprite != it.itemimg)
                icon.sprite = it.itemimg;
        }
        else
        {
            if (icon.enabled) icon.enabled = false;
            if (icon.sprite != null) icon.sprite = null;
        }
    }

    /*
        슬롯 클릭 시 호출

        - 상세 패널이 없으면 생성
        - 있으면 기존 패널 재사용
        - 선택된 인덱스를 전달하여 내용만 갱신
    */
    public void Show_Supply()
    {
        if (canvas2 == null || bookSupplyPrefab == null)
        {
            Debug.LogError("[BookSupplySlot] canvas2 또는 prefab이 비어 있습니다.");
            return;
        }

        // 이미 열린 패널이 있으면 재사용
        if (openedPanel == null)
        {
            GameObject go = Instantiate(bookSupplyPrefab, canvas2);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            openedPanel = go.GetComponent<BookSupplyPrefab>();
            if (openedPanel == null)
            {
                Debug.LogError("[BookSupplySlot] bookSupplyPrefab에 BookSupplyPrefab 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }
        }

        // 내용만 갱신
        openedPanel.Init(index);
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    BookSkySlot

    [역할]
    - 지역 사전(BookSky) 목록에서 "슬롯 하나"를 담당하는 UI 컴포넌트이다.
    - BackgroundManager의 BackgroundItem 중 하나를 인덱스로 참조하여
      아이콘 이미지를 표시한다.
    - 슬롯 클릭 시, 상세 정보를 보여주는 BookSkyPrefab 패널을 생성/재사용한다.

    [설계 의도]
    1) 인덱스 기반 구조
       - Setup(int idx)를 통해 자신이 표시할 데이터 인덱스를 저장한다.
       - Refresh()에서는 해당 인덱스만 참조하여 데이터를 갱신한다.

    2) 단일 상세 패널 재사용
       - static BookSkyPrefab openedPanel을 사용하여
         상세 패널을 1개만 유지한다.
       - 이미 생성된 패널이 있으면 재사용하여
         불필요한 Instantiate/Destroy를 방지한다.

    3) 안전성과 성능 고려
       - BackgroundManager가 없거나 로드되지 않은 경우 UI를 비운다.
       - 동일한 스프라이트일 경우 재할당을 스킵하여
         미세하지만 누적되는 비용을 줄인다.
*/
public class BookSkySlot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int index = 0;   // 참조할 BackgroundItem 인덱스
    private bool initialized = false;         // Setup 완료 여부

    [Header("UI")]
    [SerializeField] private Image icon;      // 슬롯 아이콘

    [Tooltip("상세 패널 프리팹(BookSkyPrefab 컴포넌트 포함)")]
    [SerializeField] private GameObject bookSkyPrefab;

    [Header("Optional Refs")]
    [Tooltip("Canvas2를 인스펙터로 넣으면 Find를 사용하지 않음(권장)")]
    [SerializeField] private Transform canvas2;

    // 상세 패널은 1개만 생성하여 재사용
    private static BookSkyPrefab openedPanel;

    private void Awake()
    {
        // 인스펙터에서 지정하지 않았을 경우 1회만 Find
        if (canvas2 == null)
        {
            var go = GameObject.Find("Canvas2");
            canvas2 = (go != null) ? go.transform : null;
        }

        if (canvas2 == null)
            Debug.LogError("[BookSkySlot] Canvas2를 찾지 못했습니다. Hierarchy 이름이 Canvas2인지 확인하세요.");
    }

    private void OnEnable()
    {
        // Setup 이후에만 Refresh
        if (!initialized) return;
        Refresh();
    }

    /*
        슬롯 초기화

        - BookSkyManager에서 슬롯 생성 시 호출된다.
        - 인덱스를 저장하고 초기화 완료 플래그를 설정한다.
    */
    public void Setup(int idx)
    {
        index = idx;
        initialized = true;
        Refresh();
    }

    /*
        슬롯 UI 갱신

        - BackgroundManager에서 데이터를 가져와
          해당 인덱스의 아이콘을 표시한다.
    */
    public void Refresh()
    {
        BackgroundManager bg = BackgroundManager.Instance;
        if (bg == null || !bg.IsLoaded)
        {
            ApplyUI(null);
            return;
        }

        var list = bg.BackgroundItem;
        if (list == null || (uint)index >= (uint)list.Count)
        {
            ApplyUI(null);
            return;
        }

        ApplyUI(list[index]);
    }

    /*
        아이콘 적용

        - 데이터가 없으면 아이콘을 비활성화한다.
        - 같은 스프라이트라면 재할당을 스킵한다.
    */
    private void ApplyUI(BackgroundItem it)
    {
        if (icon == null) return;

        if (it != null && it.itemimg != null)
        {
            if (!icon.enabled) icon.enabled = true;

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

        - 상세 패널(BookSkyPrefab)을 생성하거나,
          이미 존재하면 재사용한다.
        - 현재 슬롯의 인덱스를 전달하여 내용을 갱신한다.
    */
    public void Show_Supply()
    {
        if (canvas2 == null || bookSkyPrefab == null)
        {
            Debug.LogError("[BookSkySlot] canvas2 또는 prefab이 비어 있습니다.");
            return;
        }

        // 이미 열린 패널이 없으면 생성
        if (openedPanel == null)
        {
            GameObject go = Instantiate(bookSkyPrefab, canvas2);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            openedPanel = go.GetComponent<BookSkyPrefab>();
            if (openedPanel == null)
            {
                Debug.LogError("[BookSkySlot] bookSkyPrefab에 BookSkyPrefab 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }
        }

        // 패널 내용만 갱신
        openedPanel.Init(index);
    }
}
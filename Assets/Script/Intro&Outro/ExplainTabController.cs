using UnityEngine;
using UnityEngine.UI;

/*
    ExplainTabController

    [역할]
    - 설명 화면의 탭(기본/지역/자원/업그레이드)을 전환한다.
    - 탭에 따라 패널 활성화 상태를 관리하고, 선택된 버튼의 색상을 갱신한다.

    [설계 의도]
    - 패널/버튼을 배열로 관리하여 코드 중복을 줄이고 유지보수성을 높인다.
    - 같은 탭을 다시 클릭했을 때 UI 갱신을 생략하여 레이아웃 리빌드 비용을 줄인다.
*/
public class ExplainTabController : MonoBehaviour
{
    [Header("Panels")]
    // 탭별로 표시할 패널 참조
    [SerializeField] private GameObject panelDefault;
    [SerializeField] private GameObject panelArea;
    [SerializeField] private GameObject panelResource;
    [SerializeField] private GameObject panelUpgrade;

    [Header("Tab Buttons")]
    // 탭 선택 버튼 참조
    [SerializeField] private Button btnDefault;
    [SerializeField] private Button btnArea;
    [SerializeField] private Button btnResource;
    [SerializeField] private Button btnUpgrade;

    [Header("Button Colors")]
    // 선택/비선택 상태에서 사용할 버튼 색상
    [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color selectedColor = Color.white;

    // 탭 종류를 명확히 정의하여 가독성을 높인다.
    private enum Tab
    {
        Default,
        Area,
        Resource,
        Upgrade
    }

    // 탭 순서를 enum 값과 동일하게 맞춰 관리한다.
    private GameObject[] panels;
    private Button[] buttons;
    private Image[] buttonImages;

    // 현재 선택 탭 인덱스를 캐시하여 중복 갱신을 방지한다.
    private int currentIndex = -1;

    private void Awake()
    {
        // 탭 순서를 통일하여 Show((Tab)index)로 단순 처리한다.
        panels = new GameObject[] { panelDefault, panelArea, panelResource, panelUpgrade };
        buttons = new Button[] { btnDefault, btnArea, btnResource, btnUpgrade };
        buttonImages = new Image[buttons.Length];

        // 버튼 클릭 시 해당 탭을 표시하도록 리스너를 등록한다.
        // 반복문 캡처 문제를 피하기 위해 index를 로컬 변수로 복사한다.
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;

            buttonImages[i] = b.GetComponent<Image>();

            int index = i;
            b.onClick.AddListener(() => Show((Tab)index));
        }

        // 씬에서 패널이 켜져있을 수 있으므로 초기 상태를 통일한다.
        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null) panels[i].SetActive(false);

        // 초기 버튼 색상을 모두 비선택 상태로 맞춘다.
        SetAllButtons(normalColor);
    }

    private void OnEnable()
    {
        // 패널이 활성화될 때 기본 탭을 항상 먼저 보여준다.
        Show(Tab.Default);
    }

    /*
        탭 전환 처리

        - 이전 탭을 비활성화하고, 새 탭을 활성화한다.
        - 버튼 색상을 비선택/선택 상태로 갱신한다.
        - 같은 탭 재클릭 시 불필요한 SetActive/색상 변경을 생략한다.
    */
    private void Show(Tab tab)
    {
        int index = (int)tab;
        if (index < 0 || index >= panels.Length) return;

        // 같은 탭이면 아무 작업도 하지 않아 UI 리빌드 비용을 줄인다.
        if (currentIndex == index) return;

        // 이전 탭 패널을 끈다.
        if (currentIndex >= 0 && currentIndex < panels.Length && panels[currentIndex] != null)
            panels[currentIndex].SetActive(false);

        // 이전 탭 버튼 색상을 비선택 상태로 되돌린다.
        if (currentIndex >= 0 && currentIndex < buttonImages.Length && buttonImages[currentIndex] != null)
            buttonImages[currentIndex].color = normalColor;

        // 새 탭 패널을 켠다.
        if (panels[index] != null)
            panels[index].SetActive(true);

        // 새 탭 버튼 색상을 선택 상태로 표시한다.
        if (index < buttonImages.Length && buttonImages[index] != null)
            buttonImages[index].color = selectedColor;

        currentIndex = index;
    }

    // 모든 버튼 색상을 동일하게 설정한다(초기화 용도).
    private void SetAllButtons(Color color)
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] != null)
                buttonImages[i].color = color;
        }
    }
}
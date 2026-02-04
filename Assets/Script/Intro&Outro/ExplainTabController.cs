using UnityEngine;
using UnityEngine.UI;

public class ExplainTabController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject panelDefault;
    [SerializeField] private GameObject panelArea;
    [SerializeField] private GameObject panelResource;
    [SerializeField] private GameObject panelUpgrade;

    [Header("Tab Buttons")]
    [SerializeField] private Button btnDefault;
    [SerializeField] private Button btnArea;
    [SerializeField] private Button btnResource;
    [SerializeField] private Button btnUpgrade;

    [Header("Button Colors")]
    [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color selectedColor = Color.white;

    private enum Tab
    {
        Default,
        Area,
        Resource,
        Upgrade
    }

    private GameObject[] panels;
    private Button[] buttons;
    private Image[] buttonImages;

    private int currentIndex = -1; // 현재 탭 캐시

    private void Awake()
    {
        panels = new GameObject[] { panelDefault, panelArea, panelResource, panelUpgrade };
        buttons = new Button[] { btnDefault, btnArea, btnResource, btnUpgrade };
        buttonImages = new Image[buttons.Length];

        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;

            buttonImages[i] = b.GetComponent<Image>();

            int index = i;
            b.onClick.AddListener(() => Show((Tab)index));
        }

        // 초기 상태 정리(선택: 씬에서 켜져있을 수 있으니)
        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null) panels[i].SetActive(false);

        SetAllButtons(normalColor);
    }

    private void OnEnable()
    {
        Show(Tab.Default);
    }

    private void Show(Tab tab)
    {
        int index = (int)tab;
        if (index < 0 || index >= panels.Length) return;

        // 같은 탭이면 아무것도 안 함(레이아웃 리빌드 방지)
        if (currentIndex == index) return;

        // 이전 탭 끄기
        if (currentIndex >= 0 && currentIndex < panels.Length && panels[currentIndex] != null)
            panels[currentIndex].SetActive(false);

        // 이전 버튼 색 되돌리기
        if (currentIndex >= 0 && currentIndex < buttonImages.Length && buttonImages[currentIndex] != null)
            buttonImages[currentIndex].color = normalColor;

        // 새 탭 켜기
        if (panels[index] != null)
            panels[index].SetActive(true);

        // 새 버튼 색
        if (index < buttonImages.Length && buttonImages[index] != null)
            buttonImages[index].color = selectedColor;

        currentIndex = index;
    }

    private void SetAllButtons(Color color)
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] != null)
                buttonImages[i].color = color;
        }
    }
}
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
    }

    private void OnEnable()
    {
        Show(Tab.Default);
    }

    private void Show(Tab tab)
    {
        SetAllPanels(false);
        SetAllButtons(normalColor);

        int index = (int)tab;

        if (index >= 0 && index < panels.Length && panels[index] != null)
            panels[index].SetActive(true);

        if (index >= 0 && index < buttonImages.Length && buttonImages[index] != null)
            buttonImages[index].color = selectedColor;
    }

    private void SetAllPanels(bool active)
    {
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i].SetActive(active);
        }
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
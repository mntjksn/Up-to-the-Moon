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
    [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f); // 기본 회색
    [SerializeField] private Color selectedColor = Color.white;              // 선택된 버튼

    private void Awake()
    {
        btnDefault.onClick.AddListener(() => Show(Tab.Default));
        btnArea.onClick.AddListener(() => Show(Tab.Area));
        btnResource.onClick.AddListener(() => Show(Tab.Resource));
        btnUpgrade.onClick.AddListener(() => Show(Tab.Upgrade));
    }

    private void OnEnable()
    {
        Show(Tab.Default);
    }

    private enum Tab { Default, Area, Resource, Upgrade }

    private void Show(Tab tab)
    {
        // 패널 끄기
        panelDefault.SetActive(false);
        panelArea.SetActive(false);
        panelResource.SetActive(false);
        panelUpgrade.SetActive(false);

        // 버튼 색 초기화
        SetButtonColor(btnDefault, normalColor);
        SetButtonColor(btnArea, normalColor);
        SetButtonColor(btnResource, normalColor);
        SetButtonColor(btnUpgrade, normalColor);

        // 선택 패널 & 버튼
        switch (tab)
        {
            case Tab.Default:
                panelDefault.SetActive(true);
                SetButtonColor(btnDefault, selectedColor);
                break;

            case Tab.Area:
                panelArea.SetActive(true);
                SetButtonColor(btnArea, selectedColor);
                break;

            case Tab.Resource:
                panelResource.SetActive(true);
                SetButtonColor(btnResource, selectedColor);
                break;

            case Tab.Upgrade:
                panelUpgrade.SetActive(true);
                SetButtonColor(btnUpgrade, selectedColor);
                break;
        }
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = color;
    }
}
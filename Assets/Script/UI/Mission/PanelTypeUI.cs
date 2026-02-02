using TMPro;
using UnityEngine;

public class PanelTypeUI : MonoBehaviour
{
    [Header("Refs in Panel_type")]
    [SerializeField] private TextMeshProUGUI titleText; // "성장" 표시
    [SerializeField] private Transform listRoot;        // Panel_list_total

    public Transform ListRoot => listRoot;

    public void SetTitle(string title)
    {
        if (titleText != null) titleText.text = title;
    }
}
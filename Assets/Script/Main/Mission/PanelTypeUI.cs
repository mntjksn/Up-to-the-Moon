using TMPro;
using UnityEngine;

public class PanelTypeUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;   // 카테고리 제목
    [SerializeField] private Transform listRoot;          // 리스트 부모

    // MissionManager에서 접근용
    public Transform ListRoot
    {
        get { return listRoot; }
    }

    public void SetTitle(string title)
    {
        if (titleText == null) return;
        titleText.text = title;
    }
}
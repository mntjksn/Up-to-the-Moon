using TMPro;
using UnityEngine;

/*
    PanelTypeUI

    [역할]
    - 미션 카테고리(성장/지역/자원/강화/플레이 등) 제목을 표시하는 UI 컴포넌트.
    - 해당 카테고리에 속한 MissionSlot들이 배치될 부모 Transform(ListRoot)을 제공한다.

    [설계 의도]
    1) 책임 분리
       - MissionManager는 "어떤 카테고리가 있고 어떤 미션을 넣을지" 결정한다.
       - PanelTypeUI는 "제목 표시"와 "슬롯을 붙일 부모 제공"만 담당한다.

    2) 단순 데이터 전달용 인터페이스
       - ListRoot 프로퍼티를 통해 외부에서 슬롯을 생성/배치할 수 있게 한다.
       - 내부 구현은 숨기고, 필요한 정보만 노출한다.

    [사용 예]
       PanelTypeUI typeUI = typeObj.GetComponent<PanelTypeUI>();
       typeUI.SetTitle("성장");
       Instantiate(panelListPrefab, typeUI.ListRoot);
*/
public class PanelTypeUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;   // 카테고리 제목 텍스트
    [SerializeField] private Transform listRoot;          // 해당 카테고리 슬롯들이 붙을 부모 Transform

    /*
        MissionManager에서 접근하기 위한 프로퍼티

        - 직접 필드를 노출하지 않고 읽기 전용으로 제공하여
          외부에서 listRoot 참조는 가능하지만,
          임의로 다른 Transform으로 교체하지는 못하게 한다.
    */
    public Transform ListRoot
    {
        get { return listRoot; }
    }

    /*
        카테고리 제목 설정

        - MissionManager에서 카테고리 생성 시 호출된다.
    */
    public void SetTitle(string title)
    {
        if (titleText == null) return;
        titleText.text = title;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    PanelEvent

    [역할]
    - 지정된 panel(GameObject)을
      활성화(show), 비활성화(close), 완전 제거(destroy)하는 간단한 이벤트 스크립트.
    - 주로 버튼(OnClick) 이벤트에 연결하여 사용한다.

    [설계 의도]
    1) UI 버튼용 경량 스크립트
       - 복잡한 로직 없이 panel에 대한 기본 제어만 담당한다.

    2) null-safe 처리
       - panel이 연결되지 않았을 경우를 대비해
         모든 함수에서 null 체크 후 동작한다.

    [주의/전제]
    - panel에는 활성/비활성화 또는 Destroy가 가능한 GameObject가 연결되어 있어야 한다.
    - Destroy(panel)을 호출하면 해당 패널은 복구할 수 없으므로,
      단순히 숨기고 싶다면 panel_close()를 사용해야 한다.
*/
public class PanelEvent : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField] private GameObject panel; // 제어할 대상 패널

    /*
        패널 활성화
        - panel이 null이면 아무 것도 하지 않는다.
    */
    public void panel_show()
    {
        if (panel == null)
            return;

        panel.SetActive(true);
    }

    /*
        패널 비활성화
        - panel이 null이면 아무 것도 하지 않는다.
    */
    public void panel_close()
    {
        if (panel == null)
            return;

        panel.SetActive(false);
    }

    /*
        패널 오브젝트 완전 제거
        - panel이 null이면 아무 것도 하지 않는다.
        - Destroy 이후에는 다시 사용할 수 없다.
    */
    public void panel_destroy()
    {
        if (panel == null)
            return;

        Destroy(panel);
    }
}
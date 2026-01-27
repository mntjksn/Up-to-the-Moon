using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelEvent : MonoBehaviour
{
    // 제어할 패널 오브젝트
    public GameObject panel;

    // 패널 활성화
    public void panel_show()
    {
        if (panel == null)
            return;

        panel.SetActive(true);
    }

    // 패널 비활성화
    public void panel_close()
    {
        if (panel == null)
            return;

        panel.SetActive(false);
    }

    // 패널 오브젝트 완전 제거
    public void panel_destroy()
    {
        if (panel == null)
            return;

        Destroy(panel);
    }
}

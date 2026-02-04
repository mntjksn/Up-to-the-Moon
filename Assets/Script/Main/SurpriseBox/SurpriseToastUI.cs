using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    SurpriseToastUI

    [역할]
    - SurpriseToastManager가 제어하는 “토스트 UI”의 실제 표시 컴포넌트.
    - 아이콘(Image)과 메시지(TextMeshProUGUI)를 받아 화면에 반영한다.

    [설계 의도]
    1) 단순 표시 전용 컴포넌트
       - 데이터 저장/계산 로직은 없고,
         외부에서 Set(icon, msg)만 호출하면 즉시 UI가 갱신된다.

    2) null-safe 처리
       - iconImage/messageText 참조가 비어 있어도 오류 없이 동작하도록 null 체크를 한다.

    [주의/전제]
    - 이 스크립트는 토스트 프리팹에 부착되어 있어야 한다.
    - iconImage, messageText는 인스펙터에서 연결되어 있어야 한다.
*/
public class SurpriseToastUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;           // 토스트 아이콘
    [SerializeField] private TextMeshProUGUI messageText; // 토스트 메시지 텍스트

    /*
        외부에서 호출: 토스트 내용 세팅
        - icon: 표시할 아이콘(없으면 null 가능)
        - msg : 표시할 메시지 문자열
    */
    public void Set(Sprite icon, string msg)
    {
        // 메시지 텍스트 세팅
        if (messageText != null)
            messageText.text = msg;

        // 아이콘 세팅(없으면 숨김)
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = (icon != null);
        }
    }
}
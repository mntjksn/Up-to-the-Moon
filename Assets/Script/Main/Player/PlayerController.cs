using System.Collections;
using UnityEngine;

/*
    PlayerController

    [역할]
    - 현재 선택된 캐릭터 ID에 맞는 스프라이트를 플레이어 오브젝트에 적용한다.
    - SaveManager의 캐릭터 변경 이벤트를 구독하여,
      캐릭터가 바뀔 때마다 자동으로 외형을 갱신한다.

    [설계 의도]
    1) 이벤트 기반 구조
       - Update에서 매 프레임 캐릭터를 확인하지 않고,
         SaveManager.OnCharacterChanged 이벤트를 통해서만 갱신한다.
       - 불필요한 연산을 줄이고 구조를 단순화한다.

    2) 준비 상태 대기
       - SaveManager와 CharacterManager가 아직 준비되지 않았을 수 있으므로,
         코루틴을 사용해 Instance와 데이터 로드 완료 시점까지 기다린다.

    3) 안전한 구독/해제
       - OnEnable에서 이벤트를 바인딩하고,
         OnDisable에서 반드시 해제하여 중복 등록 및 메모리 누수를 방지한다.
*/
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;   // 플레이어 스프라이트 렌더러

    private Coroutine bindCo;     // 바인딩 대기 코루틴
    private bool isBound = false; // 이벤트 구독 여부

    private void Awake()
    {
        // 인스펙터에 할당되지 않았으면 자동으로 가져온다.
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        // 중복 코루틴 방지
        if (bindCo != null) StopCoroutine(bindCo);

        // SaveManager / CharacterManager 준비될 때까지 대기 후 바인딩
        bindCo = StartCoroutine(BindAndApplyRoutine());
    }

    private void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        Unbind();
    }

    /*
        SaveManager 이벤트 구독 해제

        - 이미 바인딩되어 있지 않으면 아무것도 하지 않는다.
    */
    private void Unbind()
    {
        if (!isBound) return;

        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnCharacterChanged -= ApplyCharacterSprite;

        isBound = false;
    }

    /*
        SaveManager와 CharacterManager가 준비될 때까지 대기한 뒤,
        이벤트를 구독하고 현재 캐릭터 스프라이트를 1회 적용한다.
    */
    private IEnumerator BindAndApplyRoutine()
    {
        // SaveManager 준비 대기
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        // 중복 구독 방지
        sm.OnCharacterChanged -= ApplyCharacterSprite;
        sm.OnCharacterChanged += ApplyCharacterSprite;
        isBound = true;

        // CharacterManager 준비 대기
        while (CharacterManager.Instance == null || !CharacterManager.Instance.IsLoaded)
            yield return null;

        // 현재 캐릭터 ID 기준으로 스프라이트 적용
        ApplyCharacterSprite(sm.GetCurrentCharacterId());

        bindCo = null;
    }

    /*
        캐릭터 ID에 맞는 스프라이트를 적용한다.
    */
    private void ApplyCharacterSprite(int id)
    {
        if (sr == null) return;

        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return;

        var list = cm.CharacterItem;
        if (list == null) return;

        // 범위 체크 (빠른 unsigned 비교)
        if ((uint)id >= (uint)list.Count) return;

        var it = list[id];
        if (it == null || it.itemimg == null) return;

        // 동일 스프라이트면 재할당 스킵
        if (sr.sprite != it.itemimg)
            sr.sprite = it.itemimg;
    }
}
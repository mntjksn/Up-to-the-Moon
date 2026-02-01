using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    private SpriteRenderer sr;
    private Coroutine bindCo;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        // OnEnable 타이밍에 SaveManager/CharacterManager가 아직 준비 안됐을 수 있으니
        // 코루틴으로 안전하게 대기 후 구독 + 1회 적용
        if (bindCo != null) StopCoroutine(bindCo);
        bindCo = StartCoroutine(BindAndApplyRoutine());
    }

    private void OnDisable()
    {
        if (bindCo != null) StopCoroutine(bindCo);
        bindCo = null;

        if (SaveManager.Instance != null)
            SaveManager.Instance.OnCharacterChanged -= ApplyCharacterSprite;
    }

    private IEnumerator BindAndApplyRoutine()
    {
        // SaveManager 준비될 때까지 대기
        while (SaveManager.Instance == null)
            yield return null;

        // 이벤트 중복 구독 방지
        SaveManager.Instance.OnCharacterChanged -= ApplyCharacterSprite;
        SaveManager.Instance.OnCharacterChanged += ApplyCharacterSprite;

        // CharacterManager 로드 완료까지 대기
        while (CharacterManager.Instance == null || !CharacterManager.Instance.IsLoaded)
            yield return null;

        // 저장된 캐릭터로 1회 적용
        ApplyCharacterSprite(SaveManager.Instance.GetCurrentCharacterId());
    }

    private void ApplyCharacterSprite(int id)
    {
        if (sr == null) return;

        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return;
        if (cm.CharacterItem == null) return;

        // 범위 체크
        if (id < 0 || id >= cm.CharacterItem.Count) return;

        var it = cm.CharacterItem[id];
        if (it != null && it.itemimg != null)
            sr.sprite = it.itemimg;
    }
}
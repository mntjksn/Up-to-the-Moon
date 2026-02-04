using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;

    private Coroutine bindCo;
    private bool isBound = false;

    private void Awake()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (bindCo != null) StopCoroutine(bindCo);
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

    private void Unbind()
    {
        if (!isBound) return;

        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnCharacterChanged -= ApplyCharacterSprite;

        isBound = false;
    }

    private IEnumerator BindAndApplyRoutine()
    {
        // SaveManager 준비 대기
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        // 중복 방지
        sm.OnCharacterChanged -= ApplyCharacterSprite;
        sm.OnCharacterChanged += ApplyCharacterSprite;
        isBound = true;

        // CharacterManager 준비 대기
        while (CharacterManager.Instance == null || !CharacterManager.Instance.IsLoaded)
            yield return null;

        ApplyCharacterSprite(sm.GetCurrentCharacterId());

        bindCo = null;
    }

    private void ApplyCharacterSprite(int id)
    {
        if (sr == null) return;

        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return;

        var list = cm.CharacterItem;
        if (list == null) return;

        if ((uint)id >= (uint)list.Count) return;

        var it = list[id];
        if (it == null || it.itemimg == null) return;

        // 같은 스프라이트면 재할당 스킵
        if (sr.sprite != it.itemimg)
            sr.sprite = it.itemimg;
    }
}
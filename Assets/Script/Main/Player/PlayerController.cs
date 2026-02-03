using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;

    private Coroutine bindCo;

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

        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnCharacterChanged -= ApplyCharacterSprite;
    }

    private IEnumerator BindAndApplyRoutine()
    {
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        sm.OnCharacterChanged -= ApplyCharacterSprite;
        sm.OnCharacterChanged += ApplyCharacterSprite;

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

        if (id < 0 || id >= list.Count) return;

        var it = list[id];
        if (it == null) return;
        if (it.itemimg == null) return;

        sr.sprite = it.itemimg;
    }
}
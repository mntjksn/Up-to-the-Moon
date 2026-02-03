using System.Collections;
using UnityEngine;

public class SurpriseToastManager : MonoBehaviour
{
    public static SurpriseToastManager Instance;

    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private Transform toastParent;
    [SerializeField] private float lifeTime = 2.5f;

    private Coroutine hideCo;
    private GameObject currentToast;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 외부에서 스프라이트를 직접 넘겨서 토스트 표시
    public void Show(Sprite iconSprite, string message)
    {
        SpawnAndSet(iconSprite, message);
    }

    // itemNum으로 내부에서 스프라이트를 찾아서 토스트 표시
    public void ShowByItemNum(int itemNum, string message)
    {
        Sprite icon = FindSpriteByItemNum(itemNum);
        SpawnAndSet(icon, message);
    }

    private void SpawnAndSet(Sprite iconSprite, string message)
    {
        if (toastPrefab == null || toastParent == null)
            return;

        if (currentToast != null)
            Destroy(currentToast);

        currentToast = Instantiate(toastPrefab, toastParent);
        currentToast.SetActive(true);

        var ui = currentToast.GetComponent<SurpriseToastUI>();
        if (ui != null)
            ui.Set(iconSprite, message);

        if (hideCo != null)
            StopCoroutine(hideCo);

        hideCo = StartCoroutine(AutoHideRoutine());
    }

    private IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSecondsRealtime(lifeTime);

        if (currentToast != null)
        {
            Destroy(currentToast);
            currentToast = null;
        }

        hideCo = null;
    }

    private Sprite FindSpriteByItemNum(int itemNum)
    {
        if (ItemManager.Instance == null || !ItemManager.Instance.IsLoaded)
            return null;

        var list = ItemManager.Instance.SupplyItem;
        if (list == null) return null;

        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it != null && it.item_num == itemNum)
                return it.itemimg;
        }

        return null;
    }
}
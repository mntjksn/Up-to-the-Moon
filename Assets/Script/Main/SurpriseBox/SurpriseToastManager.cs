using System.Collections;
using UnityEngine;

public class SurpriseToastManager : MonoBehaviour
{
    public static SurpriseToastManager Instance;

    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private Transform toastParent;
    [SerializeField] private float lifeTime = 2.5f;

    private Coroutine routine;
    private GameObject current;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 기존: Sprite를 외부에서 받아오는 방식 (이걸 쓰면 icon=null 문제 계속 생김)
    public void Show(Sprite iconSprite, string message)
    {
        SpawnAndSet(iconSprite, message);
    }

    // 추천: itemNum으로 sprite를 내부에서 찾아서 표시
    public void ShowByItemNum(int itemNum, string message)
    {
        Sprite icon = GetSpriteByItemNum(itemNum);
        if (icon == null)
            Debug.LogWarning($"[ToastMgr] icon is NULL (itemNum={itemNum}). spritePath/Resources 확인 필요");

        SpawnAndSet(icon, message);
    }

    private void SpawnAndSet(Sprite iconSprite, string message)
    {
        if (toastPrefab == null || toastParent == null) return;

        if (current != null) Destroy(current);

        current = Instantiate(toastPrefab, toastParent);
        current.SetActive(true);

        var ui = current.GetComponent<SurpriseToastUI>();
        if (ui != null)
            ui.Set(iconSprite, message);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(AutoHide());
    }

    private IEnumerator AutoHide()
    {
        yield return new WaitForSecondsRealtime(lifeTime);

        if (current != null)
        {
            Destroy(current);
            current = null;
        }
        routine = null;
    }

    private Sprite GetSpriteByItemNum(int itemNum)
    {
        // ItemManager 로드 체크
        if (ItemManager.Instance == null || !ItemManager.Instance.IsLoaded)
        {
            Debug.LogWarning("[ToastMgr] ItemManager not ready yet.");
            return null;
        }

        var list = ItemManager.Instance.SupplyItem;
        if (list == null) return null;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].item_num == itemNum)
                return list[i].itemimg;   // Resources.Load로 채워진 스프라이트
        }
        return null;
    }
}
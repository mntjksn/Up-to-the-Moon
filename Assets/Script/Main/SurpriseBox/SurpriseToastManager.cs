using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurpriseToastManager : MonoBehaviour
{
    public static SurpriseToastManager Instance;

    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private Transform toastParent;
    [SerializeField] private float lifeTime = 2.5f;

    private Coroutine hideCo;

    // 토스트 1개 재사용
    private GameObject toastGO;
    private SurpriseToastUI toastUI;

    // 아이콘 캐시 (itemNum -> sprite)
    private readonly Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>(64);
    private bool cacheBuilt = false;

    // Wait 캐시
    private WaitForSecondsRealtime waitLife;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        waitLife = new WaitForSecondsRealtime(lifeTime);
    }

    private void OnEnable()
    {
        // (선택) 처음 켜질 때 미리 생성해두면 더 부드러움
        EnsureToast();
    }

    public void Show(Sprite iconSprite, string message)
    {
        EnsureToast();
        SpawnAndSet(iconSprite, message);
    }

    public void ShowByItemNum(int itemNum, string message)
    {
        EnsureToast();
        Sprite icon = FindSpriteByItemNumCached(itemNum);
        SpawnAndSet(icon, message);
    }

    private void EnsureToast()
    {
        if (toastGO != null) return;
        if (toastPrefab == null || toastParent == null) return;

        toastGO = Instantiate(toastPrefab, toastParent);
        toastGO.SetActive(false);

        toastUI = toastGO.GetComponent<SurpriseToastUI>();
    }

    private void SpawnAndSet(Sprite iconSprite, string message)
    {
        if (toastGO == null) return;

        // 기존 코루틴 중단
        if (hideCo != null)
        {
            StopCoroutine(hideCo);
            hideCo = null;
        }

        // 내용 갱신
        if (toastUI != null)
            toastUI.Set(iconSprite, message);

        // 보여주기
        toastGO.SetActive(true);

        // lifeTime이 런타임에 바뀔 수 있으면 여기서 wait 재생성
        // waitLife = new WaitForSecondsRealtime(lifeTime);

        hideCo = StartCoroutine(AutoHideRoutine());
    }

    private IEnumerator AutoHideRoutine()
    {
        // Wait 객체 재사용 (GC 감소)
        yield return waitLife;

        if (toastGO != null)
            toastGO.SetActive(false);

        hideCo = null;
    }

    private Sprite FindSpriteByItemNumCached(int itemNum)
    {
        // 이미 캐시 있으면 즉시 반환
        if (iconCache.TryGetValue(itemNum, out var spr))
            return spr;

        // 캐시 아직 안 만들었으면 1회 빌드 시도
        if (!cacheBuilt)
            BuildCacheIfPossible();

        // 다시 시도
        if (iconCache.TryGetValue(itemNum, out spr))
            return spr;

        return null;
    }

    private void BuildCacheIfPossible()
    {
        var im = ItemManager.Instance;
        if (im == null || !im.IsLoaded) return;

        var list = im.SupplyItem;
        if (list == null) return;

        iconCache.Clear();

        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it == null) continue;

            // item_num이 유니크하다는 전제
            iconCache[it.item_num] = it.itemimg;
        }

        cacheBuilt = true;
    }

    // (선택) 아이템 데이터가 리로드될 수 있으면 외부에서 호출해 캐시 무효화
    public void InvalidateCache()
    {
        cacheBuilt = false;
        iconCache.Clear();
    }
}
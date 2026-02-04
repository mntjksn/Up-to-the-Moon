using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    SurpriseToastManager

    [역할]
    - 서프라이즈 박스 등에서 사용하는 “토스트(아이콘 + 메시지)” UI를 1개만 재사용하여 표시한다.
    - Show(Sprite, message) 또는 ShowByItemNum(itemNum, message)로 호출하면
      토스트 내용을 갱신하고 일정 시간(lifeTime) 후 자동으로 숨긴다.
    - itemNum 기반 아이콘 조회는 ItemManager.SupplyItem을 1회 스캔해
      (itemNum -> sprite) 캐시를 만든 뒤 빠르게 조회한다.

    [설계 의도]
    1) 토스트 1개 재사용(Instantiate 최소화)
       - EnsureToast()에서 토스트 오브젝트를 1회만 생성하고,
         이후에는 SetActive로 표시/숨김만 수행한다.
       - 반복 생성/파괴로 인한 GC/프레임 드랍을 줄인다.

    2) 자동 숨김 코루틴 관리(타이머 리셋)
       - 연속 Show 호출 시 기존 hideCo를 중단하고 새로 시작하여
         “마지막으로 띄운 토스트” 기준으로 lifeTime이 적용되게 한다.
       - WaitForSecondsRealtime(waitLife)을 캐싱해 GC를 줄인다.

    3) 아이콘 캐싱
       - ShowByItemNum 호출 시 iconCache(itemNum -> sprite)를 사용한다.
       - cacheBuilt=false면 BuildCacheIfPossible()로 1회 빌드 후 다시 조회한다.
       - 아이템 데이터가 리로드될 수 있으면 InvalidateCache() 같은 방식으로 무효화 가능(필요 시 추가).

    [주의/전제]
    - toastPrefab에는 SurpriseToastUI 컴포넌트가 있어야 한다.
    - toastParent는 Canvas 하위(또는 UI 표시용 부모 Transform)여야 한다.
    - ItemManager.SupplyItem의 item_num이 유니크하다는 전제다.
    - lifeTime을 런타임에 변경한다면 waitLife 재생성이 필요하다(코드에 주석으로 안내).
*/
public class SurpriseToastManager : MonoBehaviour
{
    public static SurpriseToastManager Instance;

    [SerializeField] private GameObject toastPrefab;     // 토스트 프리팹(SurpriseToastUI 포함)
    [SerializeField] private Transform toastParent;      // 토스트가 붙을 부모(보통 Canvas 하위)
    [SerializeField] private float lifeTime = 2.5f;      // 표시 유지 시간(Realtime)
    [SerializeField] private Sprite goldIconSprite;      // 골드 토스트용 아이콘

    private Coroutine hideCo;                            // 자동 숨김 코루틴 핸들

    // 토스트 1개 재사용용 캐시
    private GameObject toastGO;                          // 실제 토스트 오브젝트(1개)
    private SurpriseToastUI toastUI;                     // 토스트 UI 스크립트 캐시

    // 아이콘 캐시(itemNum -> sprite)
    private readonly Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>(64);
    private bool cacheBuilt = false;                     // 캐시 빌드 완료 여부

    // Wait 캐시(Realtime 기반으로 timeScale 영향 X, GC 감소)
    private WaitForSecondsRealtime waitLife;

    private void Awake()
    {
        /*
            싱글톤 중복 방지
            - 이미 Instance가 있고 내가 아니면 파괴
        */
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // lifeTime만큼 기다리는 Wait 객체를 캐싱
        waitLife = new WaitForSecondsRealtime(lifeTime);
    }

    private void OnEnable()
    {
        /*
            (선택) 매니저가 활성화될 때 토스트를 미리 생성해두면
            첫 토스트 호출 시 Instantiate 스파이크를 줄일 수 있다.
        */
        EnsureToast();
    }

    // -------------------------
    // Public API
    // -------------------------

    /*
        외부 API: 스프라이트를 직접 전달해 토스트 표시
        - iconSprite: 표시할 아이콘(없으면 null 가능)
        - message: 표시할 문구
    */
    public void Show(Sprite iconSprite, string message)
    {
        EnsureToast();
        SpawnAndSet(iconSprite, message);
    }

    /*
        외부 API: itemNum 기반으로 아이콘을 찾아 토스트 표시
        - itemNum: 아이템 번호(= 캐시 키)
        - message: 표시할 문구
    */
    public void ShowByItemNum(int itemNum, string message)
    {
        EnsureToast();
        Sprite icon = FindSpriteByItemNumCached(itemNum);
        SpawnAndSet(icon, message);
    }

    /*
        외부 API: 골드 토스트(고정 아이콘 사용)
    */
    public void ShowGold(string msg)
    {
        Show(goldIconSprite, msg);
    }

    // -------------------------
    // Toast Build / Show
    // -------------------------

    /*
        토스트 1회 생성 보장
        - toastGO가 없으면 toastPrefab을 toastParent 아래에 1개 생성하고 비활성화
        - SurpriseToastUI 컴포넌트를 캐싱
    */
    private void EnsureToast()
    {
        if (toastGO != null) return;
        if (toastPrefab == null || toastParent == null) return;

        toastGO = Instantiate(toastPrefab, toastParent);
        toastGO.SetActive(false);

        toastUI = toastGO.GetComponent<SurpriseToastUI>();
    }

    /*
        토스트 표시 공통 처리
        1) 기존 자동숨김 코루틴이 있으면 중단(연속 호출 시 타이머 리셋)
        2) UI 내용 갱신
        3) 토스트 활성화
        4) AutoHideRoutine 시작
    */
    private void SpawnAndSet(Sprite iconSprite, string message)
    {
        if (toastGO == null) return;

        // 기존 코루틴 중단(연속 호출 시 타이머 리셋)
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

        // lifeTime이 런타임에 변경될 수 있다면 여기서 waitLife 재생성 필요
        // waitLife = new WaitForSecondsRealtime(lifeTime);

        // 자동 숨김 시작
        hideCo = StartCoroutine(AutoHideRoutine());
    }

    /*
        일정 시간(lifeTime) 후 토스트 숨김
        - WaitForSecondsRealtime 재사용으로 GC 감소
    */
    private IEnumerator AutoHideRoutine()
    {
        yield return waitLife;

        if (toastGO != null)
            toastGO.SetActive(false);

        hideCo = null;
    }

    // -------------------------
    // Icon Cache
    // -------------------------

    /*
        itemNum으로 아이콘 스프라이트 찾기(캐시 사용)
        1) iconCache에 있으면 즉시 반환
        2) 없고 cacheBuilt=false면 BuildCacheIfPossible()로 1회 빌드 시도
        3) 다시 조회 후 반환, 그래도 없으면 null 반환
    */
    private Sprite FindSpriteByItemNumCached(int itemNum)
    {
        // 캐시에 있으면 즉시 반환
        if (iconCache.TryGetValue(itemNum, out var spr))
            return spr;

        // 캐시를 아직 안 만들었다면 1회 빌드 시도
        if (!cacheBuilt)
            BuildCacheIfPossible();

        // 다시 조회
        if (iconCache.TryGetValue(itemNum, out spr))
            return spr;

        return null;
    }

    /*
        아이콘 캐시 빌드(가능할 때만)
        - ItemManager가 로드 완료 상태일 때
        - SupplyItem 리스트를 스캔하며 (item_num -> itemimg) 매핑 생성
        - item_num이 유니크하다는 전제
    */
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

            iconCache[it.item_num] = it.itemimg;
        }

        cacheBuilt = true;
    }
}
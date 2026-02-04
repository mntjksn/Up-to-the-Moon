using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#region Data Models

[System.Serializable]
public class SupplyItem
{
    // 아이템 이름/설명(표시용)
    public string name;
    public string sub;

    // 아이템 식별 번호(데이터 매칭 용도)
    public int item_num;

    // 가격(게임 밸런스 값)
    public int item_price;

    // 해당 아이템이 해금되기 시작하는 최소 거리(km)
    public int zoneMinKm;

    // Resources 폴더 기준 로드 경로(확장자 제외)
    public string spritePath;

    // 런타임에서만 사용하는 스프라이트 캐시(저장/로드 대상 아님)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class SupplyItemListWrapper
{
    // JsonUtility는 최상위 배열 파싱이 제한되므로 래퍼 구조로 감싼다.
    // JSON 최상위 키가 "SupplyItem"이므로 필드명도 동일하게 유지한다.
    public List<SupplyItem> SupplyItem;
}

#endregion

/*
    ItemManager

    [역할]
    - 공급 아이템 데이터를 JSON에서 로드한다.
    - 현재 거리(km)에 따라 해금된 아이템 목록을 제공한다.
    - Resources.Load를 프레임 분산 처리하여 로딩 스파이크를 완화한다.

    [최적화 포인트]
    - zoneMinKm 오름차순 정렬 후, 해금 조회에서 break를 사용하여 불필요한 순회를 줄인다.
    - GC 발생을 줄이기 위해 NonAlloc 버전 API 및 내부 재사용 버퍼를 제공한다.
*/
public class ItemManager : MonoBehaviour
{
    // 전역 접근을 위한 싱글톤 인스턴스
    public static ItemManager Instance;

    // JSON 키와 동일한 이름을 유지하여 파싱 시 매핑을 단순화한다.
    public List<SupplyItem> SupplyItem = new List<SupplyItem>();

    // 외부에서 로드 완료 타이밍을 보장하기 위한 플래그
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "SupplyItemData.json";

    [Header("Optimization")]
    // Resources.Load가 한 프레임에 몰리면 프레임 드랍/프리징이 발생할 수 있어
    // 프레임당 로드 개수를 제한하여 분산 처리한다(0이면 한 프레임에 모두 로드).
    [SerializeField] private int spriteLoadsPerFrame = 6;

    // 자주 호출되는 해금 조회에서 List 할당을 줄이기 위한 재사용 버퍼
    // 주의: 내부 버퍼이므로 외부에서 보관/수정하지 않고 "읽기 전용"처럼 사용한다.
    private readonly List<SupplyItem> unlockedBuffer = new List<SupplyItem>(64);

    // 중복 로드 방지를 위한 코루틴 핸들
    private Coroutine loadCo;

    private void Awake()
    {
        // 싱글톤 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 씬 전환 중에도 로드가 중복 실행되지 않도록 보호한다.
        if (loadCo == null)
            loadCo = StartCoroutine(LoadSupplyItemRoutine());
    }

    /*
        해금된 아이템 목록을 반환한다(기존 API 유지).

        - 호출부 변경을 최소화하기 위해 List를 새로 생성하여 반환한다.
        - 다만 매 호출마다 List 할당이 발생하므로, 자주 호출되는 경로에서는
          아래 NonAlloc 버전 사용을 권장한다.
    */
    public List<SupplyItem> GetUnlockedByKm(float km)
    {
        var list = new List<SupplyItem>();
        GetUnlockedByKmNonAlloc(km, list);
        return list;
    }

    /*
        해금된 아이템 목록을 results에 채운다(NonAlloc).

        - results 리스트를 외부에서 재사용하면 GC 할당을 0으로 만들 수 있다.
        - zoneMinKm이 오름차순 정렬되어 있다는 전제에서,
          아직 해금되지 않은 구간을 만나면 break하여 순회를 줄인다.
    */
    public void GetUnlockedByKmNonAlloc(float km, List<SupplyItem> results)
    {
        if (results == null) return;
        results.Clear();

        if (SupplyItem == null || SupplyItem.Count == 0)
            return;

        int kmI = (int)km;

        for (int i = 0; i < SupplyItem.Count; i++)
        {
            var it = SupplyItem[i];
            if (it == null) continue;

            if (kmI >= it.zoneMinKm) results.Add(it);
            else break; // 정렬 전제: 이후 항목도 모두 미해금이므로 중단한다.
        }
    }

    /*
        내부 버퍼를 사용하여 해금 목록을 반환한다.

        - 내부 재사용 리스트를 그대로 반환하므로 할당/GC가 발생하지 않는다.
        - 주의: 반환된 리스트는 다음 호출에서 내용이 덮어써지므로
          외부에서 캐싱하거나 수정하지 않고 즉시 사용하도록 설계한다.
    */
    public List<SupplyItem> GetUnlockedByKmCached(float km)
    {
        GetUnlockedByKmNonAlloc(km, unlockedBuffer);
        return unlockedBuffer;
    }

    /*
        데이터 로드 루틴

        로드 흐름:
        1) persistentDataPath에 JSON이 없으면 StreamingAssets에서 복사한다.
        2) JSON 텍스트를 읽고 파싱한다.
        3) zoneMinKm 기준 정렬하여 조회 성능을 확보한다.
        4) 스프라이트를 프레임 분산 로드한다.
    */
    private IEnumerator LoadSupplyItemRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        // 최초 실행 시 persistentDataPath에 데이터가 없을 수 있으므로
        // StreamingAssets에서 복사하여 기준 데이터를 확보한다.
        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        // 복사 실패 또는 파일 미존재 시에도 게임이 멈추지 않도록 빈 데이터로 종료한다.
        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        // 동기 File IO가 프레임을 막지 않도록 한 프레임 양보한다.
        yield return null;

        string json = null;
        try { json = File.ReadAllText(targetPath); }
        catch { json = null; }

        // 비정상 데이터(빈 문자열 등) 방어 처리
        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        LoadFromJson(json);

        // 정렬해두면 GetUnlockedByKmNonAlloc에서 break 최적화가 가능하다.
        SortByZoneMinKm();

        // Resources.Load가 한 프레임에 몰리지 않도록 분산 처리한다.
        yield return LoadSpritesSpread();

        IsLoaded = true;
        loadCo = null;
    }

    /*
        StreamingAssets -> persistentDataPath 복사

        - Android에서는 StreamingAssets가 패키지 내부 경로가 될 수 있어
          File.Copy가 불가능한 경우가 있으므로 UnityWebRequest를 사용한다.
        - 에디터/기타 플랫폼에서는 File.Copy로 처리한다.
    */
    private IEnumerator CopyFromStreamingAssetsIfNeeded(string targetPath)
    {
        string streamingPath = Path.Combine(Application.streamingAssetsPath, JSON_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
        UnityWebRequest req = UnityWebRequest.Get(streamingPath);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try { File.WriteAllText(targetPath, req.downloadHandler.text); }
            catch { }
        }
#else
        if (File.Exists(streamingPath))
        {
            try { File.Copy(streamingPath, targetPath, true); }
            catch { }
        }

        yield break;
#endif
    }

    /*
        JSON 파싱

        지원 형태:
        - 최상위 배열 JSON: [ { ... }, { ... } ]
        - 래퍼 JSON: { "SupplyItem": [ ... ] }

        JsonUtility 제약:
        - 최상위 배열 파싱이 제한되므로 배열 입력은 래핑하여 처리한다.
    */
    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        SupplyItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                string wrapped = "{\"SupplyItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(wrapped);
            }
            else
            {
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        // 파싱 실패 시에도 NullReference가 발생하지 않도록 빈 리스트로 초기화한다.
        SupplyItem = (wrapper != null && wrapper.SupplyItem != null)
            ? wrapper.SupplyItem
            : new List<SupplyItem>();
    }

    // zoneMinKm 오름차순 정렬을 보장하여 해금 조회에서 break 최적화를 가능하게 한다.
    private void SortByZoneMinKm()
    {
        if (SupplyItem == null) return;
        SupplyItem.Sort((a, b) => a.zoneMinKm.CompareTo(b.zoneMinKm));
    }

    /*
        스프라이트 로드 분산 처리

        - Resources.Load는 호출 시점에 비용이 발생할 수 있다.
        - spriteLoadsPerFrame 단위로 yield하여 프레임을 양보한다.
    */
    private IEnumerator LoadSpritesSpread()
    {
        if (SupplyItem == null) yield break;

        int loadedThisFrame = 0;

        for (int i = 0; i < SupplyItem.Count; i++)
        {
            var item = SupplyItem[i];
            if (item == null) continue;
            if (item.itemimg != null) continue;            // 이미 로드된 경우 스킵한다.
            if (string.IsNullOrEmpty(item.spritePath)) continue;

            item.itemimg = Resources.Load<Sprite>(item.spritePath);

            if (spriteLoadsPerFrame > 0)
            {
                loadedThisFrame++;
                if (loadedThisFrame >= spriteLoadsPerFrame)
                {
                    loadedThisFrame = 0;
                    yield return null; // 다음 프레임으로 넘겨 로드를 분산한다.
                }
            }
        }
    }

    // 로드 실패 시에도 시스템이 동작 가능하도록 빈 데이터로 초기화하고 완료 처리한다.
    private void SetEmptyAndFinish()
    {
        SupplyItem = new List<SupplyItem>();
        IsLoaded = true;
    }

    /*
        인덱스로 아이템을 조회한다.

        - 외부 호출 실수(범위 초과 등)를 빠르게 발견하기 위해
          방어 로직과 오류 로그를 포함한다.
    */
    public SupplyItem GetItem(int index)
    {
        if (SupplyItem == null)
        {
            Debug.LogError("[ItemManager] 아이템 리스트가 비어 있습니다.");
            return null;
        }

        if (index < 0 || index >= SupplyItem.Count)
        {
            Debug.LogError("[ItemManager] 잘못된 인덱스 요청: " + index);
            return null;
        }

        return SupplyItem[index];
    }
}
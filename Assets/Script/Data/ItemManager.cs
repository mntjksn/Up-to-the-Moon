using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class SupplyItem
{
    public string name;
    public string sub;

    public int item_num;
    public int item_price;
    public int zoneMinKm;

    // Resources.Load 경로(확장자 제외)
    public string spritePath;

    // 런타임 전용(저장 안 됨)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class SupplyItemListWrapper
{
    // JSON 최상위 키가 "SupplyItem" 이므로 필드명도 동일해야 한다
    public List<SupplyItem> SupplyItem;
}

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance;

    // JSON 키와 동일하게 유지
    public List<SupplyItem> SupplyItem = new List<SupplyItem>();

    // 로드 완료 여부
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "SupplyItemData.json";

    [Header("Optimization")]
    [SerializeField] private int spriteLoadsPerFrame = 6; // 프레임당 Resources.Load 개수(0이면 한 프레임에 전부)

    // (GC 감소용) 재사용 버퍼
    private readonly List<SupplyItem> unlockedBuffer = new List<SupplyItem>(64);

    private Coroutine loadCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadCo == null)
            loadCo = StartCoroutine(LoadSupplyItemRoutine());
    }

    // 기존 API 유지: 호출부 안 깨짐
    // 단, 매번 new List 할당이 발생하므로 자주 호출되는 곳이면 아래 NonAlloc 버전으로 교체 추천
    public List<SupplyItem> GetUnlockedByKm(float km)
    {
        var list = new List<SupplyItem>();
        GetUnlockedByKmNonAlloc(km, list);
        return list;
    }

    // 최적화용: 외부에서 list를 재사용하면 GC 0
    public void GetUnlockedByKmNonAlloc(float km, List<SupplyItem> results)
    {
        if (results == null) return;
        results.Clear();

        if (SupplyItem == null || SupplyItem.Count == 0)
            return;

        int kmI = (int)km;

        // (전제: zoneMinKm 오름차순이면 break로 더 빨라짐)
        for (int i = 0; i < SupplyItem.Count; i++)
        {
            var it = SupplyItem[i];
            if (it == null) continue;

            if (kmI >= it.zoneMinKm) results.Add(it);
            else break; // 정렬되어 있다는 가정이면 여기서 끊어주는 게 이득
        }
    }

    // 내부에서 바로 쓰기 편한 버퍼 반환(읽기 전용처럼 사용)
    // 주의: 반환된 리스트는 다음 호출에서 내용이 바뀜!
    public List<SupplyItem> GetUnlockedByKmCached(float km)
    {
        GetUnlockedByKmNonAlloc(km, unlockedBuffer);
        return unlockedBuffer;
    }

    private IEnumerator LoadSupplyItemRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        // 파일 IO는 한 프레임 양보(로딩 프리즈 완화)
        yield return null;

        string json = null;
        try { json = File.ReadAllText(targetPath); }
        catch { json = null; }

        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        LoadFromJson(json);

        // zoneMinKm 정렬해두면 GetUnlockedByKm에서 break 가능
        SortByZoneMinKm();

        // Resources.Load 프레임 분산
        yield return LoadSpritesSpread();

        IsLoaded = true;
        loadCo = null;
    }

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

    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        SupplyItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱
                string wrapped = "{\"SupplyItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱
                wrapper = JsonUtility.FromJson<SupplyItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        SupplyItem = (wrapper != null && wrapper.SupplyItem != null)
            ? wrapper.SupplyItem
            : new List<SupplyItem>();
    }

    private void SortByZoneMinKm()
    {
        if (SupplyItem == null) return;
        SupplyItem.Sort((a, b) => a.zoneMinKm.CompareTo(b.zoneMinKm));
    }

    private IEnumerator LoadSpritesSpread()
    {
        if (SupplyItem == null) yield break;

        int loadedThisFrame = 0;

        for (int i = 0; i < SupplyItem.Count; i++)
        {
            var item = SupplyItem[i];
            if (item == null) continue;
            if (item.itemimg != null) continue;
            if (string.IsNullOrEmpty(item.spritePath)) continue;

            item.itemimg = Resources.Load<Sprite>(item.spritePath);

            if (spriteLoadsPerFrame > 0)
            {
                loadedThisFrame++;
                if (loadedThisFrame >= spriteLoadsPerFrame)
                {
                    loadedThisFrame = 0;
                    yield return null;
                }
            }
        }
    }

    private void SetEmptyAndFinish()
    {
        SupplyItem = new List<SupplyItem>();
        IsLoaded = true;
    }

    // 리스트 인덱스로 가져오기
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
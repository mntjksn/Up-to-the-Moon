using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class CharacterItem
{
    public string name;
    public string sub;

    public int item_num;
    public long item_price;
    public float item_speed;

    public bool item_unlock;
    public bool item_upgrade;

    // Resources.Load 경로(확장자 제외)
    public string spritePath;

    // 런타임 전용(저장 안됨)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class CharacterItemListWrapper
{
    // JSON 최상위 키가 "CharacterItem" 이므로 필드명도 동일해야 한다
    public List<CharacterItem> CharacterItem;
}

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance;

    public List<CharacterItem> CharacterItem = new List<CharacterItem>();
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "CharacterItemData.json";

    private Coroutine loadCo;

    [Header("Optimization")]
    [SerializeField] private int spriteLoadsPerFrame = 4;     // 프레임당 Resources.Load 개수
    [SerializeField] private float saveDebounceSec = 0.5f;    // 연속 저장 호출 합치기
    [SerializeField] private bool prettyPrint = false;        // 저장 JSON 줄바꿈(기본 false 권장)

    private Coroutine saveCo;
    private bool saveDirty;

    private string TargetPath => Path.Combine(Application.persistentDataPath, JSON_NAME);

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        loadCo = StartCoroutine(LoadCharacterItemRoutine());
    }

    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;

        if (loadCo != null) StopCoroutine(loadCo);
        loadCo = StartCoroutine(LoadCharacterItemRoutine());
    }

    private IEnumerator LoadCharacterItemRoutine()
    {
        IsLoaded = false;

        string targetPath = TargetPath;

        // JSON이 없으면 StreamingAssets에서 복사
        if (!File.Exists(targetPath))
        {
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);
        }

        // 파일이 여전히 없으면 빈 리스트
        if (!File.Exists(targetPath))
        {
            CharacterItem = new List<CharacterItem>();
            IsLoaded = true;
            yield break;
        }

        // JSON 읽기 (동기 IO라 한 프레임 양보 한번)
        yield return null;

        string json = null;
        try
        {
            json = File.ReadAllText(targetPath);
        }
        catch
        {
            json = null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            CharacterItem = new List<CharacterItem>();
            IsLoaded = true;
            yield break;
        }

        json = json.TrimStart();

        CharacterItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                string wrapped = "{\"CharacterItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<CharacterItemListWrapper>(wrapped);
            }
            else
            {
                wrapper = JsonUtility.FromJson<CharacterItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        CharacterItem = (wrapper != null && wrapper.CharacterItem != null)
            ? wrapper.CharacterItem
            : new List<CharacterItem>();

        // 리소스 로드(프레임 분산)
        yield return LoadSpritesSpread();

        IsLoaded = true;
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

    private IEnumerator LoadSpritesSpread()
    {
        if (CharacterItem == null) yield break;

        int loadedThisFrame = 0;

        for (int i = 0; i < CharacterItem.Count; i++)
        {
            var item = CharacterItem[i];
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

    // ---- 저장 최적화 ----
    // 외부에서 기존처럼 SaveToJson() 호출하면 됨.
    // 연속 호출돼도 debounce로 합쳐서 1번만 저장.
    public void SaveToJson()
    {
        saveDirty = true;

        if (!gameObject.activeInHierarchy) return;

        if (saveCo == null)
            saveCo = StartCoroutine(SaveRoutine());
    }

    private IEnumerator SaveRoutine()
    {
        // debounce: 일정 시간 동안 호출이 더 오면 합침
        float t = 0f;
        while (t < saveDebounceSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!saveDirty)
        {
            saveCo = null;
            yield break;
        }

        saveDirty = false;

        // JSON 생성도 한 프레임 양보 후 실행(게임 플레이 순간 멈춤 완화)
        yield return null;

        string targetPath = TargetPath;

        try
        {
            var wrapper = new CharacterItemListWrapper { CharacterItem = this.CharacterItem };
            string json = JsonUtility.ToJson(wrapper, prettyPrint);
            File.WriteAllText(targetPath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CharacterManager] SaveToJson failed: {e.Message}");
        }

        saveCo = null;

        // 저장 중 또 호출됐으면 다시 예약
        if (saveDirty)
            saveCo = StartCoroutine(SaveRoutine());
    }
}
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

    private Coroutine loadCo; // 추가: 코루틴 핸들

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 변경: 코루틴 핸들 저장
        loadCo = StartCoroutine(LoadCharacterItemRoutine());
    }

    // 추가: 외부에서 “즉시 재로딩” 호출용
    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;

        if (loadCo != null) StopCoroutine(loadCo);
        loadCo = StartCoroutine(LoadCharacterItemRoutine());
    }

    private IEnumerator LoadCharacterItemRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        // JSON이 없으면 StreamingAssets에서 복사
        if (!File.Exists(targetPath))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, JSON_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
            UnityWebRequest req = UnityWebRequest.Get(streamingPath);
            yield return req.SendWebRequest();

            if (!req.isNetworkError && !req.isHttpError)
                File.WriteAllText(targetPath, req.downloadHandler.text);
#else
            if (File.Exists(streamingPath))
                File.Copy(streamingPath, targetPath, true);
#endif
        }

        // 파일이 여전히 없으면 빈 리스트
        if (!File.Exists(targetPath))
        {
            CharacterItem = new List<CharacterItem>();
            IsLoaded = true;
            yield break;
        }

        // JSON 읽기
        string json = File.ReadAllText(targetPath);
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

        // 리소스 로드(1회)
        for (int i = 0; i < CharacterItem.Count; i++)
        {
            var item = CharacterItem[i];
            if (!string.IsNullOrEmpty(item.spritePath))
                item.itemimg = Resources.Load<Sprite>(item.spritePath);
        }

        IsLoaded = true;
        yield break;
    }

    public void SaveToJson()
    {
        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        var wrapper = new CharacterItemListWrapper { CharacterItem = this.CharacterItem };
        string json = JsonUtility.ToJson(wrapper, true);

        File.WriteAllText(targetPath, json);
    }

    public CharacterItem GetItem(int id)
    {
        if (CharacterItem == null) return null;
        if (id < 0 || id >= CharacterItem.Count) return null;
        return CharacterItem[id];
    }
}
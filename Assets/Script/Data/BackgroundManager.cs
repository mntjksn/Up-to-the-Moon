using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class BackgroundItem
{
    public string name;
    public string sub;

    public int item_num;
    public long zoneMinKm;

    // Resources.Load 경로(확장자 제외)
    public string spritePath;

    // 런타임 전용(저장 안됨)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class BackgroundItemListWrapper
{
    // JSON 최상위 키가 "BackgroundItem" 이므로 필드명도 동일해야 한다
    public List<BackgroundItem> BackgroundItem;
}

public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance;

    // 아이템 목록
    public List<BackgroundItem> BackgroundItem = new List<BackgroundItem>();

    // 로드 완료 여부(다른 스크립트에서 접근 타이밍 방지용)
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "BackgroundItemData.json";

    private void Awake()
    {
        // 싱글톤 유지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(LoadBackgroundItemRoutine());
    }

    // 배경은 "현재 km에 해당하는 1개"만 쓰는게 핵심
    // - zoneMinKm 오름차순 정렬되어 있다는 전제(지금 JSON처럼)
    // - 0km부터 시작하려면 첫 항목 zoneMinKm=0 권장
    public BackgroundItem GetCurrentByKm(float km)
    {
        if (BackgroundItem == null || BackgroundItem.Count == 0) return null;

        BackgroundItem current = BackgroundItem[0]; // 기본(최초 배경)

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            if (km >= BackgroundItem[i].zoneMinKm)
                current = BackgroundItem[i];
            else
                break; // 오름차순이면 여기서 끊는게 빠름
        }

        return current;
    }

    // (옵션) 기존처럼 해금 리스트가 필요하면 유지 가능
    public List<BackgroundItem> GetUnlockedByKm(float km)
    {
        var list = new List<BackgroundItem>();
        if (BackgroundItem == null) return list;

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            if (km >= BackgroundItem[i].zoneMinKm)
                list.Add(BackgroundItem[i]);
            else
                break; // 오름차순이면 더 이상 볼 필요 없음
        }
        return list;
    }

    private IEnumerator LoadBackgroundItemRoutine()
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

            if (req.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(targetPath, req.downloadHandler.text);
            }
#else
            if (File.Exists(streamingPath))
                File.Copy(streamingPath, targetPath, true);
#endif
        }

        // 파일이 여전히 없으면 빈 리스트
        if (!File.Exists(targetPath))
        {
            BackgroundItem = new List<BackgroundItem>();
            IsLoaded = true;
            yield break;
        }

        // JSON 읽기
        string json = File.ReadAllText(targetPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            BackgroundItem = new List<BackgroundItem>();
            IsLoaded = true;
            yield break;
        }

        json = json.TrimStart();

        // 배열 JSON / wrapper JSON 모두 대응
        BackgroundItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열만 있을 경우 "BackgroundItem"로 감싸서 파싱
                string wrapped = "{\"BackgroundItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<BackgroundItemListWrapper>(wrapped);
            }
            else
            {
                // {"BackgroundItem":[...]} 형태면 그대로 파싱
                wrapper = JsonUtility.FromJson<BackgroundItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        BackgroundItem = (wrapper != null && wrapper.BackgroundItem != null)
            ? wrapper.BackgroundItem
            : new List<BackgroundItem>();

        // 안전: zoneMinKm 기준 오름차순 정렬(혹시 JSON 순서가 꼬여도 대비)
        BackgroundItem.Sort((a, b) => a.zoneMinKm.CompareTo(b.zoneMinKm));

        // 리소스 로드(1회)
        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            var item = BackgroundItem[i];
            if (!string.IsNullOrEmpty(item.spritePath))
                item.itemimg = Resources.Load<Sprite>(item.spritePath);
        }

        IsLoaded = true;
        yield break;
    }

    // 리스트 인덱스로 가져오기
    public BackgroundItem GetItem(int index)
    {
        if (index < 0 || index >= BackgroundItem.Count)
        {
            Debug.LogError("[BackgroundManager] 잘못된 인덱스 요청: " + index);
            return null;
        }

        return BackgroundItem[index];
    }

    public BackgroundItem GetBackgroundByKm(float km)
    {
        if (BackgroundItem == null || BackgroundItem.Count == 0)
            return null;

        BackgroundItem result = null;

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            if (km >= BackgroundItem[i].zoneMinKm)
            {
                // 조건 만족하는 것 중 가장 높은 단계 선택
                if (result == null ||
                    BackgroundItem[i].zoneMinKm > result.zoneMinKm)
                {
                    result = BackgroundItem[i];
                }
            }
        }

        return result;
    }
}
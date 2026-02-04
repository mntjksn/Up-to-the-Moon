using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#region Data Models

[System.Serializable]
public class BackgroundItem
{
    // 배경 이름(표시용)
    public string name;

    // 배경 부가 설명(표시용)
    public string sub;

    // 배경 식별 번호(데이터 매칭 용도)
    public int item_num;

    // 해당 배경이 적용되기 시작하는 최소 거리(km)
    public long zoneMinKm;

    // Resources 폴더 기준 로드 경로(확장자 제외)
    // 예: "Backgrounds/bg_01"
    public string spritePath;

    // 런타임에서만 사용하는 스프라이트 캐시(저장/로드 대상 아님)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class BackgroundItemListWrapper
{
    // JsonUtility는 최상위가 배열일 때 직접 파싱이 제한되므로
    // "BackgroundItem" 키로 감싸는 래퍼 구조를 사용한다.
    public List<BackgroundItem> BackgroundItem;
}

#endregion

/*
    BackgroundManager

    [역할]
    - JSON 파일에서 배경 데이터를 로드한다.
    - zoneMinKm(거리) 기준으로 현재 적용될 배경을 빠르게 조회한다.
    - Resources.Load를 프레임 분산 처리하여 모바일 프리징을 완화한다.

    [설계 의도]
    - 데이터(BackgroundItem)와 조회/로딩 로직을 분리하여 유지보수성을 높인다.
    - 거리 기반 조회는 빈번하므로 정렬 + 이진 탐색(O(log n))으로 최적화한다.
    - 파일 IO 및 스프라이트 로드는 스파이크가 발생할 수 있어 코루틴으로 제어한다.
*/
public class BackgroundManager : MonoBehaviour
{
    // 전역 접근을 위한 싱글톤 인스턴스
    public static BackgroundManager Instance;

    // JSON 키와 동일한 이름을 유지하여 파싱 시 매핑을 단순화한다.
    public List<BackgroundItem> BackgroundItem = new List<BackgroundItem>();

    // 외부에서 로드 완료 타이밍을 보장하기 위한 플래그
    public bool IsLoaded { get; private set; }

    // persistentDataPath에 저장될 JSON 파일명
    private const string JSON_NAME = "BackgroundItemData.json";

    [Header("Optimization")]
    // Resources.Load가 한 프레임에 몰리면 프리징이 발생할 수 있으므로
    // 프레임당 로드 개수를 제한하여 분산 처리한다(0이면 한 프레임에 모두 로드).
    [SerializeField] private int spriteLoadsPerFrame = 4;

    // 일부 기기에서 File IO 시 프리징이 체감될 수 있어
    // IO 전/후에 프레임을 양보할 수 있도록 옵션을 둔다.
    [SerializeField] private bool yieldDuringFileIO = false;

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
            loadCo = StartCoroutine(LoadBackgroundItemRoutine());
    }

    /*
        km 기준으로 현재 적용될 배경 1개를 반환한다.

        전제 조건:
        - BackgroundItem은 zoneMinKm 오름차순으로 정렬되어 있어야 한다.
        - 로드 루틴에서 SortByZoneMinKm()을 수행한다.

        구현 방식:
        - "km 이하인 zoneMinKm 중 가장 큰 값"을 찾기 위해 이진 탐색을 사용한다.
        - 조회 빈도가 높아질 수 있으므로 O(log n)으로 최적화한다.
    */
    public BackgroundItem GetByKm(float km)
    {
        if (BackgroundItem == null || BackgroundItem.Count == 0)
            return null;

        long kmL = (long)km;

        int lo = 0;
        int hi = BackgroundItem.Count - 1;

        // kmL 이하인 값 중 가장 큰 zoneMinKm의 인덱스를 찾는다.
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long midVal = BackgroundItem[mid].zoneMinKm;

            if (midVal <= kmL)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        // hi가 최종 후보 인덱스가 되며, 범위를 벗어날 수 있어 Clamp로 보정한다.
        int idx = Mathf.Clamp(hi, 0, BackgroundItem.Count - 1);
        return BackgroundItem[idx];
    }

    /*
        배경 데이터 로드 루틴

        로드 흐름:
        1) persistentDataPath에 JSON이 없으면 StreamingAssets에서 복사한다.
        2) JSON 텍스트를 읽고 파싱한다.
        3) zoneMinKm 기준 정렬한다.
        4) 스프라이트를 프레임 분산 로드한다.
    */
    private IEnumerator LoadBackgroundItemRoutine()
    {
        IsLoaded = false;

        string targetPath = Path.Combine(Application.persistentDataPath, JSON_NAME);

        // 최초 실행 시 persistentDataPath에 데이터가 없을 수 있으므로
        // StreamingAssets에서 복사하여 기준 데이터를 확보한다.
        if (!File.Exists(targetPath))
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);

        // 복사 실패 또는 파일 미존재 시 빈 데이터로 종료한다.
        if (!File.Exists(targetPath))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        if (yieldDuringFileIO) yield return null;

        // File IO는 예외가 발생할 수 있으므로 안전하게 처리한다.
        string json = null;
        try
        {
            json = File.ReadAllText(targetPath);
        }
        catch
        {
            json = null;
        }

        if (yieldDuringFileIO) yield return null;

        // 비정상 데이터(빈 문자열 등)인 경우에도 게임이 멈추지 않도록 방어한다.
        if (string.IsNullOrWhiteSpace(json))
        {
            SetEmptyAndFinish();
            loadCo = null;
            yield break;
        }

        LoadFromJson(json);
        SortByZoneMinKm();

        // Resources.Load가 한 프레임에 몰리지 않도록 분산 처리한다.
        yield return LoadSpritesSpread();

        IsLoaded = true;
        loadCo = null;
    }

    /*
        StreamingAssets -> persistentDataPath 복사

        - Android에서는 StreamingAssets가 압축 패키지 내부에 존재할 수 있어
          일반 File IO가 불가능하므로 UnityWebRequest로 읽는다.
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
            try
            {
                File.WriteAllText(targetPath, req.downloadHandler.text);
            }
            catch { }
        }
#else
        if (File.Exists(streamingPath))
        {
            try
            {
                File.Copy(streamingPath, targetPath, true);
            }
            catch { }
        }

        yield break;
#endif
    }

    /*
        JSON 파싱

        지원 형태:
        - 최상위가 배열인 JSON: [ { ... }, { ... } ]
        - 래퍼 형태 JSON: { "BackgroundItem": [ ... ] }

        JsonUtility 제약:
        - 최상위 배열 파싱이 제한되므로 배열 입력은 래핑하여 처리한다.
    */
    private void LoadFromJson(string json)
    {
        json = json.TrimStart();

        BackgroundItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 배열 JSON인 경우 wrapper 형태로 감싸서 파싱한다.
                string wrapped = "{\"BackgroundItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<BackgroundItemListWrapper>(wrapped);
            }
            else
            {
                // wrapper JSON인 경우 그대로 파싱한다.
                wrapper = JsonUtility.FromJson<BackgroundItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        // 파싱 실패 시에도 NullReference가 발생하지 않도록 빈 리스트로 초기화한다.
        BackgroundItem = (wrapper != null && wrapper.BackgroundItem != null)
            ? wrapper.BackgroundItem
            : new List<BackgroundItem>();
    }

    // 이진 탐색을 위해 zoneMinKm 오름차순 정렬을 보장한다.
    private void SortByZoneMinKm()
    {
        if (BackgroundItem == null) return;
        BackgroundItem.Sort((a, b) => a.zoneMinKm.CompareTo(b.zoneMinKm));
    }

    /*
        스프라이트 로드 분산 처리

        - Resources.Load는 호출 시점에 로딩 비용이 발생할 수 있다.
        - 모바일에서 한 프레임에 다량 로드하면 프레임 드랍/프리징이 발생할 수 있어
          spriteLoadsPerFrame 단위로 나누어 yield로 프레임을 양보한다.
    */
    private IEnumerator LoadSpritesSpread()
    {
        if (BackgroundItem == null) yield break;

        int loadedThisFrame = 0;

        for (int i = 0; i < BackgroundItem.Count; i++)
        {
            var item = BackgroundItem[i];
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
        BackgroundItem = new List<BackgroundItem>();
        IsLoaded = true;
    }
}
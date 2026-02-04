using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#region Data Models

[System.Serializable]
public class CharacterItem
{
    // 캐릭터 이름/설명(표시용)
    public string name;
    public string sub;

    // 캐릭터 식별 번호(데이터 매칭 용도)
    public int item_num;

    // 가격 및 이동 속도(게임 밸런스 값)
    public long item_price;
    public float item_speed;

    // 해금/업그레이드 상태(저장 대상)
    public bool item_unlock;
    public bool item_upgrade;

    // Resources 폴더 기준 로드 경로(확장자 제외)
    // 예: "Characters/char_01"
    public string spritePath;

    // 런타임에서만 사용하는 스프라이트 캐시(저장/로드 대상 아님)
    [System.NonSerialized] public Sprite itemimg;
}

[System.Serializable]
public class CharacterItemListWrapper
{
    // JsonUtility는 최상위 배열 파싱이 제한되므로 래퍼 구조로 감싼다.
    // JSON 최상위 키가 "CharacterItem"이므로 필드명도 동일하게 유지한다.
    public List<CharacterItem> CharacterItem;
}

#endregion

/*
    CharacterManager

    [역할]
    - 캐릭터 데이터(JSON)를 로드/저장한다.
    - Resources.Load를 프레임 분산 처리하여 로딩 스파이크를 완화한다.
    - 저장은 디바운스(debounce) 방식으로 묶어 IO 호출 빈도를 줄인다.

    [설계 의도]
    - 모바일 환경에서 File IO / Resources.Load로 인한 프리징을 줄이기 위해 코루틴 기반으로 제어한다.
    - persistentDataPath를 사용하여 플레이 중 변경된 해금/업그레이드 상태를 유지한다.
*/
public class CharacterManager : MonoBehaviour
{
    // 전역 접근을 위한 싱글톤 인스턴스
    public static CharacterManager Instance;

    // JSON 키와 동일한 이름을 유지하여 파싱 시 매핑을 단순화한다.
    public List<CharacterItem> CharacterItem = new List<CharacterItem>();

    // 외부에서 로드 완료 타이밍을 보장하기 위한 플래그
    public bool IsLoaded { get; private set; }

    private const string JSON_NAME = "CharacterItemData.json";

    // 중복 로드 방지를 위한 코루틴 핸들
    private Coroutine loadCo;

    [Header("Optimization")]
    // Resources.Load가 한 프레임에 몰리면 프레임 드랍이 발생할 수 있어
    // 프레임당 로드 개수를 제한하여 분산 처리한다(0이면 한 프레임에 모두 로드).
    [SerializeField] private int spriteLoadsPerFrame = 4;

    // 저장 요청이 연속으로 들어오는 경우(업그레이드 연타 등)
    // 일정 시간 동안의 호출을 합쳐 실제 파일 저장을 1회로 줄인다.
    [SerializeField] private float saveDebounceSec = 0.5f;

    // 저장 JSON을 보기 좋게 줄바꿈할지 여부
    // 용량/성능을 고려하여 기본은 false를 권장한다.
    [SerializeField] private bool prettyPrint = false;

    // 저장 코루틴 및 저장 예약 플래그
    private Coroutine saveCo;
    private bool saveDirty;

    // 실제 저장 위치는 persistentDataPath를 사용한다.
    // (앱 재실행 후에도 플레이 데이터 유지 목적)
    private string TargetPath => Path.Combine(Application.persistentDataPath, JSON_NAME);

    private void Awake()
    {
        // 싱글톤 중복 생성 방지
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 최초 진입 시 데이터 로드를 시작한다.
        loadCo = StartCoroutine(LoadCharacterItemRoutine());
    }

    /*
        데이터 재로드

        - 개발/테스트 중 JSON 교체 또는 초기화 상황을 고려해 제공한다.
        - 이미 로드 중이면 기존 코루틴을 중단하고 다시 시작한다.
    */
    public void Reload()
    {
        if (!gameObject.activeInHierarchy) return;

        if (loadCo != null) StopCoroutine(loadCo);
        loadCo = StartCoroutine(LoadCharacterItemRoutine());
    }

    /*
        캐릭터 데이터 로드 루틴

        로드 흐름:
        1) persistentDataPath에 JSON이 없으면 StreamingAssets에서 복사한다.
        2) JSON 텍스트를 읽고 파싱한다.
        3) 스프라이트를 프레임 분산 로드한다.
    */
    private IEnumerator LoadCharacterItemRoutine()
    {
        IsLoaded = false;

        string targetPath = TargetPath;

        // 최초 실행 시 persistentDataPath에 데이터가 없을 수 있으므로
        // StreamingAssets에서 복사하여 기준 데이터를 확보한다.
        if (!File.Exists(targetPath))
        {
            yield return CopyFromStreamingAssetsIfNeeded(targetPath);
        }

        // 복사 실패 또는 파일 미존재 시에도 게임이 멈추지 않도록 빈 리스트로 처리한다.
        if (!File.Exists(targetPath))
        {
            CharacterItem = new List<CharacterItem>();
            IsLoaded = true;
            yield break;
        }

        // 동기 File IO로 인한 프리징 체감을 줄이기 위해 한 프레임 양보한다.
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

        // 비정상 데이터(빈 문자열 등) 방어 처리
        if (string.IsNullOrWhiteSpace(json))
        {
            CharacterItem = new List<CharacterItem>();
            IsLoaded = true;
            yield break;
        }

        // JsonUtility 파싱 편의를 위해 선행 공백을 제거한다.
        json = json.TrimStart();

        CharacterItemListWrapper wrapper = null;

        try
        {
            if (json.StartsWith("["))
            {
                // 최상위가 배열인 JSON을 래퍼 형태로 감싸 파싱한다.
                string wrapped = "{\"CharacterItem\":" + json + "}";
                wrapper = JsonUtility.FromJson<CharacterItemListWrapper>(wrapped);
            }
            else
            {
                // 래퍼 JSON인 경우 그대로 파싱한다.
                wrapper = JsonUtility.FromJson<CharacterItemListWrapper>(json);
            }
        }
        catch
        {
            wrapper = null;
        }

        // 파싱 실패 시에도 NullReference가 발생하지 않도록 빈 리스트로 초기화한다.
        CharacterItem = (wrapper != null && wrapper.CharacterItem != null)
            ? wrapper.CharacterItem
            : new List<CharacterItem>();

        // Resources.Load가 한 프레임에 몰리지 않도록 분산 처리한다.
        yield return LoadSpritesSpread();

        IsLoaded = true;
    }

    /*
        StreamingAssets -> persistentDataPath 복사

        - Android에서는 StreamingAssets가 패키지 내부 경로가 될 수 있어
          File.Copy로 직접 접근이 불가능하므로 UnityWebRequest를 사용한다.
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
        스프라이트 로드 분산 처리

        - Resources.Load는 호출 시점에 로딩 비용이 발생할 수 있다.
        - spriteLoadsPerFrame 단위로 yield하여 프레임을 양보한다.
    */
    private IEnumerator LoadSpritesSpread()
    {
        if (CharacterItem == null) yield break;

        int loadedThisFrame = 0;

        for (int i = 0; i < CharacterItem.Count; i++)
        {
            var item = CharacterItem[i];
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

    // -----------------------
    // 저장 최적화
    // -----------------------

    /*
        외부 호출용 저장 함수

        - 기존 코드에서는 SaveToJson()을 필요 시점마다 호출한다.
        - 이 함수는 실제 파일 쓰기를 즉시 수행하지 않고 "저장 예약"만 한다.
        - 짧은 시간에 여러 번 호출되어도 debounce로 묶어 1회 저장으로 처리한다.
    */
    public void SaveToJson()
    {
        saveDirty = true;

        // 오브젝트가 비활성 상태면 코루틴 시작이 불가능하므로 예약만 유지한다.
        if (!gameObject.activeInHierarchy) return;

        // 이미 저장 코루틴이 실행 중이면 추가 호출은 dirty 플래그로만 누적한다.
        if (saveCo == null)
            saveCo = StartCoroutine(SaveRoutine());
    }

    /*
        저장 루틴

        - debounce 시간 동안 추가 호출을 기다린 뒤 실제 저장을 수행한다.
        - JSON 생성 및 파일 쓰기 전 한 프레임 양보하여 체감 프리징을 완화한다.
        - 저장 중 다시 호출되면(saveDirty=true) 종료 후 재예약한다.
    */
    private IEnumerator SaveRoutine()
    {
        float t = 0f;
        while (t < saveDebounceSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 기다리는 동안 저장 예약이 해제되었으면 종료한다.
        if (!saveDirty)
        {
            saveCo = null;
            yield break;
        }

        saveDirty = false;

        // JSON 생성/파일 쓰기 전에 프레임을 양보하여 플레이 순간 멈춤을 완화한다.
        yield return null;

        string targetPath = TargetPath;

        try
        {
            // 저장 시에는 런타임 캐시(itemimg)는 직렬화 대상이 아니므로 제외된다.
            var wrapper = new CharacterItemListWrapper { CharacterItem = this.CharacterItem };
            string json = JsonUtility.ToJson(wrapper, prettyPrint);
            File.WriteAllText(targetPath, json);
        }
        catch (System.Exception e)
        {
            // 저장 실패는 치명적 예외로 처리하지 않고 경고 로그로 남긴다.
            Debug.LogWarning($"[CharacterManager] SaveToJson failed: {e.Message}");
        }

        saveCo = null;

        // 저장 중 추가 호출이 있었으면 다시 저장을 예약한다.
        if (saveDirty)
            saveCo = StartCoroutine(SaveRoutine());
    }
}
using System;
using System.IO;
using UnityEngine;
using System.Collections;

#region Save Data Models

/*
    SaveData

    [역할]
    - 플레이어 진행 데이터(골드/거리/속도/현재 캐릭터 등)를 직렬화하여 저장한다.
    - 블랙홀 업그레이드, 부스트 상태, 자원 보유량 등 게임 핵심 상태를 포함한다.

    [설계 의도]
    - 저장 데이터 구조를 하나의 루트 클래스에 모아 버전 관리(Fixup)와 안정성을 확보한다.
    - 런타임 전용 값과 저장 대상 값을 명확히 구분하여 저장 오류를 줄인다.
*/
[Serializable]
public class SaveData
{
    public Player player = new Player();
    public BlackHole blackHole = new BlackHole();
    public Boost boost = new Boost();

    [Serializable]
    public class Player
    {
        // 재화 및 진행 데이터
        public long gold = 0;
        public float km = 0;

        // 현재 선택된 캐릭터
        public int currentCharacterId = 0;

        // 이동 속도(게임 플레이 핵심 값)
        public float speed = 0.01f;
    }

    [Serializable]
    public class BlackHole
    {
        // 업그레이드 레벨
        public int blackholeIncomeLv = 0;
        public int blackholeStorageLv = 0;

        // 주의: 기존 저장 데이터 호환을 위해 오타 필드명을 유지한다.
        // 저장 데이터 필드명 변경은 구버전 세이브를 깨뜨릴 수 있으므로 Fixup과 함께 관리한다.
        public float BalckHoleIncome = 0.5f;

        // 자원 저장 최대치
        public long BlackHoleStorageMax = 100;
    }

    [Serializable]
    public class Boost
    {
        // 부스트 설정 값
        public float boostSpeed = 25f;
        public float boostTime = 1f;
        public float boostCoolTime = 60f;
        public bool boostUnlock = false;

        // 업그레이드 가격
        public long boostSpeedPrice = 1000;
        public long boostTimePrice = 500;

        // 부스트/쿨다운 종료 시각(유닉스 ms)
        // 앱 종료 후 재접속 시에도 타이머 상태를 복원하기 위한 저장 값이다.
        public long boostEndUnixMs = 0;
        public long cooldownEndUnixMs = 0;

        // 부스트 발동 전 기본 속도(복구에 사용)
        public float baseSpeedBeforeBoost = 0;
    }

    // 자원 보유량(고정 크기 배열): 인덱스 = 자원 id(0~29)
    // 배열 길이가 바뀌면 구버전 세이브와 충돌할 수 있으므로 Fixup에서 보정한다.
    public int[] resources = new int[30];
}

#endregion

/*
    SaveManager

    [역할]
    - SaveData를 JSON 파일로 저장/로드한다.
    - 데이터 변경 시 UI/시스템이 반응할 수 있도록 이벤트를 제공한다.
    - 모바일 환경에서 저장 IO로 인한 프리즈를 줄이기 위해 디바운스 저장을 적용한다.
    - 앱 백그라운드/종료 시 저장 누락을 방지하기 위해 강제 저장 로직을 포함한다.

    [핵심 설계 포인트]
    1) 저장 디바운스: Save() 연타를 합쳐 File.WriteAllText 빈도를 줄인다.
    2) 안전 보정(Fixup): 로드 후 null/범위/배열 길이 등 구버전 데이터 호환을 보장한다.
    3) 이벤트 기반 갱신: 데이터 변경 시 UI가 직접 조회하지 않아도 갱신되도록 한다.
*/
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    // 외부에서 읽기만 가능하도록 캡슐화한다.
    public SaveData Data { get; private set; }

    // UI/시스템 갱신을 위한 이벤트
    public event Action OnResourceChanged;
    public event Action OnGoldChanged;
    public event Action<float> OnSpeedChanged;
    public event Action<int> OnCharacterChanged;
    public event Action<bool> OnBoostUnlockChanged;

    private const string FILE_NAME = "save.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    // 골드 상한을 두어 오버플로 및 밸런스 붕괴를 방지한다.
    public const long GOLD_MAX = 9_000_000_000_000_000_000;

    [Header("Optimization - Save")]
    // 저장 요청이 연속으로 발생할 수 있으므로(재화 증가, 자원 획득 등)
    // 일정 시간 동안의 Save() 호출을 합쳐 실제 파일 저장 횟수를 줄인다.
    [SerializeField] private float saveDebounceSec = 0.5f;

    // 저장 JSON을 보기 좋게 줄바꿈할지 여부
    // 용량/문자열 생성/GC를 고려하여 기본은 false를 권장한다.
    [SerializeField] private bool prettyPrint = false;

    // 앱이 백그라운드로 가거나 종료될 때 저장 누락을 방지하기 위한 옵션
    [SerializeField] private bool saveOnPauseQuit = true;

    private Coroutine saveCo;
    private bool saveDirty;
    private bool isQuitting;
    private bool saveInProgress;

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

        Load();
    }

    /*
        새 게임 시작

        - 새 SaveData를 생성하고 Fixup으로 기본값을 보정한다.
        - Save()를 호출하여 파일을 생성/갱신한다(실제 저장은 디바운스로 합쳐진다).
    */
    public void NewGame()
    {
        Data = new SaveData();
        Fixup();
        Save();
    }

    /*
        기존 코드 호환: 어디서든 Save() 호출 가능하도록 유지한다.

        - 즉시 파일 저장을 수행하지 않고 디바운스 저장으로 합쳐 처리한다.
        - 앱 종료 시점에는 저장 누락이 발생할 수 있으므로 ForceSaveNow()로 즉시 저장한다.
    */
    public void Save()
    {
        if (Data == null)
            Data = new SaveData();

        Fixup();

        saveDirty = true;

        // 비활성 상태에서는 코루틴 시작이 불가능하므로 예약만 유지한다.
        if (!gameObject.activeInHierarchy) return;

        // 종료 중에는 디바운스 대기 없이 즉시 저장한다.
        if (isQuitting)
        {
            ForceSaveNow();
            return;
        }

        if (saveCo == null)
            saveCo = StartCoroutine(SaveRoutine());
    }

    /*
        디바운스 저장 루틴

        - saveDebounceSec 동안 추가 Save() 호출을 기다린 후 1회 저장한다.
        - 저장 전 한 프레임 양보하여 플레이 중 체감 프리즈를 완화한다.
        - 저장 실패 시 재시도 가능하도록 dirty를 복구한다.
    */
    private IEnumerator SaveRoutine()
    {
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

        saveInProgress = true;
        saveDirty = false;

        // JSON 생성 및 파일 쓰기 전에 프레임을 양보한다.
        yield return null;

        try
        {
            string json = JsonUtility.ToJson(Data, prettyPrint);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] Save 실패: " + e.Message);
            // 실패 시 다시 저장 예약이 가능하도록 dirty를 복구한다.
            saveDirty = true;
        }

        saveInProgress = false;
        saveCo = null;

        // 저장 중 추가 변경이 발생했다면 다시 예약한다.
        if (saveDirty && !isQuitting)
            saveCo = StartCoroutine(SaveRoutine());
    }

    /*
        즉시 저장

        - 앱 종료/백그라운드 전환 등 저장 누락 위험이 높은 시점에 사용한다.
        - 디바운스 대기 없이 바로 디스크에 기록한다.
    */
    private void ForceSaveNow()
    {
        if (Data == null) Data = new SaveData();
        Fixup();

        try
        {
            string json = JsonUtility.ToJson(Data, prettyPrint);
            File.WriteAllText(SavePath, json);
        }
        catch
        {
            // 종료 직전에는 사용자 경험을 위해 예외를 치명적으로 처리하지 않는다.
        }
        finally
        {
            saveDirty = false;
        }
    }

    /*
        저장 데이터 로드

        - 파일이 없으면 기본 데이터를 생성하고 즉시 저장하여 파일을 만든다.
        - 로드 실패(파일 손상 등) 시에도 게임이 실행되도록 기본값으로 복구한다.
        - 로드 후 Fixup으로 null/배열 길이/범위 등을 보정한다.
    */
    public void Load()
    {
        if (!File.Exists(SavePath))
        {
            Data = new SaveData();
            Fixup();
            ForceSaveNow();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            Data = JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            Data = new SaveData();
        }

        Fixup();
    }

    /*
        로드 후 데이터 안전 보정(Fixup)

        - 구버전 세이브 또는 손상된 세이브에서 NullReference가 발생하지 않도록 방어한다.
        - 값의 범위를 제한하여 비정상 값이 게임 밸런스를 깨뜨리지 않도록 한다.
        - 배열 길이 변경 이슈를 대비해 resources 길이를 고정(30)으로 보정한다.
    */
    private void Fixup()
    {
        if (Data == null) Data = new SaveData();

        if (Data.player == null) Data.player = new SaveData.Player();
        if (Data.blackHole == null) Data.blackHole = new SaveData.BlackHole();
        if (Data.boost == null) Data.boost = new SaveData.Boost();

        // 부스트 값 보정(비정상/구버전 값 방어)
        if (Data.boost.boostCoolTime <= 0f)
            Data.boost.boostCoolTime = 60f;

        if (Data.boost.boostTime > 45f)
            Data.boost.boostTime = 45f;

        if (Data.boost.boostTime < 0f)
            Data.boost.boostTime = 0f;

        if (Data.boost.boostSpeedPrice <= 0)
            Data.boost.boostSpeedPrice = 1000;

        if (Data.boost.boostTimePrice <= 0)
            Data.boost.boostTimePrice = 500;

        // 자원 배열 길이 보정(구버전 호환)
        if (Data.resources == null || Data.resources.Length != 30)
        {
            int[] newArr = new int[30];

            if (Data.resources != null)
            {
                int copy = Mathf.Min(Data.resources.Length, 30);
                for (int i = 0; i < copy; i++)
                    newArr[i] = Data.resources[i];
            }

            Data.resources = newArr;
        }
    }

    /*
        전체 데이터 초기화

        - save.json 뿐 아니라 일부 로컬 데이터 파일을 함께 삭제한다.
        - 삭제 후 즉시 save.json을 재생성하여 다음 실행에서 로드 실패를 방지한다.
        - 관련 매니저들에게 Reload를 호출해 런타임 캐시도 갱신한다.
    */
    public void ResetAllData()
    {
        string[] resetFiles =
        {
            "save.json",
            "CharacterItemData.json",
            "MissionItemData.json"
        };

        for (int i = 0; i < resetFiles.Length; i++)
        {
            string path = Path.Combine(Application.persistentDataPath, resetFiles[i]);

            if (File.Exists(path))
                File.Delete(path);
        }

        Data = new SaveData();
        Fixup();
        ForceSaveNow();

        CharacterManager.Instance?.Reload();
        MissionDataManager.Instance?.Reload();

        Debug.Log("[ResetAllData] Done");
    }

    // -----------------------
    // Player - Gold
    // -----------------------

    public long GetGold()
    {
        return (Data != null && Data.player != null) ? Data.player.gold : 0;
    }

    /*
        골드 증감 처리

        - 상한(GOLD_MAX) 및 하한(0)을 적용하여 오버플로/언더플로를 방지한다.
        - 변경 후 저장을 예약하고, UI 갱신 이벤트 및 미션 진행 값도 함께 갱신한다.
    */
    public void AddGold(long amount)
    {
        if (Data == null) Data = new SaveData();
        if (Data.player == null) Data.player = new SaveData.Player();

        long cur = Data.player.gold;
        long next;

        if (amount >= 0)
        {
            if (cur >= GOLD_MAX) next = GOLD_MAX;
            else
            {
                long space = GOLD_MAX - cur;
                next = (amount >= space) ? GOLD_MAX : (cur + amount);
            }
        }
        else
        {
            long dec = -amount;
            next = (dec >= cur) ? 0 : (cur - dec);
        }

        Data.player.gold = next;

        Save();
        OnGoldChanged?.Invoke();

        // 미션 진행 시스템과의 연결 지점(의존성 최소화를 위해 null-conditional 사용)
        MissionProgressManager.Instance?.SetValue("gold", GetGold());
    }

    // -----------------------
    // Player - Distance
    // -----------------------

    public float GetKm()
    {
        return (Data != null && Data.player != null) ? Data.player.km : 0f;
    }

    public void AddKm(float amount)
    {
        if (Data == null) Data = new SaveData();
        if (Data.player == null) Data.player = new SaveData.Player();

        Data.player.km += amount;
        Save();
    }

    // -----------------------
    // Player - Speed
    // -----------------------

    public float GetSpeed()
    {
        return (Data != null && Data.player != null) ? Data.player.speed : 0f;
    }

    public void SetSpeed(float amount)
    {
        if (Data == null) Data = new SaveData();
        if (Data.player == null) Data.player = new SaveData.Player();

        Data.player.speed = amount;
        Save();
        OnSpeedChanged?.Invoke(amount);
    }

    // -----------------------
    // Player - Character
    // -----------------------

    public int GetCurrentCharacterId()
    {
        return (Data != null && Data.player != null) ? Data.player.currentCharacterId : 0;
    }

    public void SetCurrentCharacterId(int id)
    {
        if (Data == null) Data = new SaveData();
        if (Data.player == null) Data.player = new SaveData.Player();

        if (Data.player.currentCharacterId == id) return;

        Data.player.currentCharacterId = id;
        Save();
        OnCharacterChanged?.Invoke(id);
    }

    // -----------------------
    // Boost
    // -----------------------

    public bool IsBoostUnlocked()
    {
        return Data != null && Data.boost != null && Data.boost.boostUnlock;
    }

    public void SetBoostUnlocked(bool unlocked)
    {
        if (Data == null) Data = new SaveData();
        if (Data.boost == null) Data.boost = new SaveData.Boost();

        if (Data.boost.boostUnlock == unlocked) return;

        Data.boost.boostUnlock = unlocked;
        Save();
        OnBoostUnlockChanged?.Invoke(unlocked);
    }

    public float GetBoostSpeed()
    {
        return (Data != null && Data.boost != null) ? Data.boost.boostSpeed : 0f;
    }

    public void SetBoostSpeed(float speed)
    {
        if (Data == null) Data = new SaveData();
        if (Data.boost == null) Data.boost = new SaveData.Boost();

        Data.boost.boostSpeed = speed;
        Save();

        MissionProgressManager.Instance?.SetValue("boost_speed", speed);
    }

    public float GetBoostTime()
    {
        return (Data != null && Data.boost != null) ? Data.boost.boostTime : 0f;
    }

    public void SetBoostTime(float time)
    {
        if (Data == null) Data = new SaveData();
        if (Data.boost == null) Data.boost = new SaveData.Boost();

        Data.boost.boostTime = time;
        Save();

        MissionProgressManager.Instance?.SetValue("boost_time", time);
    }

    // -----------------------
    // Resources
    // -----------------------

    public int GetResource(int id)
    {
        if (Data == null || Data.resources == null) return 0;
        if (id < 0 || id >= 30) return 0;
        return Data.resources[id];
    }

    /*
        자원 증감 처리

        - 배열 범위를 검증하고, 최소값을 0으로 고정한다.
        - 변경 후 저장 예약 및 UI 갱신 이벤트를 발생시킨다.
    */
    public void AddResource(int id, int amount)
    {
        if (Data == null) Data = new SaveData();
        if (Data.resources == null || Data.resources.Length != 30) Data.resources = new int[30];

        if (id < 0 || id >= 30) return;

        Data.resources[id] = Mathf.Max(0, Data.resources[id] + amount);

        Save();
        OnResourceChanged?.Invoke();

        // 누적 수집량 기반 미션 진행에 반영한다.
        MissionProgressManager.Instance?.Add("resource_collect_total", amount);
    }

    // -----------------------
    // BlackHole
    // -----------------------

    public float GetIncome()
    {
        return (Data != null && Data.blackHole != null) ? Data.blackHole.BalckHoleIncome : 0f;
    }

    public void SetIncome(float value)
    {
        if (Data == null) Data = new SaveData();
        if (Data.blackHole == null) Data.blackHole = new SaveData.BlackHole();

        Data.blackHole.BalckHoleIncome = value;
        Save();
    }

    public int GetIncomeLv()
    {
        return (Data != null && Data.blackHole != null) ? Data.blackHole.blackholeIncomeLv : 0;
    }

    public void AddIncomeLv(int delta = 1)
    {
        if (Data == null) Data = new SaveData();
        if (Data.blackHole == null) Data.blackHole = new SaveData.BlackHole();

        Data.blackHole.blackholeIncomeLv += delta;
        Save();
    }

    public int GetStorageLv()
    {
        return (Data != null && Data.blackHole != null) ? Data.blackHole.blackholeStorageLv : 0;
    }

    public void AddStorageLv(int delta = 1)
    {
        if (Data == null) Data = new SaveData();
        if (Data.blackHole == null) Data.blackHole = new SaveData.BlackHole();

        Data.blackHole.blackholeStorageLv += delta;
        Save();
    }

    // 현재 자원 총합을 계산하여 창고 사용량을 산출한다.
    public long GetStorageUsed()
    {
        if (Data == null || Data.resources == null) return 0;

        long total = 0;
        for (int i = 0; i < Data.resources.Length; i++)
            total += Data.resources[i];

        return total;
    }

    public long GetStorageMax()
    {
        return (Data != null && Data.blackHole != null) ? Data.blackHole.BlackHoleStorageMax : 0;
    }

    public bool IsStorageFull()
    {
        return GetStorageUsed() >= GetStorageMax();
    }

    /*
        앱 백그라운드 전환 처리

        - 모바일에서는 pause 시점 이후 실행이 중단될 수 있어 저장 누락 가능성이 있다.
        - 디바운스 저장 대기 중이거나 저장 진행 중이면 즉시 저장하여 안정성을 확보한다.
    */
    private void OnApplicationPause(bool pause)
    {
        if (!saveOnPauseQuit) return;

        if (pause)
        {
            if (saveDirty || saveInProgress || saveCo != null)
                ForceSaveNow();
        }
    }

    /*
        앱 종료 처리

        - 종료 시점에는 코루틴이 중단될 수 있으므로 디바운스를 기다리지 않고 즉시 저장한다.
    */
    private void OnApplicationQuit()
    {
        isQuitting = true;

        if (!saveOnPauseQuit) return;

        if (saveDirty || saveInProgress || saveCo != null)
            ForceSaveNow();
    }
}
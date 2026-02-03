using System;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveData
{
    public Player player = new Player();
    public BlackHole blackHole = new BlackHole();
    public Boost boost = new Boost();

    [Serializable]
    public class Player
    {
        public long gold = 0;
        public float km = 0;
        public int currentCharacterId = 0;
        public float speed = 0.01f;
    }

    [Serializable]
    public class BlackHole
    {
        public int blackholeIncomeLv = 0;
        public int blackholeStorageLv = 0;

        // 주의: 기존 저장 데이터 호환을 위해 오타 필드명 유지
        public float BalckHoleIncome = 0.5f;
        public long BlackHoleStorageMax = 100;
    }

    [Serializable]
    public class Boost
    {
        public float boostSpeed = 25f;
        public float boostTime = 1f;
        public float boostCoolTime = 60f;
        public bool boostUnlock = false;

        public long boostSpeedPrice = 1000;
        public long boostTimePrice = 500;

        public long boostEndUnixMs = 0;
        public long cooldownEndUnixMs = 0;
    }

    // 광물(자원) 30개 보유량: 인덱스 = 자원 id (0~29)
    public int[] resources = new int[30];
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    public SaveData Data { get; private set; }

    public event Action OnResourceChanged;
    public event Action OnGoldChanged;
    public event Action<float> OnSpeedChanged;
    public event Action<int> OnCharacterChanged;
    public event Action<bool> OnBoostUnlockChanged;

    private const string FILE_NAME = "save.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    public const long GOLD_MAX = 9_000_000_000_000_000_000;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public void NewGame()
    {
        Data = new SaveData();
        Fixup();
        Save();
    }

    public void Save()
    {
        if (Data == null)
            Data = new SaveData();

        Fixup();

        try
        {
            string json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveManager] Save 실패: " + e.Message);
        }
    }

    public void Load()
    {
        if (!File.Exists(SavePath))
        {
            NewGame();
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

    // 로드 후 데이터 안전 보정
    private void Fixup()
    {
        if (Data == null) Data = new SaveData();

        if (Data.player == null) Data.player = new SaveData.Player();
        if (Data.blackHole == null) Data.blackHole = new SaveData.BlackHole();
        if (Data.boost == null) Data.boost = new SaveData.Boost();

        // 부스트 값 보정
        if (Data.boost.boostCoolTime <= 0f)
            Data.boost.boostCoolTime = 60f;

        // 지속시간은 쿨타임보다 작게 유지하고 싶으면 여기서 규칙을 고정
        if (Data.boost.boostTime > 45f)
            Data.boost.boostTime = 45f;

        if (Data.boost.boostTime < 0f)
            Data.boost.boostTime = 0f;

        if (Data.boost.boostSpeedPrice <= 0)
            Data.boost.boostSpeedPrice = 1000;

        if (Data.boost.boostTimePrice <= 0)
            Data.boost.boostTimePrice = 500;

        // resources 배열 길이 보정
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

        // save.json은 즉시 다시 생성
        NewGame();

        // 삭제된 파일 기준으로 즉시 초기화 로드
        CharacterManager.Instance?.Reload();
        MissionDataManager.Instance?.Reload();

        Debug.Log("[ResetAllData] Done");
    }

    // 골드
    public long GetGold()
    {
        return (Data != null && Data.player != null) ? Data.player.gold : 0;
    }

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
            // 음수 입력도 방어(필요 없으면 제거 가능)
            long dec = -amount;
            next = (dec >= cur) ? 0 : (cur - dec);
        }

        Data.player.gold = next;

        Save();
        OnGoldChanged?.Invoke();
        MissionProgressManager.Instance?.SetValue("gold", GetGold());
    }

    // 이동 거리
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

    // 스피드
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

    // 캐릭터
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

    // 부스트 해금
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

    // 자원(광물)
    public int GetResource(int id)
    {
        if (Data == null || Data.resources == null) return 0;
        if (id < 0 || id >= 30) return 0;
        return Data.resources[id];
    }

    public void AddResource(int id, int amount)
    {
        if (Data == null) Data = new SaveData();
        if (Data.resources == null || Data.resources.Length != 30) Data.resources = new int[30];

        if (id < 0 || id >= 30) return;

        Data.resources[id] = Mathf.Max(0, Data.resources[id] + amount);

        Save();
        OnResourceChanged?.Invoke();
        MissionProgressManager.Instance?.Add("resource_collect_total", amount);
    }

    // 블랙홀(수급/창고)
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
}
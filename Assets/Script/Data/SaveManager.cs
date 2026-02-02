using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class SaveData
{
    public Player player = new Player();            // 플레이어
    public BlackHole blackHole = new BlackHole();            // 플레이어
    public Boost boost = new Boost();            // 플레이어

    [System.Serializable]
    public class Player
    {
        public long gold = 0;
        public float km = 0;
        public int currentCharacterId = 0;
        public float speed = 0.01f;
    }

    [System.Serializable]
    public class BlackHole
    {
        public int blackholeIncomeLv = 0;   // 초당 흡수량 Lv
        public int blackholeStorageLv = 0;  // 최대 적재량 Lv

        public float BalckHoleIncome = 0.5f;
        public long BlackHoleStorageMax = 100;
    }

    [System.Serializable]
    public class Boost
    {
        public float boostSpeed = 25f;   // 25%
        public float boostTime = 1f;  // 25%
        public float boostCoolTime = 60f;  // 60초
        public bool boostUnlock = false;

        public long boostSpeedPrice = 1000;
        public long boostTimePrice = 500;

        public long boostEndUnixMs = 0;     // 부스트 끝나는 절대시간(ms)
        public long cooldownEndUnixMs = 0;  // 쿨 끝나는 절대시간(ms)
    }

    // 광물(자원) 30개 보유량: 인덱스 = 자원 id (0~29)
    public int[] resources = new int[30];
}

public class SaveManager : MonoBehaviour
{
    public event System.Action OnResourceChanged;
    public event System.Action OnGoldChanged;

    public static SaveManager Instance;

    public SaveData Data { get; private set; }

    private const string FILE_NAME = "save.json";

    private string SavePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

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
        if (Data == null) Data = new SaveData();
        Fixup();

        string json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(SavePath, json);
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

    // 로드 후 데이터 안전 보정 (진짜 중요)
    private void Fixup()
    {
        if (Data == null) Data = new SaveData();

        if (Data.boost != null)
        {
            if (Data.boost.boostCoolTime <= 0f) Data.boost.boostCoolTime = 60f;

            // 지속시간은 쿨타임(60)을 절대 넘지 못하게
            if (Data.boost.boostTime > 45f)
                Data.boost.boostTime = 45f;

            if (Data.boost.boostTime < 0f)
                Data.boost.boostTime = 0f;
        }

        if (Data.boost.boostSpeedPrice <= 0) Data.boost.boostSpeedPrice = 1000;
        if (Data.boost.boostTimePrice <= 0) Data.boost.boostTimePrice = 500;

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

    // ===== 편의 함수들 =====

    public long GetGold() => Data.player.gold;

    public const long GOLD_MAX = 9_000_000_000_000_000_000;
    public void AddGold(long amount)
    {
        Data.player.gold = (long)Mathf.Min(Data.player.gold + amount, GOLD_MAX);
        Save();
        OnGoldChanged?.Invoke();
        MissionProgressManager.Instance?.SetValue("gold", GetGold());
    }

    public float GetKm() => Data.player.km;
    public void AddKm(float amount)
    {
        Data.player.km += amount;
        Save();
    }

    public float GetSpeed() => Data.player.speed;
    public void SetSpeed(float amount)
    {
        Data.player.speed = amount;
        Save();
        OnSpeedChanged?.Invoke(amount);
    }
    public event System.Action<float> OnSpeedChanged;

    public int GetCurrentCharacterId() => Data.player.currentCharacterId;

    public void SetCurrentCharacterId(int id)
    {
        Data.player.currentCharacterId = id;
        Save();
        OnCharacterChanged?.Invoke(id);
    }
    public System.Action<int> OnCharacterChanged;

    // 현재 해금 여부
    public bool IsBoostUnlocked()
    {
        return Data != null && Data.boost != null && Data.boost.boostUnlock;
    }

    // 해금 처리
    public void SetBoostUnlocked(bool unlocked)
    {
        if (Data == null) Data = new SaveData();
        if (Data.boost == null) Data.boost = new SaveData.Boost();

        if (Data.boost.boostUnlock == unlocked) return;

        Data.boost.boostUnlock = unlocked;
        Save();
        OnBoostUnlockChanged?.Invoke(unlocked);
    }
    public event System.Action<bool> OnBoostUnlockChanged;

    public float GetBoostSpeed() => Data.boost.boostSpeed;

    public void SetBoostSpeed(float speed)
    {
        Data.boost.boostSpeed = speed;
        Save();
        MissionProgressManager.Instance?.SetValue("boost_speed", speed);
    }

    public float GetBoostTime() => Data.boost.boostTime;

    public void SetBoostTime(float time)
    {
        Data.boost.boostTime = time;
        Save();
        MissionProgressManager.Instance?.SetValue("boost_time", time);
    }


    public int GetResource(int id)
    {
        if (id < 0 || id >= 30) return 0;
        return Data.resources[id];
    }

    public void AddResource(int id, int amount)
    {
        if (id < 0 || id >= 30) return;
        Data.resources[id] = Mathf.Max(0, Data.resources[id] + amount);
        Save();
        OnResourceChanged?.Invoke();
        MissionProgressManager.Instance?.Add("resource_collect_total", amount);
    }

    public float GetIncome() => Data.blackHole.BalckHoleIncome;
    public void AddIncome(float Lv) { Data.blackHole.BalckHoleIncome = Lv; Save(); }

    public int GetIncomeLv() => Data.blackHole.blackholeIncomeLv;
    public void AddIncomeLv(int delta = 1) { Data.blackHole.blackholeIncomeLv += delta; Save(); }

    public int GetStorageLv() => Data.blackHole.blackholeStorageLv;
    public void AddStorageLv(int delta = 1) { Data.blackHole.blackholeStorageLv += delta; Save(); }
}
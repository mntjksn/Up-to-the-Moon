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

    [System.Serializable]
    public class Player
    {
        public long gold = 0;
        public float km = 0;
        public float speed = 0.01f;
    }

    [System.Serializable]
    public class BlackHole
    {
        public int blackholeIncomeLv = 0;   // 초당 흡수량
        public int blackholeStorageLv = 0;  // 최대 적재량

        public long BlackHoleStorageMax = 100;
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
    }

    public float GetKm() => Data.player.km;
    public void AddKm(float amount)
    {
        Data.player.km += amount;
        Save();
    }

    public float GetSpeed() => Data.player.speed;
    public void AddSpeed(float amount)
    {
        Data.player.speed = amount;
        Save();
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
    }

    public int GetIncomeLv() => Data.blackHole.blackholeIncomeLv;
    public void AddIncomeLv(int delta = 1) { Data.blackHole.blackholeIncomeLv += delta; Save(); }

    public int GetStorageLv() => Data.blackHole.blackholeStorageLv;
    public void AddStorageLv(int delta = 1) { Data.blackHole.blackholeStorageLv += delta; Save(); }
}
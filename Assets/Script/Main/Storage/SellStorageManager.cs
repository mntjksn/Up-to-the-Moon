using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SellStorageManager : MonoBehaviour
{
    public static SellStorageManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private GameObject mainPanel;

    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI currentAmountText;
    [SerializeField] private TextMeshProUGUI percentText;

    [Header("Runtime Cache")]
    public readonly List<SupplySellSlot> slots = new List<SupplySellSlot>();

    private Coroutine buildRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // 저장 데이터 변경 이벤트 구독 (총합/퍼센트 실시간)
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnResourceChanged += HandleResourceChanged;

        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        // 구독 해제
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnResourceChanged -= HandleResourceChanged;

        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    private void HandleResourceChanged()
    {
        // Top UI만 갱신 (슬롯들은 각자 이벤트로 Refresh 하게 해둔 상태면 이게 제일 가벼움)
        RefreshTopUI();

        // 슬롯까지 여기서 강제 갱신하고 싶으면 아래 줄도 켜도 됨(무거울 수 있음)
        // RefreshAllSlots();
    }

    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[SellStorageManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        while (ItemManager.Instance == null)
            yield return null;

        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        if (ItemManager.Instance.SupplyItem == null || ItemManager.Instance.SupplyItem.Count <= 0)
        {
            Debug.LogError("[SellStorageManager] SupplyItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(ItemManager.Instance.SupplyItem.Count);
        RefreshAllSlots();

        buildRoutine = null;
    }

    private void BuildSlots(int count)
    {
        slots.Clear();

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            if (!obj.TryGetComponent(out SupplySellSlot slot))
            {
                Debug.LogError("[SellStorageManager] slotPrefab에 SupplySellSlot 컴포넌트가 없습니다!");
                Destroy(obj);
                continue;
            }

            slot.Setup(i);
            slots.Add(slot);
        }
    }

    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }

        RefreshTopUI();
    }

    private void RefreshTopUI()
    {
        if (SaveManager.Instance == null) return;

        var data = SaveManager.Instance.Data;
        if (data == null || data.resources == null) return;

        long total = 0;
        for (int i = 0; i < data.resources.Length; i++)
            total += data.resources[i];

        // 최대 저장 가능 개수는 SaveManager의 storageMax 사용
        long max = data.blackHole.BlackHoleStorageMax;

        int percent = 0;
        if (max > 0)
            percent = Mathf.Clamp(Mathf.RoundToInt((float)total / max * 100f), 0, 100);

        if (currentAmountText != null)
            currentAmountText.text = $"{FormatKoreanNumber(total)}개";

        if (percentText != null)
            percentText.text = $"({percent}%)";
    }

    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";

        // 음수도 안전하게
        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;                 // 10^4
        const ulong EOK = 100_000_000UL;            // 10^8
        const ulong JO = 1_000_000_000_000UL;      // 10^12
        const ulong GYEONG = 10_000_000_000_000_000UL; // 10^16

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(jo).Append("조");
        }
        if (eok > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(eok).Append("억");
        }
        if (man > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(man).Append("만");
        }
        if (rest > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(rest);
        }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }
}
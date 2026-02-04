using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BookSkyManager : MonoBehaviour
{
    public static BookSkyManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform content;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subText;

    [Header("Runtime Cache")]
    public readonly List<BookSkySlot> slots = new List<BookSkySlot>();

    [Header("Perf (Mobile)")]
    [SerializeField] private int buildPerFrame = 6;     // 프레임당 생성/초기화 개수
    [SerializeField] private int refreshPerFrame = 12;  // 프레임당 Refresh 개수

    private Coroutine buildRoutine;

    // 슬롯 풀(재사용)
    private readonly List<BookSkySlot> pool = new List<BookSkySlot>(64);

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
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());

        if (titleText != null) titleText.text = "지역 사전";
        if (subText != null) subText.text = "고도에 따라 변화하는 세계의 모습";
    }

    private void OnDisable()
    {
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[BookSkyManager] slotPrefab 또는 content가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        while (BackgroundManager.Instance == null)
            yield return null;

        BackgroundManager bg = BackgroundManager.Instance;

        while (!bg.IsLoaded)
            yield return null;

        if (bg.BackgroundItem == null || bg.BackgroundItem.Count == 0)
        {
            Debug.LogError("[BookSkyManager] BackgroundItem 데이터가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        // 프레임 분산 빌드 + 리프레시
        yield return BuildSlotsAsync(bg.BackgroundItem.Count);
        yield return RefreshAllSlotsAsync();

        buildRoutine = null;
    }

    // 기존 Destroy/Instantiate 대신: 재사용 + 부족하면 추가 생성(프레임 분산)
    private IEnumerator BuildSlotsAsync(int count)
    {
        // 1) 현재 slots를 pool로 돌려놓고 비활성화
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            s.gameObject.SetActive(false);
            pool.Add(s);
        }
        slots.Clear();

        // 2) count만큼 확보
        int builtThisFrame = 0;

        for (int i = 0; i < count; i++)
        {
            BookSkySlot slot = GetOrCreateSlot();
            if (slot == null) continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetParent(content, false);

            slot.Setup(i);
            slots.Add(slot);

            builtThisFrame++;
            if (buildPerFrame > 0 && builtThisFrame >= buildPerFrame)
            {
                builtThisFrame = 0;
                yield return null;
            }
        }

        // 3) content 아래에 남아있는(풀에 남은) 슬롯들은 비활성 상태로 content에 붙여둬도 되고,
        //    정리하고 싶으면 poolRoot 같은 곳으로 옮겨도 됨(구조 최소라 여기선 그대로 둠)
    }

    private BookSkySlot GetOrCreateSlot()
    {
        // pool에서 하나 꺼내기
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            var s = pool[i];
            pool.RemoveAt(i);
            if (s != null) return s;
        }

        // 없으면 새로 생성
        GameObject obj = Instantiate(slotPrefab, content);
        if (!obj.TryGetComponent(out BookSkySlot slot))
        {
            Debug.LogError("[BookSkyManager] slotPrefab에 BookSkySlot이 없습니다.");
            Destroy(obj);
            return null;
        }

        return slot;
    }

    // 기존 동기 RefreshAllSlots 유지
    public void RefreshAllSlots()
    {
        // 슬롯 수가 많으면 아래 Async를 쓰는 게 더 부드러움
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }
    }

    // 프레임 분산 Refresh
    private IEnumerator RefreshAllSlotsAsync()
    {
        int doneThisFrame = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s != null) s.Refresh();

            doneThisFrame++;
            if (refreshPerFrame > 0 && doneThisFrame >= refreshPerFrame)
            {
                doneThisFrame = 0;
                yield return null;
            }
        }
    }
}
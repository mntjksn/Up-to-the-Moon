using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*
    BookSkyManager

    [역할]
    - "지역 사전" UI(도감/사전 형태)에서 배경(지역) 목록 슬롯을 생성하고 갱신한다.
    - BackgroundManager의 BackgroundItem 데이터를 기반으로 슬롯을 구성한다.

    [설계 의도]
    1) 데이터 준비 대기
       - BackgroundManager 인스턴스 생성 및 데이터 로딩 완료(IsLoaded)까지 코루틴으로 대기한다.
       - 데이터가 준비되지 않은 상태에서 UI를 생성하려고 하다 발생하는 예외를 방지한다.
    2) 프레임 분산 처리
       - 슬롯 생성/초기화(buildPerFrame)와 Refresh(refreshPerFrame)를 여러 프레임에 나누어 수행한다.
       - 슬롯 수가 많을 때 UI 프리즈(멈춤 현상)를 완화한다.
    3) 슬롯 재사용(간단 풀링)
       - 기존 Destroy/Instantiate 반복 대신, 비활성화한 슬롯을 pool에 보관하고 재사용한다.
       - 런타임 GC와 Instantiate 비용을 줄인다.
    4) 코루틴 생명주기 관리
       - OnEnable에서 빌드 루틴을 시작하고, OnDisable에서 중단하여 중복 실행을 방지한다.
*/
public class BookSkyManager : MonoBehaviour
{
    public static BookSkyManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;     // 슬롯 프리팹(BookSkySlot 포함)
    [SerializeField] private Transform content;         // 슬롯이 붙을 부모(Content)

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText; // 상단 타이틀
    [SerializeField] private TextMeshProUGUI subText;   // 상단 서브 설명

    [Header("Runtime Cache")]
    // 현재 활성 슬롯 목록(현재 화면에서 사용하는 슬롯)
    public readonly List<BookSkySlot> slots = new List<BookSkySlot>();

    [Header("Perf (Mobile)")]
    [SerializeField] private int buildPerFrame = 6;     // 프레임당 슬롯 생성/초기화 개수 제한
    [SerializeField] private int refreshPerFrame = 12;  // 프레임당 Refresh 호출 개수 제한

    private Coroutine buildRoutine;

    // 비활성 슬롯 보관용 풀(재사용)
    private readonly List<BookSkySlot> pool = new List<BookSkySlot>(64);

    private void Awake()
    {
        // 싱글톤 중복 방지(필요 시 DontDestroyOnLoad를 추가해도 됨)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        // 패널이 열릴 때 빌드를 시작한다(중복 실행 방지)
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());

        // 텍스트는 패널이 켜질 때마다 설정하여 상태를 명확히 한다.
        if (titleText != null) titleText.text = "지역 사전";
        if (subText != null) subText.text = "고도에 따라 변화하는 세계의 모습";
    }

    private void OnDisable()
    {
        // 패널이 닫히면 진행 중인 코루틴을 중단한다.
        // (비활성 상태에서 계속 생성/갱신하면 불필요한 연산이 된다)
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    /*
        데이터 준비가 끝난 뒤 슬롯을 구성한다.

        - BackgroundManager.Instance 생성 대기
        - BackgroundItem 로딩 완료(IsLoaded) 대기
        - 데이터 유효성 검사 후 슬롯 생성/갱신 수행
    */
    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[BookSkyManager] slotPrefab 또는 content가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        // BackgroundManager 생성 대기
        while (BackgroundManager.Instance == null)
            yield return null;

        BackgroundManager bg = BackgroundManager.Instance;

        // 데이터 로딩 완료 대기
        while (!bg.IsLoaded)
            yield return null;

        // 데이터 검증
        if (bg.BackgroundItem == null || bg.BackgroundItem.Count == 0)
        {
            Debug.LogError("[BookSkyManager] BackgroundItem 데이터가 비어있습니다.");
            buildRoutine = null;
            yield break;
        }

        // 프레임 분산 빌드 + 프레임 분산 리프레시
        yield return BuildSlotsAsync(bg.BackgroundItem.Count);
        yield return RefreshAllSlotsAsync();

        buildRoutine = null;
    }

    /*
        슬롯 빌드(프레임 분산 + 재사용)

        1) 기존 활성 슬롯을 비활성화하여 pool로 되돌린다.
        2) 필요한 개수만큼 pool에서 꺼내거나 부족하면 새로 생성한다.
        3) Setup(i)로 슬롯 내용을 초기화한다.
    */
    private IEnumerator BuildSlotsAsync(int count)
    {
        // 1) 현재 활성 슬롯을 풀로 반환
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            s.gameObject.SetActive(false);
            pool.Add(s);
        }
        slots.Clear();

        // 2) 필요한 개수만큼 확보
        int builtThisFrame = 0;

        for (int i = 0; i < count; i++)
        {
            BookSkySlot slot = GetOrCreateSlot();
            if (slot == null) continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetParent(content, false);

            // 인덱스를 기반으로 데이터/표시를 연결한다(슬롯 내부에서 BackgroundManager를 참조할 수 있음)
            slot.Setup(i);
            slots.Add(slot);

            // 프레임 분산: 한 프레임에 너무 많이 만들지 않는다.
            builtThisFrame++;
            if (buildPerFrame > 0 && builtThisFrame >= buildPerFrame)
            {
                builtThisFrame = 0;
                yield return null;
            }
        }

        // 남은 풀 오브젝트를 별도 poolRoot로 옮길 수도 있지만,
        // 구조를 단순하게 유지하기 위해 content 아래 비활성 상태로 두는 방식도 가능하다.
    }

    /*
        풀에서 슬롯을 가져오거나, 없으면 새로 생성한다.

        - pool 리스트의 null을 건너뛰어 안전하게 재사용한다.
        - slotPrefab에 BookSkySlot이 없으면 에러 처리 후 생성 실패로 반환한다.
    */
    private BookSkySlot GetOrCreateSlot()
    {
        // pool에서 하나 꺼내기(null 제거 포함)
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

    /*
        프레임 분산 갱신

        - refreshPerFrame 기준으로 여러 프레임에 나누어 Refresh를 수행한다.
        - 대량 슬롯 갱신 시 UI 프리즈를 완화한다.
    */
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
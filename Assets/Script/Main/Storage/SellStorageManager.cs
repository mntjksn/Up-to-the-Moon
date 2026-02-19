using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*
    SellStorageManager

    [역할]
    - 판매창/보관함 UI를 구성한다.
      1) ItemManager.SupplyItem 개수에 맞춰 슬롯(SupplySellSlot) 리스트를 생성(Build)
      2) 슬롯 RefreshAllSlots로 각 슬롯 UI를 갱신
      3) 상단(Top UI)의 "현재 사용량/퍼센트" 텍스트를 표시/갱신한다.

    [설계 의도]
    1) 로드 타이밍 안전
       - ItemManager가 생성되고 IsLoaded=true가 될 때까지 코루틴(BuildWhenReady)에서 대기한다.
       - SaveManager도 BindSaveManagerRoutine에서 Instance 생성까지 대기 후 이벤트를 연결한다.

    2) 모바일 성능 대응(Top UI 디바운스)
       - 자원 변화 이벤트가 연속으로 올 수 있으므로,
         HandleResourceChanged에서 즉시 TMP 갱신 대신 RequestTopUiRefresh로 "예약"한다.
       - Update에서 일정 시간(topUiDebounceSec) 이후에만 RefreshTopUI_Immediate를 실행하여
         텍스트 재할당/레이아웃 리빌드를 줄인다.

    3) TMP 재할당 최소화
       - lastTotal / lastPercent 캐시로 같은 값이면 text 재할당을 스킵한다.
       - (TMP 텍스트 변경은 레이아웃/메시 리빌드 비용이 있어, 모바일에서 효과가 큼)

    [주의/전제]
    - slotPrefab에는 SupplySellSlot 컴포넌트가 있어야 한다.
    - content, currentAmountText, percentText는 인스펙터에서 연결되어 있어야 한다.
    - SaveManager.GetStorageUsed/GetStorageMax가 올바른 값을 반환해야 한다.
*/
public class SellStorageManager : MonoBehaviour
{
    public static SellStorageManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab; // 슬롯 프리팹(SupplySellSlot 포함)
    [SerializeField] private Transform content;     // 슬롯들이 붙을 부모(Content)

    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI currentAmountText; // 현재 보관량(사용량) 텍스트
    [SerializeField] private TextMeshProUGUI percentText;       // 퍼센트 표시 텍스트

    [Header("Perf")]
    [SerializeField] private float topUiDebounceSec = 0.15f; // 모바일용 디바운스(연속 갱신 합치기)

    public readonly List<SupplySellSlot> slots = new List<SupplySellSlot>(); // 생성된 슬롯 캐시

    private Coroutine buildCo; // ItemManager 로드 대기 + 슬롯 빌드 코루틴
    private Coroutine bindCo;  // SaveManager 생성 대기 + 이벤트 바인딩 코루틴

    // Top UI 업데이트 합치기(디바운스용 플래그/시각)
    private bool topUiDirty;
    private float nextTopUiTime;

    // 같은 값이면 TMP 재할당 방지(리빌드 비용 절감)
    private long lastTotal = -1;
    private int lastPercent = -1;

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        /*
            활성화 시 처리
            - SaveManager 이벤트 바인딩(생성 타이밍이 늦을 수 있으므로 코루틴으로 대기)
            - ItemManager 로드 완료 후 슬롯 빌드 코루틴 시작
        */
        if (bindCo != null) StopCoroutine(bindCo);
        bindCo = StartCoroutine(BindSaveManagerRoutine());

        if (buildCo == null)
            buildCo = StartCoroutine(BuildWhenReady());


        // 캐시 리셋 (패널 다시 켤 때도 무조건 텍스트 다시 세팅)
        lastTotal = -1;
        lastPercent = -1;
    }

    private void OnDisable()
    {
        // 이벤트/코루틴 정리(중복 바인딩/메모리 누수 방지)
        UnbindSaveManager();

        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        if (buildCo != null)
        {
            StopCoroutine(buildCo);
            buildCo = null;
        }

        // 예약된 Top UI 갱신 취소
        topUiDirty = false;
    }

    private void Update()
    {
        /*
            Top UI 디바운스 처리
            - topUiDirty가 true면 "다음 갱신 시각(nextTopUiTime)"이 지난 뒤 1회만 갱신
            - Time.unscaledTime 사용: 일시정지(Time.timeScale=0)에서도 UI는 정상 갱신 가능
        */
        if (topUiDirty && Time.unscaledTime >= nextTopUiTime)
        {
            topUiDirty = false;
            RefreshTopUI_Immediate();
        }
    }

    /*
        SaveManager 바인딩(로드/생성 타이밍 안전)
        - SaveManager.Instance 생성까지 대기
        - OnResourceChanged 이벤트를 연결
        - 최초 Top UI는 즉시 갱신 대신 예약(RequestTopUiRefresh)
    */
    private IEnumerator BindSaveManagerRoutine()
    {
        while (SaveManager.Instance == null)
            yield return null;

        var sm = SaveManager.Instance;

        // Data 로드 완료 대기 (조건은 네 SaveManager 구조에 맞춰 조정)
        while (sm.Data == null || sm.Data.resources == null || sm.Data.blackHole == null)
            yield return null;

        // 중복 방지 위해 -= 후 +=
        sm.OnResourceChanged -= HandleResourceChanged;
        sm.OnResourceChanged += HandleResourceChanged;

        // 여기서는 예약 말고 강제 1회 갱신 추천
        lastTotal = -1;
        lastPercent = -1;
        RefreshTopUI_Immediate();

        bindCo = null;
    }

    /*
        SaveManager 이벤트 해제
        - OnDisable에서 호출
    */
    private void UnbindSaveManager()
    {
        var sm = SaveManager.Instance;
        if (sm != null)
            sm.OnResourceChanged -= HandleResourceChanged;
    }

    /*
        자원 변화 이벤트 핸들러
        - 여기서 즉시 텍스트 갱신을 하지 않고 예약만 한다(디바운스)
    */
    private void HandleResourceChanged()
    {
        // 여기서 즉시 RefreshTopUI() 하지 말기
        RefreshTopUI_Immediate();
    }

    /*
        Top UI 갱신 예약
        - 일정 시간 후(Update에서) RefreshTopUI_Immediate가 실행되도록 플래그/시각 설정
    */
    private void RequestTopUiRefresh()
    {
        topUiDirty = true;
        nextTopUiTime = Time.unscaledTime + topUiDebounceSec;
    }

    /*
        ItemManager 로드 완료 대기 후 슬롯 빌드
        - SupplyItem 개수만큼 슬롯 생성
        - 슬롯 Refresh 후 Top UI 갱신 예약
    */
    private IEnumerator BuildWhenReady()
    {
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[SellStorageManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        // ItemManager 생성 대기
        while (ItemManager.Instance == null)
            yield return null;

        // 데이터 로드 완료 대기
        while (!ItemManager.Instance.IsLoaded)
            yield return null;

        var items = ItemManager.Instance.SupplyItem;
        if (items == null || items.Count <= 0)
        {
            Debug.LogError("[SellStorageManager] SupplyItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(items.Count);
        RefreshAllSlots();

        buildCo = null;
    }

    /*
        슬롯 생성
        - 기존 content 자식 제거 후 count 만큼 slotPrefab Instantiate
        - SupplySellSlot 컴포넌트 확인 후 Setup(i)
        - slots 리스트에 캐싱
    */
    private void BuildSlots(int count)
    {
        slots.Clear();

        // 기존 UI 제거(필요 시 풀링으로 전환 가능)
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // 새 슬롯 생성
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            if (!obj.TryGetComponent(out SupplySellSlot slot))
            {
                Debug.LogError("[SellStorageManager] slotPrefab에 SupplySellSlot 컴포넌트가 없습니다.");
                Destroy(obj);
                continue;
            }

            slot.Setup(i);
            slots.Add(slot);
        }
    }

    /*
        모든 슬롯 갱신
        - 각 SupplySellSlot.Refresh() 호출
        - 슬롯 갱신이 끝난 뒤 Top UI는 즉시 갱신하지 않고 예약(RequestTopUiRefresh)
    */
    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }

        // 슬롯 리프레시 후 Top UI는 예약(디바운스 적용)
        RequestTopUiRefresh();
    }

    /*
        Top UI 실제 갱신(텍스트 변경은 여기서만)
        - SaveManager에서 보관 사용량/최대치 계산
        - 퍼센트 계산 후 텍스트 출력
        - lastTotal/lastPercent 캐시로 동일 값이면 TMP 재할당 스킵
    */
    private void RefreshTopUI_Immediate()
    {
        var sm = SaveManager.Instance;
        if (sm == null) return;

        // 데이터 유효성 체크(방어 코드)
        var data = sm.Data;
        if (data == null || data.resources == null || data.blackHole == null) return;

        long total = sm.GetStorageUsed(); // 현재 사용량
        long max = sm.GetStorageMax();    // 최대 용량

        // 퍼센트 계산(0~100)
        int percent = 0;
        if (max > 0)
            percent = Mathf.Clamp(Mathf.RoundToInt((float)total / (float)max * 100f), 0, 100);

        // 같은 값이면 TMP 갱신 스킵(리빌드 방지)
        if (total != lastTotal)
        {
            lastTotal = total;
            if (currentAmountText != null)
                currentAmountText.text = NumberFormatter.FormatKorean(total) + "개";
        }

        if (percent != lastPercent)
        {
            lastPercent = percent;
            if (percentText != null)
                percentText.text = "(" + percent + "%)";
        }
    }
}
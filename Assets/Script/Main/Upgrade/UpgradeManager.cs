using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    UpgradeManager

    [역할]
    - 캐릭터 수(CharacterManager.CharacterItem.Count)에 맞춰 업그레이드 슬롯 UI를 생성(Build)한다.
    - 이미 만들어진 상태에서 "캐릭터 수가 동일"하면 재빌드하지 않고 Refresh만 수행한다.
    - CharacterManager 로드 완료(IsLoaded) 이후에만 안전하게 빌드를 시작한다.

    [설계 의도]
    1) 빌드 최소화
       - built / builtCount / slots.Count / content.childCount를 이용해
         "동일 개수"라면 BuildSlots를 생략하고 RefreshAllSlots만 호출한다.

    2) 로드 타이밍 안전
       - CharacterManager.Instance가 null이거나 아직 IsLoaded=false인 상태를 고려하여
         코루틴(BuildWhenReady)에서 로드 완료까지 대기한다.

    3) 런타임 캐시 유지
       - 생성된 UpgradeSlot들을 slots 리스트에 캐싱하여
         이후 갱신 시 GetComponentsInChildren 같은 탐색 비용을 피한다.

    [주의/전제]
    - slotPrefab에는 반드시 UpgradeSlot 컴포넌트가 있어야 한다.
    - content는 슬롯들이 붙을 부모 Transform이어야 한다.
    - 이 스크립트는 OnEnable 시점에 필요하면 자동으로 빌드를 수행한다.
*/
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab; // 슬롯 프리팹(UpgradeSlot 포함)
    [SerializeField] private Transform content;     // 슬롯들이 붙을 부모(Content)

    [Header("Runtime Cache")]
    public readonly List<UpgradeSlot> slots = new List<UpgradeSlot>(); // 생성된 슬롯 캐시

    private Coroutine buildRoutine; // 빌드/대기 코루틴(중복 실행 방지)

    // 캐시(재빌드 판단용)
    private bool built = false;     // 최소 1회 빌드 완료 여부
    private int builtCount = -1;    // 마지막으로 빌드한 캐릭터 수

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        /*
            활성화될 때 처리 흐름
            1) 이미 빌드된 상태라면 현재 캐릭터 수를 확인
               - 캐릭터 수가 동일하면 재빌드 없이 Refresh만
            2) 빌드가 필요하면 BuildWhenReady 코루틴 시작
        */

        // 이미 만들어졌고, 개수도 같으면 재빌드하지 말고 Refresh만
        if (built && buildRoutine == null)
        {
            int currentCount = GetCurrentCharacterCountSafe();
            if (currentCount > 0 && currentCount == builtCount)
            {
                RefreshAllSlots();
                return;
            }
        }

        // 아직 빌드가 안 됐거나(또는 개수가 바뀌었거나) 코루틴이 없으면 빌드 대기 시작
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        // 비활성화 시 코루틴 정리(메모리/중복 실행 방지)
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    /*
        현재 캐릭터 수를 "안전하게" 가져오기
        - CharacterManager가 아직 없거나, 로드가 안 됐거나, 리스트가 null이면 -1 반환
        - OnEnable에서 재빌드 여부 판단에 사용
    */
    private int GetCurrentCharacterCountSafe()
    {
        var cm = CharacterManager.Instance;
        if (cm == null || !cm.IsLoaded) return -1;
        if (cm.CharacterItem == null) return -1;
        return cm.CharacterItem.Count;
    }

    /*
        CharacterManager 로드 완료 대기 후 빌드
        - slotPrefab/content 유효성 검사
        - CharacterManager.Instance 생성 대기
        - IsLoaded=true 될 때까지 대기
        - 데이터 검증 후 필요하면 BuildSlots + RefreshAllSlots 수행
        - 동일 개수라면 Build 생략하고 Refresh만 수행
    */
    private IEnumerator BuildWhenReady()
    {
        // 필수 참조 체크
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[UpgradeManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        // CharacterManager 생성 대기
        while (CharacterManager.Instance == null)
            yield return null;

        // 데이터 로드 완료 대기
        while (!CharacterManager.Instance.IsLoaded)
            yield return null;

        var list = CharacterManager.Instance.CharacterItem;
        if (list == null || list.Count <= 0)
        {
            Debug.LogError("[UpgradeManager] CharacterItem 데이터가 비어있습니다.");
            yield break;
        }

        int count = list.Count;

        /*
            재빌드 스킵 조건
            - built == true
            - builtCount == count
            - slots.Count == count
            - content.childCount == count
            위 조건이 모두 맞으면 "현재 상태가 이미 원하는 형태"라고 보고 Refresh만 수행
        */
        if (built && builtCount == count && slots.Count == count && content.childCount == count)
        {
            RefreshAllSlots();
            buildRoutine = null;
            yield break;
        }

        // 슬롯 구조 재생성(필요 시)
        BuildSlots(count);

        // 생성 직후 최신 상태 반영
        RefreshAllSlots();

        // 캐시 업데이트
        built = true;
        builtCount = count;

        // 코루틴 핸들 해제
        buildRoutine = null;
    }

    /*
        슬롯 UI 생성
        - 기존 content 자식 제거 후
        - count 만큼 slotPrefab Instantiate
        - UpgradeSlot 컴포넌트 확인 후 Setup(i) 호출
        - slots 리스트에 캐싱
    */
    private void BuildSlots(int count)
    {
        // 캐시 초기화
        slots.Clear();

        // 기존 UI 정리(풀링으로 바꿀 수도 있음)
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // 슬롯 생성
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            // 프리팹에 UpgradeSlot이 없으면 오류 처리
            if (!obj.TryGetComponent(out UpgradeSlot slot))
            {
                Debug.LogError("[UpgradeManager] slotPrefab에 UpgradeSlot 컴포넌트가 없습니다!");
                Destroy(obj);
                continue;
            }

            // 슬롯 인덱스 기반 초기화
            slot.Setup(i);

            // 런타임 캐시에 저장(갱신 시 탐색 비용 절감)
            slots.Add(slot);
        }
    }

    /*
        모든 슬롯 UI 갱신
        - 각 슬롯의 Refresh()를 호출하여 현재 업그레이드 상태/표시를 최신화
        - 슬롯이 null일 수 있으므로 null 체크
    */
    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }
    }
}
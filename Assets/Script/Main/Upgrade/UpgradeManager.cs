using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 캐릭터 업그레이드 슬롯 생성/관리 매니저
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;   // UpgradeSlot 붙은 프리팹
    [SerializeField] private Transform content;       // ScrollView Content

    [Header("Runtime Cache")]
    public readonly List<UpgradeSlot> slots = new List<UpgradeSlot>();

    private Coroutine buildRoutine;

    private void Awake()
    {
        // 싱글톤 유지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        // 패널 열릴 때 안전하게 빌드
        if (buildRoutine == null)
            buildRoutine = StartCoroutine(BuildWhenReady());
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
        // 필수 참조 체크
        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[UpgradeManager] slotPrefab 또는 content가 비어있습니다.");
            yield break;
        }

        // CharacterManager 준비 대기
        while (CharacterManager.Instance == null)
            yield return null;

        // 데이터 로드 완료 대기
        while (!CharacterManager.Instance.IsLoaded)
            yield return null;

        // 데이터 유효성 체크
        if (CharacterManager.Instance.CharacterItem == null ||
            CharacterManager.Instance.CharacterItem.Count <= 0)
        {
            Debug.LogError("[UpgradeManager] CharacterItem 데이터가 비어있습니다.");
            yield break;
        }

        BuildSlots(CharacterManager.Instance.CharacterItem.Count);
        RefreshAllSlots();

        buildRoutine = null;
    }

    private void BuildSlots(int count)
    {
        // 기존 슬롯 정리
        slots.Clear();

        // Content 자식 삭제
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // 슬롯 생성
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(slotPrefab, content);

            if (!obj.TryGetComponent(out UpgradeSlot slot))
            {
                Debug.LogError("[UpgradeManager] slotPrefab에 UpgradeSlot 컴포넌트가 없습니다!");
                Destroy(obj);
                continue;
            }

            slot.Setup(i);
            slots.Add(slot);
        }
    }

    // 전체 슬롯 UI 갱신
    public void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Refresh();
        }
    }
}
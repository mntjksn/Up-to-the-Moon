using System.Collections.Generic;
using UnityEngine;

/*
    ResourceIncomeSystem

    [역할]
    - 블랙홀 수급량(income)에 따라 자원을 주기적으로 생성(획득)한다.
    - 현재 km 구간에서 해금된 자원 목록 중 1개를 가중치 랜덤으로 선택해 보유량을 증가시킨다.
    - 드랍 시 선택적으로 픽업 VFX를 생성하되, 모바일 성능을 위해 생성 개수/빈도를 제한한다.

    [설계 의도]
    1) 누적(accumulator) 방식
       - income(초당 수급량)을 Time.deltaTime과 곱해 acc에 누적한다.
       - acc가 1 이상이 될 때마다 1개 드랍을 처리하여 "초당 n개"를 자연스럽게 구현한다.
    2) 프레임 폭증 방지
       - 한 프레임에서 처리 가능한 최대 드랍 수(maxDropsPerFrame)를 제한한다.
       - 저장소가 가득 찼으면 즉시 중단한다.
    3) GC 최소화
       - SaveManager/ItemManager 인스턴스를 캐시한다.
       - 해금 리스트는 ItemManager의 캐시 리스트를 재사용한다(매번 new List 방지).
       - 가중치 배열(weightBuffer)을 재사용하고 필요 시에만 확장한다.
    4) VFX 제어 및 풀링 지원
       - 프레임당 최대 VFX 생성 수(maxVfxPerFrame)를 제한한다.
       - n개당 1번 VFX(vfxEveryNDrops)로 빈도를 조절한다.
       - 풀(ResourcePickupVFXPool)이 있으면 재사용하고, 없으면 Instantiate로 동작한다.
*/
public class ResourceIncomeSystem : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private ResourcePickupVFX pickupVfxPrefab; // 픽업 연출 프리팹
    [SerializeField] private Transform blackHole;               // VFX가 빨려 들어갈 목표(블랙홀)
    [SerializeField] private float spawnMinRadius = 0.6f;       // 스폰 최소 반경
    [SerializeField] private float spawnMaxRadius = 1.4f;       // 스폰 최대 반경
    [SerializeField] private float spawnZ = 0f;                 // 레이어/정렬을 위한 Z 고정값

    [Header("Drop Weight")]
    [SerializeField] private float weightPower = 1.5f;          // 가중치 감소 곡선(클수록 낮은 id가 더 자주 나온다)

    [Header("Perf (Mobile)")]
    [SerializeField] private bool spawnVfx = true;              // VFX 생성 여부(저사양 옵션)
    [SerializeField] private int maxVfxPerFrame = 8;            // 프레임당 VFX 최대 생성 수
    [SerializeField] private int vfxEveryNDrops = 1;            // n개당 1번 VFX(1이면 매번)
    [SerializeField] private int maxDropsPerFrame = 32;         // 한 프레임 최대 드랍 수(폭증 방지)

    // 수급 누적값(정수 1개 단위로 드랍 처리)
    private float acc = 0f;

    // 프레임당 VFX 생성 수 제한용 카운터
    private int vfxSpawnedThisFrame = 0;

    // 드랍 카운터(몇 개당 1번 VFX 같은 빈도 제어에 사용)
    private int dropCounter = 0;

    // 매 프레임 Find/Instance 접근을 줄이기 위한 캐시
    private SaveManager saveCached;
    private ItemManager itemCached;

    // 가중치 계산용 버퍼(매 호출 new float[] 방지)
    private float[] weightBuffer = new float[0];

    private void LateUpdate()
    {
        // Update에서 VFX를 여러 번 생성할 수 있으므로,
        // 프레임 끝에서 다음 프레임을 위한 카운터를 리셋한다.
        vfxSpawnedThisFrame = 0;
    }

    private void Update()
    {
        // 싱글톤 참조를 매 프레임 재탐색하지 않도록 캐시한다.
        if (saveCached == null) saveCached = SaveManager.Instance;
        if (itemCached == null) itemCached = ItemManager.Instance;

        var save = saveCached;
        var item = itemCached;

        // 필수 시스템 준비 전에는 동작하지 않는다.
        if (save == null || item == null) return;
        if (!item.IsLoaded) return;

        // 저장소가 가득 찼다면 더 이상 드랍하지 않는다.
        if (save.IsStorageFull()) return;

        float income = save.GetIncome();
        if (income <= 0f) return;

        // 초당 수급량을 누적한다.
        acc += Time.deltaTime * income;

        // 한 프레임에 너무 많이 처리하지 않도록 제한한다.
        int drops = 0;

        // 같은 프레임에서 km가 크게 변하지 않는 구조라면 한 번만 읽어도 충분하다.
        float km = save.GetKm();

        // acc가 1 이상인 동안 1개씩 지급한다.
        while (acc >= 1f && drops < maxDropsPerFrame)
        {
            // 현재 km에서 드랍 가능한 아이템이 없거나 지급 실패하면 종료한다.
            if (!GiveOneDrop(save, item, km))
                break;

            acc -= 1f;
            drops++;

            // 드랍 후 저장소가 가득 찼으면 즉시 중단한다.
            if (save.IsStorageFull())
                break;
        }
    }

    /*
        드랍 1회 처리

        - km 기준 해금된 자원 리스트를 가져온다.
        - 가중치 랜덤으로 1개를 선택한다.
        - 선택된 자원을 SaveManager에 1개 추가한다.
        - VFX 옵션이 켜져있고 조건을 만족하면 픽업 연출을 생성한다.
    */
    private bool GiveOneDrop(SaveManager save, ItemManager item, float km)
    {
        // 해금 리스트는 캐시 리스트를 재사용하여 GC를 줄인다.
        List<SupplyItem> unlocked = item.GetUnlockedByKmCached(km);
        if (unlocked == null || unlocked.Count == 0) return false;

        SupplyItem picked = PickWeightedRandom(unlocked);
        if (picked == null) return false;

        // 실제 보유량 증가(저장 로직은 SaveManager 내부 정책을 따른다)
        save.AddResource(picked.item_num, 1);

        dropCounter++;

        if (spawnVfx && ShouldSpawnVfxThisDrop())
            SpawnPickupVFX(picked);

        return true;
    }

    /*
        이번 드랍에서 VFX를 생성할지 결정한다.

        - 프레임당 최대 생성 수를 넘지 않는다.
        - vfxEveryNDrops 설정에 따라 n개당 1회만 생성한다.
    */
    private bool ShouldSpawnVfxThisDrop()
    {
        if (vfxSpawnedThisFrame >= maxVfxPerFrame) return false;

        if (vfxEveryNDrops <= 1) return true;

        return (dropCounter % vfxEveryNDrops) == 0;
    }

    /*
        가중치 랜덤 선택

        - 아이템 id가 낮을수록 더 자주 나오도록 가중치를 설정한다.
        - w = 1 / (id+1)^power
        - 매 호출마다 배열을 생성하지 않도록 weightBuffer를 재사용한다.
    */
    private SupplyItem PickWeightedRandom(List<SupplyItem> list)
    {
        int count = list.Count;
        if (count == 0) return null;
        if (count == 1) return list[0];

        EnsureWeightBuffer(count);

        float total = 0f;

        for (int i = 0; i < count; i++)
        {
            SupplyItem it = list[i];
            if (it == null)
            {
                weightBuffer[i] = 0f;
                continue;
            }

            int id = it.item_num;
            if (id < 0) id = 0;

            float w = 1f / Mathf.Pow(id + 1f, weightPower);
            weightBuffer[i] = w;
            total += w;
        }

        // 모든 가중치가 0이면 마지막 원소로 fallback한다.
        if (total <= 0f)
            return list[count - 1];

        float r = Random.value * total;

        for (int i = 0; i < count; i++)
        {
            r -= weightBuffer[i];
            if (r <= 0f)
                return list[i] != null ? list[i] : list[count - 1];
        }

        return list[count - 1];
    }

    /*
        가중치 버퍼 확보

        - 필요한 길이보다 작으면 NextPowerOfTwo로 확장해 재할당 빈도를 줄인다.
    */
    private void EnsureWeightBuffer(int needed)
    {
        if (weightBuffer == null || weightBuffer.Length < needed)
        {
            int newSize = Mathf.NextPowerOfTwo(needed);
            weightBuffer = new float[newSize];
        }
    }

    /*
        픽업 VFX 생성

        - 블랙홀 주변 원형 영역 내 랜덤 위치에 생성한다.
        - 풀(ResourcePickupVFXPool)이 있으면 재사용하고, 없으면 Instantiate한다.
        - 프레임당 최대 생성 수 제한을 적용한다.
    */
    private void SpawnPickupVFX(SupplyItem item)
    {
        if (pickupVfxPrefab == null || blackHole == null) return;
        if (item == null || item.itemimg == null) return;

        if (vfxSpawnedThisFrame >= maxVfxPerFrame) return;
        vfxSpawnedThisFrame++;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float r = Random.Range(spawnMinRadius, spawnMaxRadius);

        Vector3 spawnPos = blackHole.position + new Vector3(dir.x, dir.y, 0f) * r;
        spawnPos.z = spawnZ;

        // 풀 존재 시 재사용
        var pool = ResourcePickupVFXPool.Instance;
        if (pool == null)
        {
            var vfx = Instantiate(pickupVfxPrefab, spawnPos, Quaternion.identity);
            vfx.Init(item.itemimg, blackHole);
            return;
        }

        var vfxPooled = pool.Get(spawnPos, Quaternion.identity);
        if (vfxPooled == null) return;

        vfxPooled.Init(item.itemimg, blackHole);
    }
}
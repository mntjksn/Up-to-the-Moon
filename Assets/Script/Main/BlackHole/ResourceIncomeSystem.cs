using System.Collections.Generic;
using UnityEngine;

public class ResourceIncomeSystem : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private ResourcePickupVFX pickupVfxPrefab;
    [SerializeField] private Transform blackHole;
    [SerializeField] private float spawnMinRadius = 0.6f;
    [SerializeField] private float spawnMaxRadius = 1.4f;
    [SerializeField] private float spawnZ = 0f;

    [Header("Drop Weight")]
    [SerializeField] private float weightPower = 1.5f;

    [Header("Perf (Mobile)")]
    [SerializeField] private bool spawnVfx = true;
    [SerializeField] private int maxVfxPerFrame = 8;     // 프레임당 VFX 최대 생성
    [SerializeField] private int vfxEveryNDrops = 1;     // n개당 1번 VFX(1=매번)
    [SerializeField] private int maxDropsPerFrame = 32;  // 폭증 방지(프레임당 최대 드랍)

    private float acc = 0f;

    private int vfxSpawnedThisFrame = 0;
    private int dropCounter = 0;

    // 캐시
    private SaveManager saveCached;
    private ItemManager itemCached;

    // 가중치 버퍼(매번 new float[] 방지)
    private float[] weightBuffer = new float[0];

    private void LateUpdate()
    {
        vfxSpawnedThisFrame = 0;
    }

    private void Update()
    {
        if (saveCached == null) saveCached = SaveManager.Instance;
        if (itemCached == null) itemCached = ItemManager.Instance;

        var save = saveCached;
        var item = itemCached;

        if (save == null || item == null) return;
        if (!item.IsLoaded) return;
        if (save.IsStorageFull()) return;

        float income = save.GetIncome();
        if (income <= 0f) return;

        acc += Time.deltaTime * income;

        // 한 프레임 폭증 제한
        int drops = 0;

        // 자주 쓰는 값 캐시
        float km = save.GetKm();

        while (acc >= 1f && drops < maxDropsPerFrame)
        {
            if (!GiveOneDrop(save, item, km))
                break;

            acc -= 1f;
            drops++;

            if (save.IsStorageFull())
                break;

            // km가 프레임 내에서 크게 바뀌지 않으면 재조회 불필요.
            // (원하면 km가 계속 늘어나는 구조면 여기서 km = save.GetKm(); 로 갱신 가능)
        }
    }

    private bool GiveOneDrop(SaveManager save, ItemManager item, float km)
    {
        // new List 제거: 캐시 리스트 사용 (ItemManager에 GetUnlockedByKmCached를 추가해둔 버전 기준)
        List<SupplyItem> unlocked = item.GetUnlockedByKmCached(km);
        if (unlocked == null || unlocked.Count == 0) return false;

        SupplyItem picked = PickWeightedRandom(unlocked);
        if (picked == null) return false;

        save.AddResource(picked.item_num, 1);

        dropCounter++;

        if (spawnVfx && ShouldSpawnVfxThisDrop())
            SpawnPickupVFX(picked);

        return true;
    }

    private bool ShouldSpawnVfxThisDrop()
    {
        if (vfxSpawnedThisFrame >= maxVfxPerFrame) return false;

        if (vfxEveryNDrops <= 1) return true;

        return (dropCounter % vfxEveryNDrops) == 0;
    }

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

            // w = 1 / (id+1)^power
            float w = 1f / Mathf.Pow(id + 1f, weightPower);
            weightBuffer[i] = w;
            total += w;
        }

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

    private void EnsureWeightBuffer(int needed)
    {
        if (weightBuffer == null || weightBuffer.Length < needed)
        {
            // 2배 확장으로 재할당 빈도 최소화
            int newSize = Mathf.NextPowerOfTwo(needed);
            weightBuffer = new float[newSize];
        }
    }

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
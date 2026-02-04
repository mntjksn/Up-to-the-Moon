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

    private float acc = 0f;

    private int vfxSpawnedThisFrame = 0;
    private int dropCounter = 0;

    private void LateUpdate()
    {
        vfxSpawnedThisFrame = 0;
    }

    private void Update()
    {
        SaveManager save = SaveManager.Instance;
        ItemManager item = ItemManager.Instance;

        if (save == null || item == null) return;
        if (!item.IsLoaded) return;
        if (save.IsStorageFull()) return;

        acc += Time.deltaTime * save.GetIncome();

        while (acc >= 1f)
        {
            GiveOneDrop(save, item);
            acc -= 1f;

            if (save.IsStorageFull())
                break;
        }
    }

    private void GiveOneDrop(SaveManager save, ItemManager item)
    {
        float km = save.GetKm();

        List<SupplyItem> unlocked = item.GetUnlockedByKm(km);
        if (unlocked == null || unlocked.Count == 0) return;

        SupplyItem picked = PickWeightedRandom(unlocked);
        if (picked == null) return;

        save.AddResource(picked.item_num, 1);

        dropCounter++;

        if (spawnVfx && ShouldSpawnVfxThisDrop())
            SpawnPickupVFX(picked);

        // 절대 여기서 전체 UI Refresh 하지 말기
        // StorageSlot들이 SaveManager.OnResourceChanged 이벤트로 Refresh 하게 두면 됨
        // StorageManager.Instance?.RefreshAllSlots();
    }

    private bool ShouldSpawnVfxThisDrop()
    {
        if (vfxEveryNDrops <= 1) return vfxSpawnedThisFrame < maxVfxPerFrame;

        if (dropCounter % vfxEveryNDrops != 0)
            return false;

        return vfxSpawnedThisFrame < maxVfxPerFrame;
    }

    private SupplyItem PickWeightedRandom(List<SupplyItem> list)
    {
        int count = list.Count;
        if (count == 0) return null;
        if (count == 1) return list[0];

        float total = 0f;
        float[] weights = new float[count];

        for (int i = 0; i < count; i++)
        {
            SupplyItem it = list[i];
            if (it == null)
            {
                weights[i] = 0f;
                continue;
            }

            int id = it.item_num;
            if (id < 0) id = 0;

            float w = 1f / Mathf.Pow(id + 1f, weightPower);
            weights[i] = w;
            total += w;
        }

        if (total <= 0f)
            return list[count - 1];

        float r = Random.value * total;

        for (int i = 0; i < count; i++)
        {
            r -= weights[i];
            if (r <= 0f)
                return list[i] != null ? list[i] : list[count - 1];
        }

        return list[count - 1];
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

        // 풀에서 꺼내기
        var pool = ResourcePickupVFXPool.Instance;
        if (pool == null)
        {
            // 풀 없으면 기존 방식 fallback
            var vfx = Instantiate(pickupVfxPrefab, spawnPos, Quaternion.identity);
            vfx.Init(item.itemimg, blackHole);
            return;
        }

        var vfxPooled = pool.Get(spawnPos, Quaternion.identity);
        if (vfxPooled == null) return;

        vfxPooled.Init(item.itemimg, blackHole);
    }
}
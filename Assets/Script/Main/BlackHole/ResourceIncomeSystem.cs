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

    private float acc = 0f;

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
        SpawnPickupVFX(picked);

        if (StorageManager.Instance != null)
            StorageManager.Instance.RefreshAllSlots();
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

        Vector2 dir = Random.insideUnitCircle.normalized;
        float r = Random.Range(spawnMinRadius, spawnMaxRadius);

        Vector3 spawnPos = blackHole.position + new Vector3(dir.x, dir.y, 0f) * r;
        spawnPos.z = spawnZ;

        ResourcePickupVFX vfx = Instantiate(pickupVfxPrefab, spawnPos, Quaternion.identity);
        vfx.Init(item.itemimg, blackHole);
    }
}
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
    [SerializeField] private float weightPower = 1.5f; // 1.2~2.0 (커질수록 고티어 더 안나옴)

    private float acc = 0f;

    private void Update()
    {
        if (SaveManager.Instance == null || ItemManager.Instance == null) return;
        if (!ItemManager.Instance.IsLoaded) return;

        if (IsStorageFull()) return;

        acc += Time.deltaTime * SaveManager.Instance.GetIncome();

        while (acc >= 1f)
        {
            GiveOneDrop();
            acc -= 1f;

            if (IsStorageFull()) break;
        }
    }

    private void GiveOneDrop()
    {
        float km = SaveManager.Instance.GetKm();

        // 해금 리스트를 "여기서" 한번 더 강제 필터링
        var all = ItemManager.Instance.SupplyItem;
        if (all == null || all.Count == 0) return;

        List<SupplyItem> unlocked = GetUnlockedLocal(all, km);
        if (unlocked.Count == 0) return;

        // 해금된 것 중에서 가중치 랜덤
        var picked = PickWeightedRandom(unlocked);

        SaveManager.Instance.AddResource(picked.item_num, 1);
        SpawnPickupVFX(picked);

        // UI는 이벤트로 갱신하는 구조면 이거 없어도 됨
        if (StorageManager.Instance != null)
            StorageManager.Instance.RefreshAllSlots();
    }

    // zoneMinKm 기준으로 확실히 필터
    private List<SupplyItem> GetUnlockedLocal(List<SupplyItem> all, float km)
    {
        var result = new List<SupplyItem>(all.Count);

        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (it == null) continue;

            // zoneMinKm <= 현재 km 일 때만 해금
            if (km >= it.zoneMinKm)
                result.Add(it);
        }

        return result;
    }

    // id(=item_num)가 높을수록 덜 나오게
    private SupplyItem PickWeightedRandom(List<SupplyItem> list)
    {
        float total = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            int id = Mathf.Max(0, it.item_num); // 안전
            float w = 1f / Mathf.Pow(id + 1f, weightPower);
            total += w;
        }

        float r = Random.value * total;

        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            int id = Mathf.Max(0, it.item_num);
            float w = 1f / Mathf.Pow(id + 1f, weightPower);
            r -= w;
            if (r <= 0f) return it;
        }

        return list[list.Count - 1];
    }

    private bool IsStorageFull()
    {
        var data = SaveManager.Instance.Data;
        if (data == null || data.resources == null) return false;

        // 너 SaveData에 storageMax를 넣었잖아. 그걸로 맞추자.
        long max = data.blackHole.BlackHoleStorageMax;
        if (max <= 0) return false;

        long total = 0;
        for (int i = 0; i < data.resources.Length; i++)
            total += data.resources[i];

        return total >= max;
    }

    private void SpawnPickupVFX(SupplyItem item)
    {
        if (pickupVfxPrefab == null || blackHole == null) return;
        if (item == null || item.itemimg == null) return;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float r = Random.Range(spawnMinRadius, spawnMaxRadius);

        Vector3 spawnPos = blackHole.position + new Vector3(dir.x, dir.y, 0f) * r;
        spawnPos.z = spawnZ;

        var vfx = Instantiate(pickupVfxPrefab, spawnPos, Quaternion.identity);
        vfx.Init(item.itemimg, blackHole);
    }
}
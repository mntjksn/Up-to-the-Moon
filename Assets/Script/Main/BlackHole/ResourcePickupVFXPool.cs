using System.Collections.Generic;
using UnityEngine;

public class ResourcePickupVFXPool : MonoBehaviour
{
    public static ResourcePickupVFXPool Instance;

    [Header("Prefab")]
    [SerializeField] private ResourcePickupVFX prefab;

    [Header("Pool")]
    [SerializeField] private int prewarmCount = 80;
    [SerializeField] private bool canExpand = true;

    [Header("Roots (Optional)")]
    [SerializeField] private Transform poolRoot;   // 비활성 보관 루트(비워두면 this.transform)
    [SerializeField] private Transform spawnRoot;  // 활성 스폰 루트(비워두면 null=씬 루트)

    private readonly Queue<ResourcePickupVFX> pool = new Queue<ResourcePickupVFX>(256);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // (추천) 씬 넘어가도 풀 유지 + 프리웜 1회만
        DontDestroyOnLoad(gameObject);

        if (prefab == null)
        {
            Debug.LogError("[VFXPool] prefab is null");
            return;
        }

        if (poolRoot == null) poolRoot = transform;

        Prewarm();
    }

    private void Prewarm()
    {
        for (int i = 0; i < prewarmCount; i++)
        {
            var v = CreateNew();
            Release(v);
        }
    }

    private ResourcePickupVFX CreateNew()
    {
        var parent = poolRoot != null ? poolRoot : transform;

        var v = Instantiate(prefab, parent);
        v.gameObject.SetActive(false);
        v.SetPool(this);
        return v;
    }

    public ResourcePickupVFX Get(Vector3 pos, Quaternion rot)
    {
        ResourcePickupVFX v = (pool.Count > 0) ? pool.Dequeue() : null;

        if (v == null)
        {
            if (!canExpand) return null;
            v = CreateNew();
        }

        var tr = v.transform;

        // spawnRoot가 있으면 거기로, 없으면 씬 루트(기존과 동일)
        if (spawnRoot != null) tr.SetParent(spawnRoot, false);
        else tr.SetParent(null, false);

        tr.SetPositionAndRotation(pos, rot);

        v.gameObject.SetActive(true);
        return v;
    }

    public void Release(ResourcePickupVFX v)
    {
        if (v == null) return;

        var go = v.gameObject;

        // 중복 Release 방지(이미 비활성이면 풀에 들어가 있다고 가정)
        if (!go.activeSelf) return;

        go.SetActive(false);

        var tr = v.transform;
        if (poolRoot != null) tr.SetParent(poolRoot, false);
        else tr.SetParent(transform, false);

        pool.Enqueue(v);
    }
}
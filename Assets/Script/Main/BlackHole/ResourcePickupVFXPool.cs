using System.Collections.Generic;
using UnityEngine;

public class ResourcePickupVFXPool : MonoBehaviour
{
    public static ResourcePickupVFXPool Instance;

    [Header("Prefab")]
    [SerializeField] private ResourcePickupVFX prefab;

    [Header("Pool")]
    [SerializeField] private int prewarmCount = 80;  // 최대치(초당 50) 고려해서 넉넉히
    [SerializeField] private bool canExpand = true;

    private readonly Queue<ResourcePickupVFX> pool = new Queue<ResourcePickupVFX>(256);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (prefab == null)
        {
            Debug.LogError("[VFXPool] prefab is null");
            return;
        }

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
        var v = Instantiate(prefab, transform);
        v.gameObject.SetActive(false);
        v.SetPool(this);
        return v;
    }

    public ResourcePickupVFX Get(Vector3 pos, Quaternion rot)
    {
        ResourcePickupVFX v = null;

        while (pool.Count > 0 && v == null)
            v = pool.Dequeue();

        if (v == null)
        {
            if (!canExpand) return null;
            v = CreateNew();
        }

        var go = v.gameObject;
        go.transform.SetParent(null, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);

        return v;
    }

    public void Release(ResourcePickupVFX v)
    {
        if (v == null) return;

        var go = v.gameObject;
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        pool.Enqueue(v);
    }
}
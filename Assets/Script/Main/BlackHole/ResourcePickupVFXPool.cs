using System.Collections.Generic;
using UnityEngine;

/*
    ResourcePickupVFXPool

    [역할]
    - ResourcePickupVFX 오브젝트 풀을 관리한다.
    - Instantiate/Destroy 비용을 줄이기 위해 미리 생성(프리웜)해두고,
      필요할 때 꺼내서 활성화(Get), 사용 후 비활성화하여 반환(Release)한다.

    [설계 의도]
    1) 프리웜(Prewarm)
       - 시작 시 prewarmCount만큼 미리 생성해두어, 게임 플레이 중 스파이크(프레임 드랍)를 줄인다.
    2) 확장 정책(canExpand)
       - 풀 부족 시 추가 생성 허용 여부를 옵션으로 제공하여 메모리/성능 밸런스를 조절한다.
    3) 루트 분리(poolRoot/spawnRoot)
       - 비활성 오브젝트는 poolRoot 아래에 모아 관리한다.
       - 활성 오브젝트는 spawnRoot로 옮기거나, 필요 시 씬 루트에 배치한다.
    4) 중복 반환 방지
       - 이미 비활성 상태면 풀에 들어간 것으로 보고 Release를 무시한다.
    5) 씬 전환 대응
       - DontDestroyOnLoad로 씬이 바뀌어도 풀을 유지하여 프리웜을 1회만 수행한다.
*/
public class ResourcePickupVFXPool : MonoBehaviour
{
    public static ResourcePickupVFXPool Instance;

    [Header("Prefab")]
    [SerializeField] private ResourcePickupVFX prefab;  // 풀에서 관리할 VFX 프리팹

    [Header("Pool")]
    [SerializeField] private int prewarmCount = 80;     // 시작 시 미리 생성할 개수
    [SerializeField] private bool canExpand = true;     // 풀 부족 시 추가 생성 허용 여부

    [Header("Roots (Optional)")]
    [SerializeField] private Transform poolRoot;        // 비활성 보관 루트(없으면 this.transform)
    [SerializeField] private Transform spawnRoot;       // 활성 스폰 루트(없으면 null=씬 루트)

    // 대기(비활성) 오브젝트 보관용 큐
    private readonly Queue<ResourcePickupVFX> pool = new Queue<ResourcePickupVFX>(256);

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 씬 전환 후에도 풀을 유지하여 프리웜을 한 번만 수행한다.
        DontDestroyOnLoad(gameObject);

        // 프리팹이 없으면 풀 동작이 불가능하므로 에러를 남기고 종료한다.
        if (prefab == null)
        {
            Debug.LogError("[VFXPool] prefab is null");
            return;
        }

        // poolRoot를 비워두면 현재 오브젝트를 기본 루트로 사용한다.
        if (poolRoot == null) poolRoot = transform;

        Prewarm();
    }

    /*
        프리웜(Prewarm)

        - 플레이 중 Instantiate가 몰리면 순간 프레임 드랍이 발생할 수 있으므로,
          시작 시점에 미리 생성해 풀에 채워둔다.
    */
    private void Prewarm()
    {
        for (int i = 0; i < prewarmCount; i++)
        {
            var v = CreateNew();
            Release(v);
        }
    }

    /*
        신규 생성

        - 생성 즉시 비활성화하고, 자신의 풀을 참조하도록 SetPool을 주입한다.
        - 기본적으로 poolRoot 아래에서 관리한다.
    */
    private ResourcePickupVFX CreateNew()
    {
        var parent = poolRoot != null ? poolRoot : transform;

        var v = Instantiate(prefab, parent);
        v.gameObject.SetActive(false);
        v.SetPool(this);
        return v;
    }

    /*
        Get

        - 풀에서 1개를 꺼내 위치/회전을 세팅 후 활성화하여 반환한다.
        - 풀이 비었을 때 canExpand가 true면 새로 생성하여 반환한다.
        - spawnRoot가 있으면 그 하위로, 없으면 씬 루트로 배치한다.
    */
    public ResourcePickupVFX Get(Vector3 pos, Quaternion rot)
    {
        ResourcePickupVFX v = (pool.Count > 0) ? pool.Dequeue() : null;

        if (v == null)
        {
            // 확장 불가면 null 반환(호출부에서 생성 실패로 처리)
            if (!canExpand) return null;
            v = CreateNew();
        }

        var tr = v.transform;

        // 활성 루트로 이동(정리/정렬 목적)
        if (spawnRoot != null) tr.SetParent(spawnRoot, false);
        else tr.SetParent(null, false);

        // 위치/회전을 한 번에 반영한다.
        tr.SetPositionAndRotation(pos, rot);

        v.gameObject.SetActive(true);
        return v;
    }

    /*
        Release

        - 사용이 끝난 VFX를 비활성화하고 poolRoot 아래로 되돌린 뒤 큐에 넣는다.
        - 중복 Release를 막기 위해 이미 비활성이면 무시한다.
    */
    public void Release(ResourcePickupVFX v)
    {
        if (v == null) return;

        var go = v.gameObject;

        // 이미 비활성이면 풀에 들어간 것으로 보고 중복 반환을 방지한다.
        if (!go.activeSelf) return;

        go.SetActive(false);

        var tr = v.transform;

        // 비활성 보관 루트로 이동한다.
        if (poolRoot != null) tr.SetParent(poolRoot, false);
        else tr.SetParent(transform, false);

        pool.Enqueue(v);
    }
}
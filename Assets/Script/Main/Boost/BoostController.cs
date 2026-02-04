using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BoostController : MonoBehaviour
{
    [SerializeField] private bool debugLog = false;

    private float baseSpeedBeforeBoost = 0f;
    private float boostedSpeed = 0f;
    private Coroutine boostCo;

    private Camera cachedCam;
    private Collider2D col;

    private static long NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        cachedCam = Camera.main; // 없으면 클릭 시점에 다시 잡음
    }

    private void Update()
    {
        if (WasPressedThisFrame())
            TryActivateBoost();
    }

    private void OnEnable()
    {
        RestoreBoostState();
    }

    private void RestoreBoostState()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return;

        long now = NowMs();

        // 부스트가 "진행 중"이면 부스트 속도를 재적용
        if (now < b.boostEndUnixMs)
        {
            float percent = sm.GetBoostSpeed();
            float baseSpeed = (b.baseSpeedBeforeBoost > 0f) ? b.baseSpeedBeforeBoost : sm.GetSpeed();
            float targetBoosted = baseSpeed * (1f + percent / 100f);

            sm.SetSpeed(targetBoosted);

            // 남은 시간만큼 코루틴 다시 돌려서 종료/쿨타임 처리도 정상화
            if (boostCo != null) StopCoroutine(boostCo);
            boostCo = StartCoroutine(ResumeBoostRoutine(targetBoosted, baseSpeed));
            return;
        }

        // 부스트가 끝났는데도 속도가 부스트값이면 원복
        if (b.boostEndUnixMs != 0)
            b.boostEndUnixMs = 0;

        if (b.baseSpeedBeforeBoost > 0f)
        {
            // 현재 속도가 부스트로 올려진 값일 가능성이 높으니 원복
            sm.SetSpeed(b.baseSpeedBeforeBoost);
            b.baseSpeedBeforeBoost = 0f;
            sm.Save();
        }
    }

    private IEnumerator ResumeBoostRoutine(float boosted, float baseSpeed)
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) { boostCo = null; yield break; }

        // 남은 부스트 시간
        float remain = Mathf.Max(0f, (b.boostEndUnixMs - NowMs()) / 1000f);
        yield return new WaitForSeconds(remain);

        // 종료 시점 원복 (다른 시스템이 speed 바꿨으면 덮어쓰지 않기)
        float current = sm.GetSpeed();
        if (Mathf.Abs(current - boosted) < 0.0001f)
            sm.SetSpeed(baseSpeed);

        float cooldown = b.boostCoolTime;
        b.cooldownEndUnixMs = NowMs() + (long)(cooldown * 1000f);
        b.boostEndUnixMs = 0;
        b.baseSpeedBeforeBoost = 0f;
        sm.Save();

        boostCo = null;
    }

    private bool WasPressedThisFrame()
    {
        // Mouse(에디터/PC)
        if (Input.GetMouseButtonDown(0))
            return IsHitByScreenPoint(Input.mousePosition);

        // Touch(모바일)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                return IsHitByScreenPoint(t.position);
        }

        return false;
    }

    private bool IsHitByScreenPoint(Vector3 screenPos)
    {
        if (col == null) return false;

        if (cachedCam == null)
            cachedCam = Camera.main;

        if (cachedCam == null) return false;

        Vector2 worldPos = cachedCam.ScreenToWorldPoint(screenPos);

        // Raycast 대신 OverlapPoint (가벼움 + 정확)
        return col.OverlapPoint(worldPos);
    }

    private void TryActivateBoost()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (!sm.IsBoostUnlocked()) return;

        if (!TryGetBoost(sm, out var b)) return;

        long now = NowMs();

        if (now < b.cooldownEndUnixMs)
        {
            if (debugLog) Debug.Log("[Boost] cooldown");
            return;
        }

        if (now < b.boostEndUnixMs)
            return;

        if (boostCo != null) StopCoroutine(boostCo);
        boostCo = StartCoroutine(BoostRoutine());
    }

    private IEnumerator BoostRoutine()
    {
        MissionProgressManager.Instance?.Add("boost_use_count", 1);

        SaveManager sm = SaveManager.Instance;
        if (sm == null) yield break;

        if (!TryGetBoost(sm, out var b)) yield break;

        float percent = sm.GetBoostSpeed();
        float duration = Mathf.Clamp(sm.GetBoostTime(), 0.01f, 45f);
        float cooldown = b.boostCoolTime;

        long now = NowMs();

        // 부스트 시작 시간 기록
        b.boostEndUnixMs = now + (long)(duration * 1000f);
        sm.Save(); // 시작 상태 저장

        baseSpeedBeforeBoost = sm.GetSpeed();
        b.baseSpeedBeforeBoost = baseSpeedBeforeBoost;
        boostedSpeed = baseSpeedBeforeBoost * (1f + percent / 100f);
        sm.SetSpeed(boostedSpeed);

        if (debugLog) Debug.Log("[Boost] ON " + duration + "s");

        yield return new WaitForSeconds(duration);

        // 부스트 도중 다른 시스템이 speed를 바꿨으면 덮어쓰지 않기
        float current = sm.GetSpeed();
        if (Mathf.Abs(current - boostedSpeed) < 0.0001f)
            sm.SetSpeed(baseSpeedBeforeBoost);

        // 쿨타임 기록
        b.cooldownEndUnixMs = NowMs() + (long)(cooldown * 1000f);
        b.boostEndUnixMs = 0;
        b.baseSpeedBeforeBoost = 0f;
        sm.Save(); // 종료 상태 저장

        if (debugLog) Debug.Log("[Boost] OFF. Cooldown " + cooldown + "s");

        boostCo = null;
    }

    private bool TryGetBoost(SaveManager sm, out SaveData.Boost boost)
    {
        boost = null;
        if (sm == null || sm.Data == null || sm.Data.boost == null) return false;
        boost = sm.Data.boost;
        return true;
    }

    // UI용
    public float GetBoostRemaining()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return 0f;

        long remainMs = b.boostEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    public float GetCooldownRemaining()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return 0f;

        long remainMs = b.cooldownEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    public bool IsBoosting()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return false;

        return NowMs() < b.boostEndUnixMs;
    }
}
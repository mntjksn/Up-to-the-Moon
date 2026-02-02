using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BoostController : MonoBehaviour
{
    [SerializeField] private bool debugLog = false;

    private float baseSpeedBeforeBoost = 0f;
    private float boostedSpeed = 0f;
    private Coroutine boostCo;

    private static long NowMs()
    {
        return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void Update()
    {
        if (WasPressedThisFrame())
            TryActivateBoost();
    }

    private bool WasPressedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return IsHitByScreenPoint(Input.mousePosition);

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                return IsHitByScreenPoint(t.position);
        }
        return false;
    }

    private bool IsHitByScreenPoint(Vector3 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return false;

        Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        return hit.collider != null && hit.collider.gameObject == gameObject;
    }

    private void TryActivateBoost()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null || sm.Data.boost == null) return;

        var b = sm.Data.boost;

        if (!b.boostUnlock) return;

        long now = NowMs();

        // 쿨타임이면 발동 금지
        if (now < b.cooldownEndUnixMs)
        {
            if (debugLog) Debug.Log("[Boost] cooldown");
            return;
        }

        // 이미 부스트 중이면 무시
        if (now < b.boostEndUnixMs) return;

        if (boostCo != null) StopCoroutine(boostCo);
        boostCo = StartCoroutine(BoostRoutine());
    }

    private IEnumerator BoostRoutine()
    {
        MissionProgressManager.Instance?.Add("boost_use_count", 1);

        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null || sm.Data.boost == null) yield break;

        var b = sm.Data.boost;

        float percent = b.boostSpeed;        // 25, 50, ...
        float duration = Mathf.Clamp(b.boostTime, 0.01f, 45f);
        float cooldown = b.boostCoolTime;    // 60

        long now = NowMs();

        // 종료 절대시간 저장
        b.boostEndUnixMs = now + (long)(duration * 1000f);
        sm.Save();

        // 속도 적용
        baseSpeedBeforeBoost = sm.GetSpeed();
        boostedSpeed = baseSpeedBeforeBoost * (1f + percent / 100f);
        sm.SetSpeed(boostedSpeed);

        if (debugLog)
            Debug.Log($"[Boost] ON {duration}s");

        yield return new WaitForSeconds(duration);

        // 복귀
        float current = sm.GetSpeed();
        if (Mathf.Abs(current - boostedSpeed) < 0.0001f)
            sm.SetSpeed(baseSpeedBeforeBoost);

        // 쿨타임 절대시간 저장
        b.cooldownEndUnixMs = NowMs() + (long)(cooldown * 1000f);
        b.boostEndUnixMs = 0;
        sm.Save();

        if (debugLog)
            Debug.Log($"[Boost] OFF. Cooldown {cooldown}s");

        boostCo = null;
    }

    // ===== UI용 =====
    public float GetBoostRemaining()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data?.boost == null) return 0f;

        long remainMs = sm.Data.boost.boostEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    public float GetCooldownRemaining()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data?.boost == null) return 0f;

        long remainMs = sm.Data.boost.cooldownEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    public bool IsBoosting()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data?.boost == null) return false;

        return NowMs() < sm.Data.boost.boostEndUnixMs;
    }
}
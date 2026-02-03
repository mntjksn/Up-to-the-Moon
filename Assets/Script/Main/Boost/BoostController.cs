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
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                return IsHitByScreenPoint(t.position);
        }

        return false;
    }

    private bool IsHitByScreenPoint(Vector3 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        return hit.collider != null && hit.collider.gameObject == gameObject;
    }

    private void TryActivateBoost()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (!sm.IsBoostUnlocked()) return;

        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return;

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
        if (MissionProgressManager.Instance != null)
            MissionProgressManager.Instance.Add("boost_use_count", 1);

        SaveManager sm = SaveManager.Instance;
        if (sm == null) yield break;

        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) yield break;

        float percent = sm.GetBoostSpeed();
        float duration = Mathf.Clamp(sm.GetBoostTime(), 0.01f, 45f);
        float cooldown = b.boostCoolTime;

        long now = NowMs();

        b.boostEndUnixMs = now + (long)(duration * 1000f);
        sm.Save();

        baseSpeedBeforeBoost = sm.GetSpeed();
        boostedSpeed = baseSpeedBeforeBoost * (1f + percent / 100f);
        sm.SetSpeed(boostedSpeed);

        if (debugLog)
            Debug.Log("[Boost] ON " + duration + "s");

        yield return new WaitForSeconds(duration);

        float current = sm.GetSpeed();
        if (Mathf.Abs(current - boostedSpeed) < 0.0001f)
            sm.SetSpeed(baseSpeedBeforeBoost);

        b.cooldownEndUnixMs = NowMs() + (long)(cooldown * 1000f);
        b.boostEndUnixMs = 0;
        sm.Save();

        if (debugLog)
            Debug.Log("[Boost] OFF. Cooldown " + cooldown + "s");

        boostCo = null;
    }

    private bool TryGetBoost(SaveManager sm, out SaveData.Boost boost)
    {
        boost = null;

        if (sm == null) return false;
        if (sm.Data == null) return false;
        if (sm.Data.boost == null) return false;

        boost = sm.Data.boost;
        return true;
    }

    // UI¿ë
    public float GetBoostRemaining()
    {
        SaveManager sm = SaveManager.Instance;
        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return 0f;

        long remainMs = b.boostEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    public float GetCooldownRemaining()
    {
        SaveManager sm = SaveManager.Instance;
        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return 0f;

        long remainMs = b.cooldownEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    public bool IsBoosting()
    {
        SaveManager sm = SaveManager.Instance;
        SaveData.Boost b;
        if (!TryGetBoost(sm, out b)) return false;

        return NowMs() < b.boostEndUnixMs;
    }
}

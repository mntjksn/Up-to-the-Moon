using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
/*
    BoostController

    [역할]
    - 클릭/터치 입력으로 부스터를 발동한다.
    - 부스트 지속 시간 동안 속도를 증가시키고, 종료 후 원래 속도로 복원한다.
    - 쿨타임을 적용하여 연속 사용을 제한한다.
    - 앱을 껐다 켜도(재시작/백그라운드 복귀) 부스트/쿨타임 상태가 유지되도록
      Unix 시간(ms) 기반으로 상태를 저장/복원한다.

    [설계 의도]
    1) 입력 처리 통합
       - PC(마우스) / 모바일(터치)을 동일 흐름으로 처리한다.
       - Collider2D.OverlapPoint를 사용해 간단하고 비용이 낮은 클릭 판정을 한다.
    2) 상태 복원
       - boostEndUnixMs / cooldownEndUnixMs를 저장해 실시간 경과를 계산한다.
       - 부스트 중 종료된 경우 남은 시간만큼 코루틴을 재시작하여 "종료 처리"까지 정상 동작시킨다.
    3) 안전한 속도 복구
       - 부스트 도중 다른 시스템이 speed 값을 변경한 경우,
         종료 시 무조건 덮어쓰지 않고 "내가 올린 값"일 때만 원복한다.
*/
public class BoostController : MonoBehaviour
{
    // 디버그 로그 출력 여부
    [SerializeField] private bool debugLog = false;

    // 부스트 시작 시점의 기본 속도(원복 용도)
    private float baseSpeedBeforeBoost = 0f;

    // 부스트 적용 후 속도(내가 올린 값인지 비교하는 기준)
    private float boostedSpeed = 0f;

    // 현재 실행 중인 부스트 코루틴 핸들
    private Coroutine boostCo;

    // 카메라/콜라이더 캐시(매 프레임 조회 비용 감소)
    private Camera cachedCam;
    private Collider2D col;

    // 시스템 시간 기반(ms). 앱 재시작 후에도 경과 계산이 가능하다.
    private static long NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        cachedCam = Camera.main; // 없으면 클릭 시점에 다시 획득한다.
    }

    private void Update()
    {
        // 이번 프레임에 버튼/오브젝트가 눌렸으면 부스트 발동을 시도한다.
        if (WasPressedThisFrame())
            TryActivateBoost();
    }

    private void OnEnable()
    {
        // 오브젝트가 다시 활성화될 때 저장된 부스트 상태를 복원한다.
        RestoreBoostState();
    }

    /*
        저장된 부스트 상태 복원

        - 앱을 껐다 켠 경우에도 boostEndUnixMs/cooldownEndUnixMs를 기준으로
          현재 부스트 진행 여부를 판단한다.
        - 부스트가 진행 중이라면 부스트 속도를 재적용하고,
          남은 시간만큼 대기 후 종료/쿨타임 처리까지 이어서 수행한다.
        - 부스트가 끝났는데 속도가 부스트 상태로 남아있을 수 있으므로 원복 처리도 수행한다.
    */
    private void RestoreBoostState()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return;

        long now = NowMs();

        // 부스트가 진행 중이면 저장된 baseSpeed를 기준으로 부스트 속도를 재적용한다.
        if (now < b.boostEndUnixMs)
        {
            float percent = sm.GetBoostSpeed();
            float baseSpeed = (b.baseSpeedBeforeBoost > 0f) ? b.baseSpeedBeforeBoost : sm.GetSpeed();
            float targetBoosted = baseSpeed * (1f + percent / 100f);

            sm.SetSpeed(targetBoosted);

            // 남은 시간만큼만 기다렸다가 정상적으로 종료/쿨타임 처리를 수행한다.
            if (boostCo != null) StopCoroutine(boostCo);
            boostCo = StartCoroutine(ResumeBoostRoutine(targetBoosted, baseSpeed));
            return;
        }

        // 부스트가 끝난 상태인데 기록이 남아있으면 정리한다.
        if (b.boostEndUnixMs != 0)
            b.boostEndUnixMs = 0;

        // 저장된 baseSpeedBeforeBoost가 남아있다면 원복이 필요하다고 판단한다.
        if (b.baseSpeedBeforeBoost > 0f)
        {
            sm.SetSpeed(b.baseSpeedBeforeBoost);
            b.baseSpeedBeforeBoost = 0f;
            sm.Save();
        }
    }

    /*
        부스트 복원 루틴

        - 저장된 종료 시각까지 남은 시간만큼 대기한 뒤 종료 처리를 수행한다.
        - 종료 시점에도 다른 시스템이 speed를 바꿨을 수 있으므로
          "내가 올린 boosted 값"일 때만 baseSpeed로 되돌린다.
    */
    private IEnumerator ResumeBoostRoutine(float boosted, float baseSpeed)
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) { boostCo = null; yield break; }

        float remain = Mathf.Max(0f, (b.boostEndUnixMs - NowMs()) / 1000f);
        yield return new WaitForSeconds(remain);

        float current = sm.GetSpeed();
        if (Mathf.Abs(current - boosted) < 0.0001f)
            sm.SetSpeed(baseSpeed);

        // 종료 후 쿨타임 시작 시각을 기록한다.
        float cooldown = b.boostCoolTime;
        b.cooldownEndUnixMs = NowMs() + (long)(cooldown * 1000f);

        // 부스트 기록 정리
        b.boostEndUnixMs = 0;
        b.baseSpeedBeforeBoost = 0f;
        sm.Save();

        boostCo = null;
    }

    /*
        이번 프레임에 "눌림"이 발생했는지 확인한다.

        - PC: 마우스 클릭
        - 모바일: TouchPhase.Began
        - 눌린 위치가 내 Collider2D 영역인지까지 함께 검사한다.
    */
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

    /*
        화면 좌표를 월드 좌표로 변환한 뒤, Collider2D에 닿았는지 검사한다.

        - Raycast보다 간단한 OverlapPoint를 사용하여 비용을 줄인다.
    */
    private bool IsHitByScreenPoint(Vector3 screenPos)
    {
        if (col == null) return false;

        if (cachedCam == null)
            cachedCam = Camera.main;

        if (cachedCam == null) return false;

        Vector2 worldPos = cachedCam.ScreenToWorldPoint(screenPos);
        return col.OverlapPoint(worldPos);
    }

    /*
        부스트 발동 시도

        - 해금 여부를 확인한다.
        - 쿨타임/진행 중 여부를 확인하여 중복 발동을 방지한다.
        - 조건이 충족되면 부스트 코루틴을 시작한다.
    */
    private void TryActivateBoost()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (!sm.IsBoostUnlocked()) return;
        if (!TryGetBoost(sm, out var b)) return;

        long now = NowMs();

        // 쿨타임 중이면 발동하지 않는다.
        if (now < b.cooldownEndUnixMs)
        {
            if (debugLog) Debug.Log("[Boost] cooldown");
            return;
        }

        // 이미 부스트 진행 중이면 무시한다.
        if (now < b.boostEndUnixMs)
            return;

        if (boostCo != null) StopCoroutine(boostCo);
        boostCo = StartCoroutine(BoostRoutine());
    }

    /*
        부스트 수행 루틴

        흐름:
        1) 부스트 시작 시각/종료 시각을 저장한다.
        2) 시작 시점의 baseSpeed를 기록하고, boostedSpeed를 계산해 적용한다.
        3) duration 동안 대기 후, 조건이 맞으면 baseSpeed로 원복한다.
        4) 쿨타임 종료 시각을 기록하고 상태를 저장한다.
    */
    private IEnumerator BoostRoutine()
    {
        // 부스터 사용 횟수 미션 누적
        MissionProgressManager.Instance?.Add("boost_use_count", 1);

        SaveManager sm = SaveManager.Instance;
        if (sm == null) yield break;

        if (!TryGetBoost(sm, out var b)) yield break;

        float percent = sm.GetBoostSpeed();
        float duration = Mathf.Clamp(sm.GetBoostTime(), 0.01f, 45f);
        float cooldown = b.boostCoolTime;

        long now = NowMs();

        // 부스트 종료 시각 기록(앱 재시작 복원용)
        b.boostEndUnixMs = now + (long)(duration * 1000f);
        sm.Save();

        // 원복을 위해 부스트 전 속도를 저장한다.
        baseSpeedBeforeBoost = sm.GetSpeed();
        b.baseSpeedBeforeBoost = baseSpeedBeforeBoost;

        // 부스트 적용 속도 계산 후 적용한다.
        boostedSpeed = baseSpeedBeforeBoost * (1f + percent / 100f);
        sm.SetSpeed(boostedSpeed);

        if (debugLog) Debug.Log("[Boost] ON " + duration + "s");

        yield return new WaitForSeconds(duration);

        // 부스트 도중 다른 시스템이 speed를 바꿨으면 덮어쓰지 않는다.
        float current = sm.GetSpeed();
        if (Mathf.Abs(current - boostedSpeed) < 0.0001f)
            sm.SetSpeed(baseSpeedBeforeBoost);

        // 쿨타임 종료 시각 기록(앱 재시작 복원용)
        b.cooldownEndUnixMs = NowMs() + (long)(cooldown * 1000f);

        // 부스트 상태 정리
        b.boostEndUnixMs = 0;
        b.baseSpeedBeforeBoost = 0f;
        sm.Save();

        if (debugLog) Debug.Log("[Boost] OFF. Cooldown " + cooldown + "s");

        boostCo = null;
    }

    /*
        SaveData 내 Boost 데이터 참조를 안전하게 얻는다.

        - SaveManager/Data/boost 중 하나라도 없으면 false를 반환한다.
    */
    private bool TryGetBoost(SaveManager sm, out SaveData.Boost boost)
    {
        boost = null;
        if (sm == null || sm.Data == null || sm.Data.boost == null) return false;
        boost = sm.Data.boost;
        return true;
    }

    // -----------------------
    // UI 표시용 API
    // -----------------------

    // 남은 부스트 지속 시간을 초 단위로 반환한다.
    public float GetBoostRemaining()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return 0f;

        long remainMs = b.boostEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    // 남은 쿨타임을 초 단위로 반환한다.
    public float GetCooldownRemaining()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return 0f;

        long remainMs = b.cooldownEndUnixMs - NowMs();
        return Mathf.Max(0f, remainMs / 1000f);
    }

    // 현재 부스트 진행 중인지 반환한다.
    public bool IsBoosting()
    {
        SaveManager sm = SaveManager.Instance;
        if (!TryGetBoost(sm, out var b)) return false;

        return NowMs() < b.boostEndUnixMs;
    }
}
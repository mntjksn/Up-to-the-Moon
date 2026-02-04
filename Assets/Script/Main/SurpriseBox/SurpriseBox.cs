using System.Collections;
using UnityEngine;

/*
    SurpriseBox

    [역할]
    - 유저가 박스를 클릭(터치)하면 1회만 열리고,
      보상 지급 후 애니메이션(팝업 → 축소)으로 사라진다.
    - 보상은 “창고 최대 용량(Storage Max)의 일정 비율(minPercent~maxPercent)” 기준으로 계산한다.
    - 지급할 자원은 “현재 보유 중인 광물(id) 중 랜덤 1개”를 선택한다.
    - 창고 공간이 부족하면 clampToRemain 옵션에 따라
        (1) 남은 공간만큼으로 지급량을 줄이거나
        (2) 지급 실패 토스트를 띄운다.

    [설계 의도]
    1) 1회 클릭 보장
       - opened 플래그로 중복 클릭/중복 지급 방지

    2) 가벼운 연출
       - OpenPopAndDestroy 코루틴으로
         짧은 팝업(Scale Up) → 축소(Scale Down) 연출 후 제거

    3) 랜덤 자원 선택 최적화
       - PickRandomOwnedResourceId는 리스트 생성 없이
         Reservoir Sampling 방식으로 O(n), 추가 메모리 0

    [주의/전제]
    - SaveManager.GetStorageMax / GetStorageUsed / AddResource가 정상 동작해야 한다.
    - resources 배열의 인덱스가 item id(item_num)와 동일하다는 전제.
    - OnMouseDown 사용 시 Collider 필요.
*/
public class SurpriseBox : MonoBehaviour
{
    // --------------------
    // Reward Setting
    // --------------------

    [Header("Reward Percent (of Storage Max)")]
    [Range(0f, 1f)] public float minPercent = 0.05f; // 최소 비율
    [Range(0f, 1f)] public float maxPercent = 0.15f; // 최대 비율

    [Header("Open Scale Pop")]
    [SerializeField] private float popUpScale = 1.15f; // 팝업 시 배율
    [SerializeField] private float popUpTime = 0.08f;  // 팝업 시간
    [SerializeField] private float shrinkTime = 0.18f; // 축소 시간
    [SerializeField] private float endScale = 0f;      // 최종 스케일

    [Header("If storage is not enough")]
    [SerializeField] private bool clampToRemain = true; // 공간 부족 시 줄여서 지급할지

    [Header("Gold Or Mineral")]
    [Range(0f, 1f)]
    public float goldChance = 0.25f; // 골드가 나올 확률

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    // --------------------
    // Runtime
    // --------------------

    private bool opened = false;     // 중복 클릭 방지
    private Vector3 baseScale;       // 원래 스케일

    private void Awake()
    {
        // 초기 스케일 저장
        baseScale = transform.localScale;
    }

    private void OnMouseDown()
    {
        // 이미 열렸으면 무시
        if (opened) return;
        opened = true;

        // 보상 지급
        OpenBox();

        // 연출 시작
        StartCoroutine(OpenPopAndDestroy());
    }

    // --------------------
    // Animation
    // --------------------

    /*
        팝업 → 축소 연출 후 제거
    */
    private IEnumerator OpenPopAndDestroy()
    {
        Vector3 popScale = baseScale * popUpScale;

        // 팝업
        float t = 0f;
        while (t < popUpTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / popUpTime);
            transform.localScale = Vector3.Lerp(baseScale, popScale, EaseOutBack(a));
            yield return null;
        }

        // 축소
        Vector3 target = baseScale * endScale;

        t = 0f;
        while (t < shrinkTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / shrinkTime);
            transform.localScale = Vector3.Lerp(popScale, target, EaseInQuad(a));
            yield return null;
        }

        Destroy(gameObject);
    }

    // --------------------
    // Reward Logic
    // --------------------

    private void OpenBox()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null || sm.Data.resources == null)
            return;

        // SFX 재생
        if (sfx != null)
        {
            var snd = SoundManager.Instance;
            if (snd != null) sfx.mute = !snd.IsSfxOn();
            sfx.Play();
        }

        // 골드 or 광물 선택
        bool giveGold = Random.value < goldChance;

        // ----------------
        // 골드 지급
        // ----------------
        if (giveGold)
        {
            long maxStorage = sm.GetStorageMax();
            if (maxStorage <= 0) return;

            float minP = Mathf.Clamp01(minPercent);
            float maxP = Mathf.Clamp01(maxPercent);
            if (maxP < minP) { float t = minP; minP = maxP; maxP = t; }

            float percent = Random.Range(minP, maxP);
            long raw = (long)(maxStorage * percent + 0.5f);
            int gold = (int)Mathf.Clamp(raw, 1, int.MaxValue);

            sm.AddGold(gold);

            SurpriseToastManager.Instance
                ?.ShowGold($"+ {NumberFormatter.FormatKorean(gold)}원!");

            MissionProgressManager.Instance?.Add("surprise_box_open_count", 1);
            return;
        }

        // ----------------
        // 광물 지급
        // ----------------

        int id = PickRandomOwnedResourceId(sm.Data.resources);
        if (id < 0)
        {
            SurpriseToastManager.Instance?.Show(null, "보유 중인 광물이 없습니다!");
            return;
        }

        long maxStorage2 = sm.GetStorageMax();
        if (maxStorage2 <= 0) return;

        float minP2 = Mathf.Clamp01(minPercent);
        float maxP2 = Mathf.Clamp01(maxPercent);
        if (maxP2 < minP2) { float t = minP2; minP2 = maxP2; maxP2 = t; }

        float percent2 = Random.Range(minP2, maxP2);
        long rawAmount = (long)(maxStorage2 * percent2 + 0.5f);
        int amount = (int)Mathf.Clamp(rawAmount, 1, int.MaxValue);

        long remain = sm.GetStorageMax() - sm.GetStorageUsed();
        if (remain <= 0)
        {
            SurpriseToastManager.Instance?.Show(null, "창고가 가득 찼습니다!");
            return;
        }

        int give = amount;

        if ((long)amount > remain)
        {
            if (!clampToRemain)
            {
                SurpriseToastManager.Instance?.Show(null, "창고 공간이 부족합니다!");
                return;
            }

            give = (int)Mathf.Clamp(remain, 1, int.MaxValue);
        }

        sm.AddResource(id, give);

        SurpriseToastManager.Instance
            ?.ShowByItemNum(id, "+ " + NumberFormatter.FormatKorean(give) + "개!");
    }

    // --------------------
    // Utility
    // --------------------

    /*
        보유 중인 자원 id 중 랜덤 1개 선택
        - Reservoir Sampling
    */
    private int PickRandomOwnedResourceId(int[] resources)
    {
        int chosen = -1;
        int seen = 0;

        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] <= 0) continue;

            seen++;
            if (Random.Range(0, seen) == 0)
                chosen = i;
        }

        return chosen;
    }

    // --------------------
    // Easing
    // --------------------

    private float EaseInQuad(float x) => x * x;

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }
}
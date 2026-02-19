using System.Collections;
using UnityEngine;

/*
    SurpriseBox (Rebalanced)

    변경 요약
    1) 자원 선택 가중치(가격 기반)
       - 비싼 자원이 덜 나오도록 "로그 기반 역가중치" 사용
       - weight = 1 / (1 + log10(price+1))^k

    2) 자원 수량 상한(가격 구간별 cap)
       - 초반(<=100): 제한 없음(원하면 huge cap로 바꿔도 됨)
       - 중반(<=100,000): 최대 1000개
       - 후반(>100,000): 최대 500개
       - (원문 요구의 1,000,000~10,000,000 구간은 후반에 포함됨)

    3) 골드 보상 분리(단가 곱 제거)
       - "선택된 자원 단가 x 개수" 대신, storageMax 기반으로 골드 계산
       - hard cap 적용 (기본 1억)

    주의
    - SaveManager / ItemManager / SurpriseToastManager / NumberFormatter / MissionProgressManager 존재 전제
    - resources 배열 index == item_num(id) 전제
*/
public class SurpriseBox : MonoBehaviour
{
    // --------------------
    // Reward Setting
    // --------------------

    [Header("Mineral Amount (of Storage Max)")]
    [Range(0f, 1f)] public float minPercent = 0.03f; // 추천: 0.03 (3%)
    [Range(0f, 1f)] public float maxPercent = 0.08f; // 추천: 0.08 (8%)

    [Header("Weighted Pick (price-based)")]
    [SerializeField, Range(0f, 5f)]
    private float weightK = 1.75f; // 추천: 2 (비싼 자원 더 희귀)

    [Header("Open Scale Pop")]
    [SerializeField] private float popUpScale = 1.15f;
    [SerializeField] private float popUpTime = 0.08f;
    [SerializeField] private float shrinkTime = 0.18f;
    [SerializeField] private float endScale = 0f;

    [Header("If storage is not enough")]
    [SerializeField] private bool clampToRemain = true;

    [Header("Gold Or Mineral")]
    [Range(0f, 1f)]
    public float goldChance = 0.25f;

    [Header("Gold Reward (separate from item price)")]
    [SerializeField] private long storageMaxCapForGold = 500_000; // 네 게임 최대 창고
    [SerializeField] private int goldHardCap = 50_000_000;
    [SerializeField, Range(1.5f, 4f)] private float goldPower = 2.5f;
    [SerializeField] private Vector2 goldJitter = new Vector2(0.85f, 1.15f);

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    // --------------------
    // Runtime
    // --------------------

    private bool opened = false;
    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnMouseDown()
    {
        if (opened) return;
        opened = true;

        OpenBox();
        StartCoroutine(OpenPopAndDestroy());
    }

    // --------------------
    // Animation
    // --------------------

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

        TryPlaySfx();

        long maxStorage = sm.GetStorageMax();
        if (maxStorage <= 0) return;

        // 골드 or 광물 먼저 결정 (골드는 id/단가와 분리)
        bool giveGold = Random.value < goldChance;

        if (giveGold)
        {
            GiveGold(sm, maxStorage);
            MissionProgressManager.Instance?.Add("surprise_box_open_count", 1);
            return;
        }

        // ----------------
        // 광물 지급
        // ----------------
        int id = PickWeightedOwnedResourceId_LogPrice(sm.Data.resources, weightK);
        if (id < 0)
        {
            SurpriseToastManager.Instance?.Show(null, "보유 중인 광물이 없습니다!");
            return;
        }

        int amount = CalcMineralAmount(maxStorage);
        if (amount <= 0) return;

        // 창고 남은 공간 체크
        long remain = maxStorage - sm.GetStorageUsed();
        if (remain <= 0)
        {
            SurpriseToastManager.Instance?.Show(null, "창고가 가득 찼습니다!");
            return;
        }

        int give = amount;

        if ((long)give > remain)
        {
            if (!clampToRemain)
            {
                SurpriseToastManager.Instance?.Show(null, "창고 공간이 부족합니다!");
                return;
            }

            give = (int)Mathf.Clamp(remain, 1, int.MaxValue);
        }

        // 가격 구간별 수량 cap 적용
        int maxGive = GetMaxGiveByPrice(id);
        give = Mathf.Min(give, maxGive);

        if (give <= 0) return;

        // 지급 + 토스트
        sm.AddResource(id, give);
        SurpriseToastManager.Instance?.ShowByItemNum(id, "+ " + NumberFormatter.FormatKorean(give) + "개!");

        MissionProgressManager.Instance?.Add("surprise_box_open_count", 1);
    }

    private void GiveGold(SaveManager sm, long maxStorage)
    {
        long gold = CalcGoldReward(maxStorage);

        if (gold <= 0)
        {
            SurpriseToastManager.Instance?.ShowGold(" + 0원");
            return;
        }

        // 기존 스타일 유지: 1억 이상이면 1억만 지급 (hard cap)
        if (gold >= goldHardCap)
        {
            SurpriseToastManager.Instance?.ShowGold($" + {NumberFormatter.FormatKorean(goldHardCap)}원!");
            sm.AddGold(goldHardCap);
        }
        else
        {
            SurpriseToastManager.Instance?.ShowGold($" + {NumberFormatter.FormatKorean(gold)}원!");
            sm.AddGold((int)gold);
        }
    }

    // --------------------
    // Calc
    // --------------------

    private int CalcMineralAmount(long maxStorage)
    {
        float minP = Mathf.Clamp01(minPercent);
        float maxP = Mathf.Clamp01(maxPercent);
        if (maxP < minP) { float t = minP; minP = maxP; maxP = t; }

        float percent = Random.Range(minP, maxP);
        long rawAmount = (long)(maxStorage * percent + 0.5f);

        // 최소 1개
        return (int)Mathf.Clamp(rawAmount, 1, int.MaxValue);
    }

    // 골드 보상: storageMax 기반 (단가 곱 제거)
    private long CalcGoldReward(long storageMax)
    {
        double cap = goldHardCap;

        double r = storageMaxCapForGold <= 0 ? 0.0 : (double)storageMax / storageMaxCapForGold;
        if (r < 0.0) r = 0.0;
        if (r > 1.0) r = 1.0;

        double baseGold = cap * System.Math.Pow(r, goldPower);

        double jitter = Random.Range(goldJitter.x, goldJitter.y);
        long gold = (long)(baseGold * jitter + 0.5);

        if (gold < 1) gold = 1;
        if (gold > goldHardCap) gold = goldHardCap;

        return gold;
    }

    // --------------------
    // Caps (by price range)
    // --------------------
    // 요구안:
    // - 초반(<=100): 제한 없음
    // - 중반(<=100,000): 1000개
    // - 후반(>100,000): 500개
    private int GetMaxGiveByPrice(int itemId)
    {
        int price = GetGoldValuePerItem(itemId);
        if (price <= 0) price = 1;

        if (price <= 1200) return 10000;  // 초반
        if (price <= 100_000) return 5000;      // 중반
        if (price <= 500_000) return 1000;      // 중반
        if (price <= 2_000_000) return 500;      // 중반
        if (price <= 5_000_000) return 100;      // 중반
        return 50;                              // 후반
    }

    // --------------------
    // Weighted Pick (log price)
    // --------------------
    private int PickWeightedOwnedResourceId_LogPrice(int[] resources, float k)
    {
        var im = ItemManager.Instance;
        if (im == null) return PickRandomOwnedResourceId(resources); // fallback

        // k가 0이면 사실상 균등에 가까움
        if (k < 0.0001f) return PickRandomOwnedResourceId(resources);

        double total = 0.0;

        // 1) 총 가중치 합
        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] <= 0) continue;

            int price = GetGoldValuePerItem(i);
            if (price <= 0) price = 1;

            double v = System.Math.Log10(price + 1.0);
            double w = 1.0 / System.Math.Pow(1.0 + v, k);

            total += w;
        }

        if (total <= 0.0) return -1;

        // 2) 룰렛 선택
        double r = Random.value * total;
        double acc = 0.0;

        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] <= 0) continue;

            int price = GetGoldValuePerItem(i);
            if (price <= 0) price = 1;

            double v = System.Math.Log10(price + 1.0);
            double w = 1.0 / System.Math.Pow(1.0 + v, k);

            acc += w;
            if (r <= acc) return i;
        }

        // 부동소수 오차 대비
        return -1;
    }

    // 기존 균등 랜덤(보유 자원 중)
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

    private int GetGoldValuePerItem(int itemId)
    {
        var im = ItemManager.Instance;
        if (im == null) return 0;

        var item = im.GetItem(itemId);
        if (item == null) return 0;

        return Mathf.Max(0, item.item_price);
    }

    private void TryPlaySfx()
    {
        if (sfx == null) return;

        var snd = SoundManager.Instance;
        if (snd != null) sfx.mute = !snd.IsSfxOn();

        sfx.Play();
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
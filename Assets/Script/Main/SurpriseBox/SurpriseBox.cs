using System.Collections;
using UnityEngine;

public class SurpriseBox : MonoBehaviour
{
    [Header("Reward Percent (of Storage Max)")]
    [Range(0f, 1f)] public float minPercent = 0.05f;
    [Range(0f, 1f)] public float maxPercent = 0.15f;

    [Header("Open Scale Pop")]
    [SerializeField] private float popUpScale = 1.15f;
    [SerializeField] private float popUpTime = 0.08f;
    [SerializeField] private float shrinkTime = 0.18f;
    [SerializeField] private float endScale = 0f;

    [Header("If storage is not enough")]
    [SerializeField] private bool clampToRemain = true;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

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

    private IEnumerator OpenPopAndDestroy()
    {
        Vector3 popScale = baseScale * popUpScale;

        float t = 0f;
        while (t < popUpTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / popUpTime);
            transform.localScale = Vector3.Lerp(baseScale, popScale, EaseOutBack(a));
            yield return null;
        }

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

    private void OpenBox()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null || sm.Data.resources == null) return;

        // SFX
        if (sfx != null)
        {
            var snd = SoundManager.Instance;
            if (snd != null) sfx.mute = !snd.IsSfxOn();
            sfx.Play();
        }

        int id = PickRandomOwnedResourceId(sm.Data.resources);
        if (id < 0)
        {
            SurpriseToastManager.Instance?.Show(null, "보유 중인 광물이 없습니다!");
            return;
        }

        long maxStorage = sm.GetStorageMax();
        if (maxStorage <= 0) return;

        // percent 안전 보정
        float minP = Mathf.Clamp01(minPercent);
        float maxP = Mathf.Clamp01(maxPercent);
        if (maxP < minP) { float tmp = minP; minP = maxP; maxP = tmp; }

        float percent = Random.Range(minP, maxP);
        long rawAmount = (long)(maxStorage * percent + 0.5f); // round
        long amountL = (long)Mathf.Clamp(rawAmount, 1, int.MaxValue);
        int amount = (int)amountL;

        long used = sm.GetStorageUsed();
        long remain = maxStorage - used;

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
        MissionProgressManager.Instance?.Add("surprise_box_open_count", 1);

        // 토스트 1회만
        string msg = "+ " + NumberFormatter.FormatKorean(give) + "개!";
        SurpriseToastManager.Instance?.ShowByItemNum(id, msg);

        // 줄어서 지급된 경우 다른 문구를 원하면 이렇게
        // if (give < amount)
        //     SurpriseToastManager.Instance?.ShowByItemNum(id, "공간 부족으로 일부만 지급!");
    }

    // 리스트 할당 없이 "보유 중인 id 중 랜덤 1개" 뽑기
    // Reservoir Sampling 방식: O(n), 추가 메모리 0
    private int PickRandomOwnedResourceId(int[] resources)
    {
        int chosen = -1;
        int seen = 0;

        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] <= 0) continue;

            seen++;
            // 1/seen 확률로 현재 i로 교체
            if (Random.Range(0, seen) == 0)
                chosen = i;
        }

        return chosen;
    }

    private float EaseInQuad(float x) => x * x;

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }
}
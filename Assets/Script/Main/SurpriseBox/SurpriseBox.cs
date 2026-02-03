using System.Collections;
using System.Collections.Generic;
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

        if (sfx != null)
        {
            sfx.mute = !SoundManager.Instance.IsSfxOn();
            sfx.Play();
        }

        List<int> available = new List<int>();
        for (int i = 0; i < sm.Data.resources.Length; i++)
        {
            if (sm.Data.resources[i] > 0)
                available.Add(i);
        }

        if (available.Count == 0) return;

        int id = available[Random.Range(0, available.Count)];

        long maxStorage = sm.GetStorageMax();
        if (maxStorage <= 0) return;

        float percent = Random.Range(minPercent, maxPercent);
        long rawAmount = (long)Mathf.RoundToInt(maxStorage * percent);
        int amount = (int)Mathf.Clamp(rawAmount, 1, int.MaxValue);

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

        string msg = "+ " + NumberFormatter.FormatKorean(give) + "개!";
        SurpriseToastManager.Instance?.ShowByItemNum(id, msg);

        // (선택) 원래 amount보다 줄었으면 안내 토스트 추가로 띄우고 싶으면
         if (give < amount)
            SurpriseToastManager.Instance?.ShowByItemNum(id, msg);
    }

    private float EaseInQuad(float x)
    {
        return x * x;
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }
}
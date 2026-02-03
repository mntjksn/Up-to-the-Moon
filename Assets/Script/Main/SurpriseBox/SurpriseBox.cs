using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurpriseBox : MonoBehaviour
{
    [Header("Reward Percent (of Storage Max)")]
    [Range(0f, 1f)] public float minPercent = 0.05f; // 5%
    [Range(0f, 1f)] public float maxPercent = 0.15f; // 15%

    [Header("Open Scale Pop")]
    [SerializeField] private float popUpScale = 1.15f;   // 순간 커지는 비율
    [SerializeField] private float popUpTime = 0.08f;    // 커지는 시간
    [SerializeField] private float shrinkTime = 0.18f;   // 줄어드는 시간
    [SerializeField] private float endScale = 0f;        // 최종 스케일(0이면 사라짐)

    [Header("If storage is not enough")]
    [SerializeField] private bool clampToRemain = true; // true면 남은공간만 지급, false면 그냥 실패

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
        // 애니 후 삭제
        StartCoroutine(OpenPopAndDestroy());
    }

    private IEnumerator OpenPopAndDestroy()
    {
        // 1) 살짝 커졌다가
        Vector3 popScale = baseScale * popUpScale;
        float t = 0f;
        while (t < popUpTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / popUpTime);
            transform.localScale = Vector3.Lerp(baseScale, popScale, EaseOutBack(a));
            yield return null;
        }

        // 2) 줄어들면서 사라짐
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

    // ---- Easing (느낌 좋게) ----
    private float EaseInQuad(float x) => x * x;

    // 살짝 튕기는 느낌(원치 않으면 그냥 a로 바꾸면 됨)
    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }


    private void OpenBox()
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.Data == null) return;

        sfx.mute = !SoundManager.Instance.IsSfxOn();
        sfx.Play();

        // 1) 해금된 자원만 추리기 (한 번이라도 보유한 적 있는 자원만)
        List<int> available = new List<int>();
        for (int i = 0; i < sm.Data.resources.Length; i++)
        {
            if (sm.Data.resources[i] > 0)
                available.Add(i);
        }
        if (available.Count == 0) return;

        // 2) 랜덤 자원 선택
        int id = available[Random.Range(0, available.Count)];

        // 3) 지급량 = 최대 적재량의 퍼센트
        long maxStorage = sm.GetStorageMax();
        float percent = Random.Range(minPercent, maxPercent);
        int amount = Mathf.Max(1, Mathf.RoundToInt(maxStorage * percent));

        // 4) 현재 남은 공간 계산
        long used = sm.GetStorageUsed();

        long remain = maxStorage - used;

        // 이미 가득이면 종료
        if (remain <= 0)
        {
            SurpriseToastManager.Instance?.Show(null, "창고가 가득 찼습니다!");
            return;
        }

        // 5) 남은 공간 처리
        int give = amount;

        if (amount > remain)
        {
            if (!clampToRemain)
            {
                // 공간 부족이면 아예 지급 안 함
                SurpriseToastManager.Instance?.Show(null, "창고 공간이 부족합니다!");
                return;
            }

            // 남은 공간만 지급 (int로 clamp)
            give = Mathf.Max(1, (int)remain);
        }

        // 6) 지급
        sm.AddResource(id, give);
        MissionProgressManager.Instance?.Add("surprise_box_open_count", 1);

        // 토스트 메시지
        string msg = $"+ {FormatKoreanNumber(give)}개";
        SurpriseToastManager.Instance?.ShowByItemNum(id, msg);

        // (선택) 원래 amount보다 줄었으면 안내 토스트 추가로 띄우고 싶으면
        // if (give < amount)
        //     SurpriseToastManager.Instance?.Show(null, $"창고가 부족해 {FormatKoreanNumber(give)}개만 획득!");
    }

    private string FormatKoreanNumber(long n)
    {
        if (n == 0) return "0";

        bool neg = n < 0;
        ulong v = (ulong)(neg ? -n : n);

        const ulong MAN = 10_000UL;
        const ulong EOK = 100_000_000UL;
        const ulong JO = 1_000_000_000_000UL;
        const ulong GYEONG = 10_000_000_000_000_000UL;

        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(jo).Append("조"); }
        if (eok > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(eok).Append("억"); }
        if (man > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(man).Append("만"); }
        if (rest > 0) { if (sb.Length > 0) sb.Append(" "); sb.Append(rest); }

        return neg ? "-" + sb.ToString() : sb.ToString();
    }
}
using System.Collections;
using UnityEngine;

/*
    SurpriseBox

    [역할]
    - 유저가 박스를 클릭(터치)하면 1회만 열리고, 보상 지급 후 애니메이션(팝업→축소)으로 사라진다.
    - 보상은 “창고 최대 용량(Storage Max)의 일정 비율(minPercent~maxPercent)”을 기준으로 계산한다.
    - 지급할 자원은 “현재 보유 중인 광물(id) 중 랜덤 1개”를 뽑아서 해당 자원에 지급한다.
    - 창고 공간이 부족하면 clampToRemain 옵션에 따라
      (1) 남은 공간만큼으로 지급량을 줄이거나
      (2) 지급 실패 토스트를 띄운다.

    [설계 의도]
    1) 1회 클릭 보장
       - opened 플래그로 중복 클릭/중복 지급 방지

    2) 모바일/가벼운 연출
       - OpenPopAndDestroy 코루틴으로
         짧은 팝업(Scale up) 후 축소(Scale down) 연출을 수행하고 오브젝트를 파괴한다.

    3) 랜덤 자원 선택 최적화
       - PickRandomOwnedResourceId는 리스트를 새로 만들지 않고,
         "보유 중인 자원 인덱스 중 랜덤 1개"를 Reservoir Sampling 방식으로 선택한다.
         (O(n), 추가 메모리 0)

    [주의/전제]
    - SaveManager.GetStorageMax/GetStorageUsed/AddResource가 정상 동작해야 한다.
    - resources 배열의 인덱스가 곧 아이템 id(item_num)로 사용된다는 전제다.
    - OnMouseDown은 모바일에선 Collider 필요 + 터치 처리 환경에 따라 동작이 다를 수 있다.
      (필요 시 별도의 터치 입력 처리로 교체 가능)
*/
public class SurpriseBox : MonoBehaviour
{
    [Header("Reward Percent (of Storage Max)")]
    [Range(0f, 1f)] public float minPercent = 0.05f; // 지급량 최소 비율(창고 최대치 기준)
    [Range(0f, 1f)] public float maxPercent = 0.15f; // 지급량 최대 비율(창고 최대치 기준)

    [Header("Open Scale Pop")]
    [SerializeField] private float popUpScale = 1.15f; // 팝업 시 스케일 배수
    [SerializeField] private float popUpTime = 0.08f;  // 팝업까지 걸리는 시간
    [SerializeField] private float shrinkTime = 0.18f; // 축소까지 걸리는 시간
    [SerializeField] private float endScale = 0f;      // 최종 스케일(0이면 완전 축소)

    [Header("If storage is not enough")]
    [SerializeField] private bool clampToRemain = true; // 남은 공간보다 크면 지급량을 줄일지 여부

    [Header("SFX")]
    [SerializeField] private AudioSource sfx; // 박스 오픈 효과음

    private bool opened = false;  // 중복 클릭/중복 지급 방지 플래그
    private Vector3 baseScale;    // 원래 스케일(애니메이션 기준)

    private void Awake()
    {
        // 최초 스케일 저장(팝업/축소 연출에 사용)
        baseScale = transform.localScale;
    }

    private void OnMouseDown()
    {
        /*
            클릭(터치) 1회 처리
            - opened로 중복 진입 방지
            - OpenBox로 보상 지급
            - 연출 코루틴 시작 후 파괴
        */
        if (opened) return;
        opened = true;

        OpenBox();
        StartCoroutine(OpenPopAndDestroy());
    }

    /*
        팝업(확대) → 축소 연출 후 오브젝트 파괴
        - popUpTime 동안 baseScale → popScale (EaseOutBack)
        - shrinkTime 동안 popScale → target(endScale 적용) (EaseInQuad)
    */
    private IEnumerator OpenPopAndDestroy()
    {
        Vector3 popScale = baseScale * popUpScale;

        // 1) 팝업(확대) 구간
        float t = 0f;
        while (t < popUpTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / popUpTime);
            transform.localScale = Vector3.Lerp(baseScale, popScale, EaseOutBack(a));
            yield return null;
        }

        // 2) 축소 구간
        Vector3 target = baseScale * endScale;

        t = 0f;
        while (t < shrinkTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / shrinkTime);
            transform.localScale = Vector3.Lerp(popScale, target, EaseInQuad(a));
            yield return null;
        }

        // 3) 제거
        Destroy(gameObject);
    }

    /*
        보상 지급 로직
        1) SaveManager/데이터 유효성 확인
        2) 효과음 재생
        3) 보유 중인 자원 id 랜덤 선택(없으면 토스트)
        4) 창고 최대치 기반으로 지급량 계산(비율 랜덤)
        5) 남은 공간 확인 후 clampToRemain 규칙 적용
        6) 자원 지급 + 미션 카운트 증가 + 토스트 출력
    */
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

        // 보유 중인 자원(id) 중 랜덤 1개 선택
        int id = PickRandomOwnedResourceId(sm.Data.resources);
        if (id < 0)
        {
            SurpriseToastManager.Instance?.Show(null, "보유 중인 광물이 없습니다!");
            return;
        }

        // 창고 최대치 확인
        long maxStorage = sm.GetStorageMax();
        if (maxStorage <= 0) return;

        // percent 안전 보정(0~1로 클램프, min/max 역전 시 swap)
        float minP = Mathf.Clamp01(minPercent);
        float maxP = Mathf.Clamp01(maxPercent);
        if (maxP < minP) { float tmp = minP; minP = maxP; maxP = tmp; }

        // 지급량 계산(창고 최대치 * 랜덤 비율)
        float percent = Random.Range(minP, maxP);
        long rawAmount = (long)(maxStorage * percent + 0.5f); // 반올림
        long amountL = (long)Mathf.Clamp(rawAmount, 1, int.MaxValue);
        int amount = (int)amountL;

        // 남은 공간 계산
        long used = sm.GetStorageUsed();
        long remain = maxStorage - used;

        // 공간이 0 이하라면 지급 불가
        if (remain <= 0)
        {
            SurpriseToastManager.Instance?.Show(null, "창고가 가득 찼습니다!");
            return;
        }

        // 지급량 결정(남은 공간보다 크면 옵션에 따라 처리)
        int give = amount;
        if ((long)amount > remain)
        {
            if (!clampToRemain)
            {
                SurpriseToastManager.Instance?.Show(null, "창고 공간이 부족합니다!");
                return;
            }

            // 남은 공간만큼으로 지급량 축소
            give = (int)Mathf.Clamp(remain, 1, int.MaxValue);
        }

        // 실제 지급
        sm.AddResource(id, give);
        MissionProgressManager.Instance?.Add("surprise_box_open_count", 1);

        // 토스트(1회만)
        string msg = "+ " + NumberFormatter.FormatKorean(give) + "개!";
        SurpriseToastManager.Instance?.ShowByItemNum(id, msg);

        // 줄어서 지급된 경우 다른 문구를 원하면 이렇게
        // if (give < amount)
        //     SurpriseToastManager.Instance?.ShowByItemNum(id, "공간 부족으로 일부만 지급!");
    }

    /*
        리스트 할당 없이 "보유 중인 id 중 랜덤 1개" 뽑기
        - Reservoir Sampling 방식: O(n), 추가 메모리 0
        - resources[i] > 0 인 i들 중 균등 랜덤 선택
        - 반환값 i가 곧 resource id로 사용됨
    */
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

    /*
        이징 함수들
        - EaseInQuad: 천천히 시작 → 빠르게(축소 구간)
        - EaseOutBack: 빠르게 나가며 약간 튕기는 느낌(팝업 구간)
    */
    private float EaseInQuad(float x) => x * x;

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }
}
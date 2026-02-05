using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
    BlackholeUpgradeUI

    [역할]
    - 블랙홀 업그레이드(수급량/창고 용량) UI를 갱신하고 구매를 처리한다.
    - 저장 데이터(SaveManager)를 기준으로 현재/다음 수치와 가격을 표시한다.
    - 골드 보유량과 최대 레벨 여부에 따라 버튼을 활성/비활성 처리한다.

    [설계 의도]
    1) UI 갱신 최적화
       - RefreshAll()이 자주 호출되더라도, 값이 바뀐 경우에만 텍스트/버튼을 갱신한다.
       - lastIncomeLv/lastStorageLv/lastGold 캐시를 사용해 불필요한 UI 업데이트를 줄인다.
    2) 규칙의 함수화
       - 레벨별 수급량/용량, 가격 계산을 함수로 분리하여 밸런스 조정이 쉬워진다.
    3) 안전한 구매 처리
       - 최대 레벨, 골드 부족 등 조건을 먼저 체크하고 성공했을 때만 데이터 변경/미션 카운트를 수행한다.
*/
public class BlackholeUpgradeUI : MonoBehaviour
{
    [Header("Income UI")]
    [SerializeField] private TextMeshProUGUI incomeValueText;   // 현재/다음 수급량 표시
    [SerializeField] private TextMeshProUGUI incomePriceText;   // 수급 업그레이드 가격 표시
    [SerializeField] private Button incomeBuyButton;            // 수급 업그레이드 구매 버튼

    [Header("Storage UI")]
    [SerializeField] private TextMeshProUGUI storageValueText;  // 현재/다음 용량 표시
    [SerializeField] private TextMeshProUGUI storagePriceText;  // 용량 업그레이드 가격 표시
    [SerializeField] private Button storageBuyButton;           // 용량 업그레이드 구매 버튼

    [Header("Tuning")]
    // 시스템 제한(레벨 상한)
    [SerializeField] private int maxIncomeLv = 50;
    [SerializeField] private int maxStorageLv = 50;

    [Header("SFX")]
    [SerializeField] private AudioSource sfx;

    // ---- UI 캐시(같은 값이면 텍스트/버튼 갱신을 생략한다) ----
    private int lastIncomeLv = int.MinValue;
    private int lastStorageLv = int.MinValue;
    private long lastGold = long.MinValue;

    // 버튼 리스너 중복 등록 방지용
    private bool listenersBound = false;

    private void OnEnable()
    {
        // 패널이 켜질 때 리스너를 연결하고, UI를 강제로 최신 상태로 만든다.
        BindListenersOnce();
        ForceRefreshAll();
    }

    private void OnDisable()
    {
        // 리스너 누적 방지를 위해 비활성화 시 제거한다.
        // (대안으로 OnDestroy에서만 제거하고 Enable/Disable에서는 유지하는 방식도 가능하다)
        UnbindListeners();
        listenersBound = false;
    }

    /*
        버튼 리스너 바인딩

        - OnEnable에서 AddListener를 반복하면 동일 콜백이 여러 번 실행될 수 있으므로,
          한 번만 바인딩하도록 가드한다.
    */
    private void BindListenersOnce()
    {
        if (listenersBound) return;

        if (incomeBuyButton != null) incomeBuyButton.onClick.AddListener(BuyIncome);
        if (storageBuyButton != null) storageBuyButton.onClick.AddListener(BuyStorage);

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (incomeBuyButton != null) incomeBuyButton.onClick.RemoveListener(BuyIncome);
        if (storageBuyButton != null) storageBuyButton.onClick.RemoveListener(BuyStorage);
    }

    /*
        외부에서 호출해도 되는 갱신 함수

        - 저장 데이터에서 현재 레벨/골드를 읽고,
          이전 값과 비교하여 변경된 항목만 부분 갱신한다.
    */
    public void RefreshAll()
    {
        var save = SaveManager.Instance;
        if (save == null) return;

        int incomeLv = save.GetIncomeLv();
        int storageLv = save.GetStorageLv();
        long gold = save.GetGold();

        // 모든 값이 동일하면 UI 갱신을 생략한다.
        if (incomeLv == lastIncomeLv && storageLv == lastStorageLv && gold == lastGold)
            return;

        // 수급 UI는 수급 레벨 또는 골드가 바뀐 경우에만 갱신한다.
        if (incomeLv != lastIncomeLv || gold != lastGold)
            RefreshIncome(save, incomeLv, gold);

        // 용량 UI는 용량 레벨 또는 골드가 바뀐 경우에만 갱신한다.
        if (storageLv != lastStorageLv || gold != lastGold)
            RefreshStorage(save, storageLv, gold);

        // 캐시 업데이트
        lastIncomeLv = incomeLv;
        lastStorageLv = storageLv;
        lastGold = gold;
    }

    /*
        패널 오픈 시 강제 갱신

        - 캐시 값을 초기화하여 RefreshAll이 반드시 UI를 갱신하게 만든다.
    */
    private void ForceRefreshAll()
    {
        lastIncomeLv = int.MinValue;
        lastStorageLv = int.MinValue;
        lastGold = long.MinValue;
        RefreshAll();
    }

    /*
        수급(Income) UI 갱신

        - 현재/다음 수급량, 가격, 버튼 활성 여부를 결정한다.
        - 최대 레벨이면 "MAX"로 표기하고 구매 버튼을 비활성화한다.
    */
    private void RefreshIncome(SaveManager save, int lv, long gold)
    {
        bool isMax = lv >= maxIncomeLv;

        float cur = GetIncomeByLevel(lv);
        float next = GetIncomeByLevel(Mathf.Min(lv + 1, maxIncomeLv));

        if (incomeValueText != null)
        {
            incomeValueText.text = isMax
                ? $"{cur:0.##}개/s (MAX)"
                : $"{cur:0.##}개/s -> {next:0.##}개/s";
        }

        long price = GetIncomePrice(lv);

        if (incomePriceText != null)
            incomePriceText.text = isMax ? "MAX" : NumberFormatter.FormatKorean(price) + "원";

        // 구매 가능 조건: 최대 레벨이 아니고, 골드가 가격 이상
        if (incomeBuyButton != null)
            incomeBuyButton.interactable = !isMax && gold >= price;
    }

    /*
        수급 업그레이드 구매 처리

        - 조건(최대 레벨/골드 부족) 체크 후 성공 시에만 데이터 변경을 수행한다.
        - 미션 카운트는 "성공 시"에만 올라가도록 구매 성공 흐름 안에서 호출한다.
    */
    private void BuyIncome()
    {
        var save = SaveManager.Instance;
        if (save == null) return;

        int lv = save.GetIncomeLv();
        if (lv >= maxIncomeLv) return;

        long price = GetIncomePrice(lv);
        if (save.GetGold() < price) return;

        PlaySfx();

        // 성공 시에만 미션 카운트 증가
        MissionProgressManager.Instance?.Add("blackhole_income_upgrade_count", 1);

        save.AddGold(-price);
        save.AddIncomeLv(1);

        // 레벨 증가 후 수급량 값을 저장 데이터에 반영한다.
        float income = GetIncomeByLevel(lv + 1);
        save.SetIncome(income);

        RefreshAll();
    }

    /*
        레벨별 수급량 계산

        - 구간별 증가 폭을 다르게 적용하여 성장 체감을 조절한다.
        - 밸런싱이 필요하면 이 함수만 수정하면 된다.
    */
    public float GetIncomeByLevel(int L)
    {
        if (L <= 3) return 0.5f + 0.5f * L;
        if (L <= 6) return 2.0f + 1f * (L - 3);
        if (L <= 11) return 5.0f + 2f * (L - 6);
        if (L <= 15) return 15.0f + 2.5f * (L - 11);
        if (L <= 18) return 25.0f + 5f * (L - 15);
        return 40.0f + 10f * (L - 18);
    }

    /*
        수급 업그레이드 가격 계산

        - 지수 성장( basePrice * mult^lv ) 형태로 가격을 증가시킨다.
        - long 범위를 초과할 수 있으므로 overflow 방어를 한다.
        - 보기/지불 단위를 맞추기 위해 특정 단위(step)로 올림 처리한다.
    */
    private long GetIncomePrice(int lv)
    {
        double basePrice = 100;
        double mult = 2.25;
        double raw = basePrice * System.Math.Pow(mult, lv);

        if (raw > long.MaxValue) return long.MaxValue;

        long v = (long)raw;
        return CeilTo(v, 100);
    }

    /*
        용량(Storage) UI 갱신

        - 현재/다음 용량, 가격, 버튼 활성 여부를 결정한다.
        - 최대 레벨이면 "MAX"로 표기하고 구매 버튼을 비활성화한다.
    */
    private void RefreshStorage(SaveManager save, int lv, long gold)
    {
        bool isMax = lv >= maxStorageLv;

        long cur = GetStorageByLevel(lv);
        long next = GetStorageByLevel(Mathf.Min(lv + 1, maxStorageLv));

        if (storageValueText != null)
        {
            string curStr = NumberFormatter.FormatKorean(cur);
            storageValueText.text = isMax
                ? curStr + "개 (MAX)"
                : curStr + "개 -> " + NumberFormatter.FormatKorean(next) + "개";
        }

        long price = GetStoragePrice(lv);

        if (storagePriceText != null)
            storagePriceText.text = isMax ? "MAX" : NumberFormatter.FormatKorean(price) + "원";

        if (storageBuyButton != null)
            storageBuyButton.interactable = !isMax && gold >= price;
    }

    /*
        용량 업그레이드 구매 처리

        - 조건(최대 레벨/골드 부족) 체크 후 성공 시에만 데이터 변경을 수행한다.
        - 현재 구조에서는 SaveManager에 SetStorageMax API가 없어 저장 데이터에 직접 반영한다.
    */
    private void BuyStorage()
    {
        var save = SaveManager.Instance;
        if (save == null) return;

        int lv = save.GetStorageLv();
        if (lv >= maxStorageLv) return;

        long price = GetStoragePrice(lv);
        if (save.GetGold() < price) return;

        PlaySfx();

        save.AddGold(-price);
        save.AddStorageLv(1);

        // SaveManager에 전용 setter가 없으므로 데이터 구조에 직접 반영한다.
        if (save.Data != null && save.Data.blackHole != null)
        {
            long max = GetStorageByLevel(save.GetStorageLv());
            save.Data.blackHole.BlackHoleStorageMax = max;

            // debounce 저장 구조라면 부담이 낮다.
            save.Save();
        }

        RefreshAll();
    }

    /*
        레벨별 저장 용량 계산

        - 지수 성장( baseCap * mult^lv ) 형태로 증가시킨다.
        - long 범위 초과를 방어하고, step 단위로 올림 처리한다.
    */
    private long GetStorageByLevel(int lv)
    {
        long baseCap = 100;
        double mult = 2.6;
        double raw = baseCap * System.Math.Pow(mult, lv);

        if (raw > long.MaxValue) return long.MaxValue;

        long v = (long)raw;
        return CeilTo(v, 100);
    }

    /*
        용량 업그레이드 가격 계산

        - 지수 성장으로 가격을 증가시킨다.
        - 보기/지불 단위를 맞추기 위해 500 단위로 올림 처리한다.
    */
    private long GetStoragePrice(int lv)
    {
        double basePrice = 500;
        double mult = 4.5;
        double raw = basePrice * System.Math.Pow(mult, lv);

        if (raw > long.MaxValue) return long.MaxValue;

        long v = (long)raw;
        return CeilTo(v, 500);
    }

    /*
        사운드 재생

        - SoundManager의 설정에 따라 mute를 적용한다.
        - 구매 성공 시 사용자 피드백을 제공한다.
    */
    private void PlaySfx()
    {
        if (sfx == null) return;

        SoundManager sm = SoundManager.Instance;
        if (sm != null) sfx.mute = !sm.IsSfxOn();

        sfx.Play();
    }

    /*
        특정 단위(step)로 올림 처리

        - 가격/수치를 보기 좋은 단위로 맞추기 위해 사용한다.
        - 예: 1234를 100 단위로 올림하면 1300이 된다.
    */
    private long CeilTo(long value, long step)
    {
        if (step <= 0) return value;
        if (value <= 0) return 0;
        return ((value + step - 1) / step) * step;
    }
}
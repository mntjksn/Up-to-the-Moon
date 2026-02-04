using System.Text;

/*
    NumberFormatter

    [역할]
    - long 정수를 한국식 단위(만/억/조/경) 문자열로 변환한다.
    - 예)
        1234           -> "1234"
        12_345         -> "1만 2345"
        123_456_789    -> "1억 2345만 6789"
        5_000_000_000  -> "50억"
        -987_654_321   -> "-9억 8765만 4321"

    [설계 의도]
    1) 한국식 단위 기준 분해
       - 10,000 단위(만) 기준으로
         만(10^4), 억(10^8), 조(10^12), 경(10^16) 순으로 나눈다.

    2) 부호 처리
       - 음수인 경우 neg=true로 기록 후,
         절댓값을 기준으로 계산하고 마지막에 "-"를 붙인다.

    3) StringBuilder 사용
       - 문자열 덧셈(+)을 반복하지 않고 StringBuilder로 누적하여
         GC 발생과 성능 비용을 줄인다.

    [주의/전제]
    - 소수점은 처리하지 않고 정수(long)만 처리한다.
    - 단위는 최대 "경(10^16)"까지만 지원한다.
    - 단위 사이에는 공백(" ")을 넣어 가독성을 높인다.
*/
public static class NumberFormatter
{
    /*
        long 값을 한국식 단위 문자열로 변환
        - n: 변환할 정수 값
        - 반환: "경/조/억/만/나머지" 형식 문자열
    */
    public static string FormatKorean(long n)
    {
        // 0은 바로 반환
        if (n == 0) return "0";

        // 음수 여부 저장
        bool neg = n < 0;

        // 절댓값을 unsigned로 변환하여 안전하게 처리
        ulong v = (ulong)(neg ? -n : n);

        // 단위 기준 값
        const ulong MAN = 10_000UL;                  // 만 (10^4)
        const ulong EOK = 100_000_000UL;             // 억 (10^8)
        const ulong JO = 1_000_000_000_000UL;       // 조 (10^12)
        const ulong GYEONG = 10_000_000_000_000_000UL;  // 경 (10^16)

        // 각 단위별 몫 계산 후 나머지 갱신
        ulong gyeong = v / GYEONG; v %= GYEONG;
        ulong jo = v / JO; v %= JO;
        ulong eok = v / EOK; v %= EOK;
        ulong man = v / MAN; v %= MAN;
        ulong rest = v; // 만 단위 아래 나머지

        StringBuilder sb = new StringBuilder();

        // 단위가 있는 것만 순서대로 추가
        if (gyeong > 0) sb.Append(gyeong).Append("경");
        if (jo > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(jo).Append("조");
        }
        if (eok > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(eok).Append("억");
        }
        if (man > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(man).Append("만");
        }
        if (rest > 0)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(rest);
        }

        // 음수였으면 앞에 "-" 붙여서 반환
        return neg ? "-" + sb.ToString() : sb.ToString();
    }
}
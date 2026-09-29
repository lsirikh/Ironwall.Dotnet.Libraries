namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 장비 부품 건강의 <b>요약 단계</b> — 값이 클수록 나쁘다(최악 선택을 <c>Max</c> 로 한다).
/// </summary>
/// <remarks>
/// <para><see cref="None"/> 은 "요약할 것이 없다"(축 미수신 · 부품 선언 없음)이고, <see cref="Unknown"/> 은
/// "부품은 있는데 보고가 없거나 서버가 UNKNOWN 이라 했다"다. 둘 다 아이콘에 배지를 그리지 않는다 — 모르는 것은 그리지 않는다.</para>
/// </remarks>
public enum ComponentHealthLevel
{
    /// <summary>요약할 부품이 없다(축 미수신 · 선언 없음 · 전부 사용 안 함).</summary>
    None = 0,
    /// <summary>서버 <c>OK</c>.</summary>
    Ok = 1,
    /// <summary>보고 없음 또는 서버 <c>UNKNOWN</c>.</summary>
    Unknown = 2,
    /// <summary>서버 <c>DEGRADED</c> — 동작은 하나 성능 저하.</summary>
    Degraded = 3,
    /// <summary>서버 <c>FAULT</c>.</summary>
    Fault = 4,
}

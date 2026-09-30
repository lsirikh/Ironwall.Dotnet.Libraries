namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>
/// 부품 한 줄의 <b>건강 점</b> 분류 — 화면이 점 색을 고르는 데만 쓴다(글은 <see cref="ComponentDisplay"/>).
/// </summary>
/// <remarks>
/// 점 색 = <c>StatusNormalBrush</c>(정상) · <c>StatusWarningBrush</c>(저하) · <c>StatusCriticalBrush</c>(고장) ·
/// 미상은 회색(<c>TextMutedBrush</c>). "사용 안 함"은 점 대신 빈 원(윤곽)으로 그린다 — 색이 아니라 모양으로 가른다.
/// </remarks>
public enum ComponentHealthKind
{
    /// <summary>보고 없음 · UNKNOWN · 어휘 밖 값 — 회색 점.</summary>
    Unknown = 0,
    Ok,
    Warn,
    Crit,
    /// <summary><c>in_service=false</c> — 달려 있지만 쓰지 않는 부품.</summary>
    OutOfService,
}

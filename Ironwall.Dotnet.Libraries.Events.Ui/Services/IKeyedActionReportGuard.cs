namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;

/****************************************************************************
   Purpose      : 조치보고 멱등 가드의 '타입까지 보는' 입구.
                  기존 IActionReportGuard 는 int Id 하나로만 잠가서 탐지 3번과 장애 3번이
                  같은 자물쇠를 나눠 쓴다 — 조치 트레이가 둘을 한꺼번에 담으면 뒤엣것이
                  '이미 진행 중'으로 스킵되는데도 화면은 "적용" 으로 셌다(N-07 적대 검토 R1).
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>원본 이벤트의 종류 — 자물쇠 키의 앞자리.</summary>
public static class ActionReportKind
{
    public const string Detection = "detection";
    public const string Malfunction = "malfunction";
}

/// <summary>
/// <see cref="IActionReportGuard"/> 를 <b>넓히지 않고</b> 곁에 두는 입구. 기존 인터페이스를 고치면
/// 목 · 페이크가 전부 깨지므로(파라미터 목록 정확 일치) 새 인터페이스로 낸다.
/// </summary>
/// <remarks>
/// <para>의미:</para>
/// <list type="bullet">
///   <item>타입 있는 <see cref="TryEnter"/> 는 <c>{kind}:{id}</c> 하나만 잠근다 — 탐지 3번과 장애 3번은 서로를 막지 않는다.</item>
///   <item>옛 <see cref="IActionReportGuard.TryEnter(int)"/> 는 <b>그 Id 의 모든 종류</b>를 잠근다(지금 동작 보존).
///     자동 · 자동복구 · 배치 경로가 아직 Id 로만 잠그므로, 그쪽이 쥐고 있으면 타입 있는 쪽도 들어가지 못한다.</item>
/// </list>
/// </remarks>
public interface IKeyedActionReportGuard
{
    /// <summary>종류 + Id 로 점유를 시도한다. <paramref name="eventId"/> ≤ 0 이면 가드 대상이 아니다(항상 true).</summary>
    bool TryEnter(string kind, int eventId);

    /// <summary>종류 + Id 점유 해제.</summary>
    void Exit(string kind, int eventId);
}

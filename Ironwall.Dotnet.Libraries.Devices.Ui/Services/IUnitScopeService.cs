using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;
/****************************************************************************
   Purpose      : 현재 부대(unit) id 해석·캐시 — 서버 8.0 unit_id 주입용
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <b>이 클라이언트가 속한 부대의 서버 id</b>를 1회 해석해 캐시한다(서버 8.0 <c>unit_id</c> 주입의 유일한 출처).
/// </summary>
/// <remarks>
/// <para><b>왜 해석이 필요한가</b> — 우리가 설정으로 아는 것은 <b>부대 코드</b>(<c>GroupNats</c>, 예 <c>"unit001"</c>)뿐이고
/// 서버 쓰기 본문이 요구하는 것은 <b>정수 id</b>(<c>unit_id</c>)다. 둘을 잇는 사전은
/// <c>GET /api/units</c> 뿐이라(코드 필터 파라미터가 없어 목록을 받아 <c>code</c> 로 매칭한다)
/// 런타임 해석이 구조적으로 필요하다.
/// <br/>라이브 실측(2026-09-18, 개발 8.0.1): <c>total=1</c> · <c>{id:1, code:"unit001"}</c> — 우리 <c>GroupNats</c> 와 일치.</para>
///
/// <para><b>왜 관측 가능해야 하는가</b> — 8.0 서버는 <c>unit_id</c> 를 생략해도 422 를 주지 않고
/// <b>기본 부대로 묶고 서버 로거 경고만</b> 남긴다(응답 <c>warnings[]</c> 아님). 즉 누락이 클라이언트에
/// <b>아무 신호도 남기지 않는다</b>. 그래서 해석 실패는 반드시 <b>우리 로그</b>에 남긴다 —
/// 조용히 넘기면 다부대 전개 시 장비가 엉뚱한 부대에 붙은 뒤에야 발견된다.</para>
///
/// <para><b>안전 방향</b> — <see cref="IsUnitEra"/> 가 <c>false</c>(프로브 미주입·미확보 포함)면
/// <b>네트워크에 나가지 않고</b> 해석 자체를 포기한다. 운영 6.3.2 와 7.0.1 의 쓰기 스키마에는
/// <c>unit_id</c> 키가 <b>없고</b> 7.0 이후는 <c>additionalProperties:false</c> 라 실리는 순간 422 다.
/// 프로브 미확보 시 기본값이 <see cref="EnumServerContract.V6_3"/> 인 것도 같은 이유다(운영 무회귀 우선).</para>
/// </remarks>
public interface IUnitScopeService : IService
{
    /// <summary>
    /// 부대 편제 축을 쓸 수 있는 계약인가 — <c>Contract &gt;= V8_0</c>. <b><c>&gt;=</c> 비교만 쓴다</b>
    /// (동치 비교는 9.0 서버에서 기능을 통째로 죽인다).
    /// </summary>
    bool IsUnitEra { get; }

    /// <summary>설정에서 읽은 우리 부대 <b>코드</b>(<c>GroupNats</c>). 미설정이면 <c>null</c>.</summary>
    string? UnitCode { get; }

    /// <summary>
    /// 해석된 부대 id. <b>아직 해석하지 못했거나 8.0 미만이면 <c>null</c></b> —
    /// 호출부는 <c>null</c> 을 "실으면 안 됨"으로 읽는다.
    /// </summary>
    int? CurrentUnitId { get; }

    /// <summary>1회라도 성공적으로 해석했는가.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// 부대 id 를 해석한다(<b>멱등</b> — 이미 해석했으면 네트워크에 나가지 않는다).
    /// 실패해도 <b>예외를 던지지 않고</b> <c>null</c> 을 돌려주며 경고를 남긴다.
    /// </summary>
    Task<int?> ResolveAsync(CancellationToken token = default);
}

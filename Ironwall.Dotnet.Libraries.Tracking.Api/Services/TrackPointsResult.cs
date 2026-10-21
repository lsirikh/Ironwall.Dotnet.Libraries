using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

namespace Ironwall.Dotnet.Libraries.Tracking.Api.Services;
/****************************************************************************
   Purpose      : 추적 이력 조회 결과 종류 (G12 · G13)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 예외를 던지지 않는 계약은 그대로 두고, 실패를 '데이터 없음'과
                  가를 수 있게 결과 종류 · 부분 여부를 함께 돌려준다.
****************************************************************************/

/// <summary>추적점 조회 결과 종류.</summary>
public enum EnumTrackFetchOutcome
{
    /// <summary>구간 끝(next_cursor null)까지 다 읽음. 0건이면 진짜 '데이터 없음'.</summary>
    Success = 0,
    /// <summary>403 — <c>map:view</c> 권한 없음(AUTH_MODE=token, v6.3.17+).</summary>
    Forbidden,
    /// <summary>401 — 인증 실패(재로그인은 BearerAuthHandler 가 처리).</summary>
    Unauthorized,
    /// <summary>그 밖의 실패(5xx · 400 Invalid cursor · 통신/해석 오류 · 페이지 안전차단 · 취소).</summary>
    Failed,
}

/// <summary>
/// <see cref="ITrackingApiService.GetTrackPointsWithStatusAsync"/> 결과.
/// 실패여도 그때까지 읽은 점(<see cref="Points"/>)은 그대로 담는다.
/// </summary>
public sealed class TrackPointsResult
{
    /// <summary>읽은 추적점(실패 시 그 앞 페이지까지).</summary>
    public List<TrackPointDto> Points { get; init; } = new();

    /// <summary>결과 종류.</summary>
    public EnumTrackFetchOutcome Outcome { get; init; }

    /// <summary>마지막 HTTP 상태(통신 예외면 0).</summary>
    public int StatusCode { get; init; }

    /// <summary>성공적으로 읽은 페이지 수.</summary>
    public int PagesRead { get; init; }

    /// <summary>몇 쪽 읽은 뒤 실패했는가(앞 페이지 결과는 <see cref="Points"/> 에 남음).</summary>
    public bool IsPartial => Outcome != EnumTrackFetchOutcome.Success && PagesRead > 0;
}

/// <summary><c>GET /api/tracking/health</c> 판정.</summary>
public enum EnumTrackingHealth
{
    /// <summary>200 — 쓸 수 있음.</summary>
    Ok = 0,
    /// <summary>503 — 추적 저장소를 쓸 수 없음(본문은 봉투 아닌 평문 <c>{"status":"unavailable"}</c>).</summary>
    Unavailable,
    /// <summary>404 · 405 등 — 경로가 없는 옛 판. 막지 않는다.</summary>
    NotSupported,
    /// <summary>통신 실패 · 그 밖의 상태 — 판단 불가라 막지 않는다.</summary>
    Unknown,
}

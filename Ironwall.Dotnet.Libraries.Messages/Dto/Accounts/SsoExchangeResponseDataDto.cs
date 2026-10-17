using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// <c>POST /api/auth/sso-exchange</c> 응답의 <c>data</c> — SSO PRD FR-04.
/// <para>로그인 응답과 같은 모양에서 <b><c>refresh_token</c> 이 빠지고</b> <c>server_time</c> 이 더해진다.
/// 그래서 <see cref="LoginResponseDataDto"/> 를 상속해 토큰 저장·권한 평탄화 경로를 그대로 쓴다 —
/// <see cref="TokenDataDto.RefreshToken"/> 은 항상 빈 값으로 남는다.</para>
/// <para><b>갱신은 refresh 가 아니라 재교환</b>이다. 재교환하면 <see cref="TokenDataDto.SessionId"/> 가
/// <b>새 값</b>이 되고, 서버는 같은 <c>(계정, X-Client-Id)</c> 의 앞선 교환 세션을 끝낸다
/// (옛 access 는 <c>401 SESSION_REVOKED</c>).</para>
/// </summary>
public class SsoExchangeResponseDataDto : LoginResponseDataDto
{
    /// <summary>
    /// 서버 현재 시각(KST aware ISO-8601) — 클라·서버 시계 편차 보정용.
    /// 권한 <c>valid_until</c> 판정은 PC 시계가 아니라 이 값과 비교해야 한다.
    /// 원문 문자열로 둔다(표준시 해석을 호출부가 정하게).
    /// </summary>
    [JsonProperty("server_time")] public string? ServerTime { get; set; }
}

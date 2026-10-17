using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;

/// <summary>
/// SSO 교환 응답으로 로그인을 마무리한다 — SSO PRD FR-03.
/// <para>구현은 <see cref="ApiAccountGateway"/> 하나이고, 비밀번호 로그인과 <b>같은 마무리 코드</b>를 탄다
/// (토큰 → 권한 적용 → 강제 로그아웃 가드 재무장 → 로그인 게이팅 알림). SSO 조정자가 이 경계로 부른다 —
/// 조정자(<c>Ironwall.Dotnet.Libraries.Sso</c>)가 게이트웨이 구체형을 몰라도 되게.</para>
/// </summary>
public interface ISsoLoginCompleter
{
    AuthOutcome CompleteSsoLogin(SsoExchangeResponseDataDto data);
}

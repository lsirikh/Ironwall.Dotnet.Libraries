namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// access/refresh 토큰의 단일 보관소(SingleInstance). BearerAuthHandler 가 읽고, ApiAccountGateway 가 로그인/refresh 시 기록한다.
/// <para>응답에 expires_in 이 없으므로(§2.3.1) JWT exp 클레임을 디코드해 만료시각을 보관한다.</para>
/// <para>권한/역할은 IPermissionService 가 별도 보관(관심사 분리). 본 서비스는 토큰만 책임진다. — GOP-00 PRD FR-3</para>
/// <para>강제 로그아웃(FR-FL-01/05): jti·user_id·session_id 식별자 보관 + Clear() 마다 Generation 증가(refresh 부활 차단 가드).</para>
/// </summary>
public interface ITokenStorageService
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTime? AccessExpiresAtUtc { get; }
    DateTime? RefreshExpiresAtUtc { get; }
    bool IsAuthenticated { get; }

    /// <summary>현재 access 토큰의 jti 클레임 — 강제 로그아웃 세션 매칭 키(FR-FL-01). 미보유/비JWT 시 null.</summary>
    string? Jti { get; }
    /// <summary>토큰 소유자 user_id(sub 또는 user_id 클레임). 미보유 시 null.</summary>
    string? UserId { get; }
    /// <summary>세션 식별자 — 서버 제공(응답 또는 sid 클레임) 시 보관. 현재 서버 미제공이면 보통 null(seam, FR-FL-01).</summary>
    string? SessionId { get; }
    /// <summary>Clear() 마다 1 증가하는 세대 번호. 진행 중 refresh 가 Clear 이후 토큰을 되살리지 못하게 하는 가드(FR-FL-05).</summary>
    int Generation { get; }

    /// <summary>토큰 저장 + JWT exp/jti/user_id/sid 디코드. refreshToken 이 null 이면 access 만 갱신. sessionId 명시 시 우선 보관.</summary>
    void SetTokens(string accessToken, string? refreshToken = null, string? sessionId = null);

    /// <summary>generation 가드 SetTokens — <paramref name="expectedGeneration"/> 이 현재 Generation 과 같을 때만 적용한다.
    /// refresh 진행 중 Clear()(강제 로그아웃)가 끼어들면 false 반환·미적용(폐기 세션 부활 차단, FR-FL-05).</summary>
    bool SetTokensIfGeneration(int expectedGeneration, string accessToken, string? refreshToken = null, string? sessionId = null);

    /// <summary>access 가 threshold 이내 만료 예정인가. exp 미상(non-JWT)이면 false(선제 refresh 비대상).</summary>
    bool IsAccessTokenExpiring(TimeSpan threshold);

    /// <summary>로그아웃/만료 시 전체 제로화 + Generation 증가.</summary>
    void Clear();
}

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// access/refresh 토큰의 단일 보관소(SingleInstance). BearerAuthHandler 가 읽고, ApiAccountGateway 가 로그인/refresh 시 기록한다.
/// <para>응답에 expires_in 이 없으므로(§2.3.1) JWT exp 클레임을 디코드해 만료시각을 보관한다.</para>
/// <para>권한/역할은 IPermissionService 가 별도 보관(관심사 분리). 본 서비스는 토큰만 책임진다. — GOP-00 PRD FR-3</para>
/// </summary>
public interface ITokenStorageService
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTime? AccessExpiresAtUtc { get; }
    DateTime? RefreshExpiresAtUtc { get; }
    bool IsAuthenticated { get; }

    /// <summary>토큰 저장 + JWT exp 디코드. refreshToken 이 null 이면 access 만 갱신(refresh 회전 미수반 케이스).</summary>
    void SetTokens(string accessToken, string? refreshToken = null);

    /// <summary>access 가 threshold 이내 만료 예정인가. exp 미상(non-JWT)이면 false(선제 refresh 비대상).</summary>
    bool IsAccessTokenExpiring(TimeSpan threshold);

    /// <summary>로그아웃/만료 시 전체 제로화.</summary>
    void Clear();
}

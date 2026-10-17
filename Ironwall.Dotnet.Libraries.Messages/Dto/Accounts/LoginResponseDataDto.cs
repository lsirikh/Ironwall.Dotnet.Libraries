using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// POST /api/auth/login 의 data 본문 — 토큰 + 중첩 user.
/// <para>envelope 전체는 <c>ApiResponse&lt;LoginResponseDataDto&gt;</c>로 파싱한다(data.access_token + data.user). — GOP-00 PRD FR-1</para>
/// </summary>
public class LoginResponseDataDto : TokenDataDto
{
    /// <summary>로그인 사용자 + 그룹 권한(permissions는 이 경로에만 존재, A-4).</summary>
    [JsonProperty("user")] public AuthUserDto? User { get; set; }
}

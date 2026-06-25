using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// POST /api/users/{id}/reset-password 요청 본문 (§9.6, C-3 서버 확정).
/// 1필드(new_password), 서버측 비밀번호 정책 검증 없음(min1). — GOP-00 PRD FR-19
/// </summary>
public class ResetPasswordRequestDto
{
    [JsonProperty("new_password")] public string NewPassword { get; set; } = string.Empty;
}

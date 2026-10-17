using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// POST /api/users/{id}/reset-password 요청 본문 (§9.3, 관리자 강제 초기화).
/// 1필드(new_password) — 서버 제약 <b>minLength 8 · maxLength 100</b>(배포 <c>PasswordResetRequest</c>).
/// 종전 주석의 "min1" 은 옛 서버 기준으로 낡았다. — GOP-00 PRD FR-19
/// </summary>
public class ResetPasswordRequestDto
{
    [JsonProperty("new_password")] public string NewPassword { get; set; } = string.Empty;
}

using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// PUT /api/users/me/password 요청 본문 (§9.3, 본인 비밀번호 변경).
/// 서버 제약: current_password(minLength 1) · new_password(<b>minLength 8</b>, maxLength 100)
/// — 배포 <c>PasswordChangeRequest</c> 기준. 종전 주석의 "min6" 은 낡았다(UI 는 이미 8자 검증). — GOP-00 PRD FR-17
/// </summary>
public class PasswordChangeRequestDto
{
    [JsonProperty("current_password")] public string CurrentPassword { get; set; } = string.Empty;
    [JsonProperty("new_password")]     public string NewPassword { get; set; } = string.Empty;
}

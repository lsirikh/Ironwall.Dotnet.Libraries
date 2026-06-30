using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 세션/인증 정책 설정 DTO — GET/PUT /api/settings/session.
/// 편집 가능: 세션 만료시간·refresh TTL·로그인 잠금 임계·세션 사용여부.
/// 읽기 전용(배포/.env): 인증 모드·JWT 알고리즘 (서버 미반환/표시만).
/// — GOP_Session_Settings_Admin(클라) / PRD_GOP_Server_Session_Settings(서버). 서버 API 미배포 시 클라 graceful 처리.
/// </summary>
public class SessionSettingsDto
{
    /// <summary>액세스 토큰(세션) 만료시간(시간). 서버 JWT_EXPIRATION_HOURS. 편집 가능.</summary>
    [JsonProperty("session_timeout_hours")] public int? SessionTimeoutHours { get; set; }

    /// <summary>Refresh 토큰 유효기간(일). 서버 JWT_REFRESH_EXPIRATION_DAYS. 편집 가능.</summary>
    [JsonProperty("refresh_expiration_days")] public int? RefreshExpirationDays { get; set; }

    /// <summary>로그인 실패 잠금 임계(횟수, 0=비활성). 편집 가능.</summary>
    [JsonProperty("lockout_threshold")] public int? LockoutThreshold { get; set; }

    /// <summary>세션 만료 enforce 사용여부. 편집 가능.</summary>
    [JsonProperty("session_enabled")] public bool? SessionEnabled { get; set; }

    /// <summary>인증 모드(token/public). 읽기 전용 — 배포/.env 전용(UI 편집 금지).</summary>
    [JsonProperty("auth_mode")] public string? AuthMode { get; set; }

    /// <summary>JWT 서명 알고리즘. 읽기 전용.</summary>
    [JsonProperty("jwt_algorithm")] public string? JwtAlgorithm { get; set; }
}

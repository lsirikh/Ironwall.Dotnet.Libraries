using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 세션/인증 정책 설정 DTO — GET/PUT /api/settings/session.
/// 편집 가능: 세션 만료시간·refresh TTL·로그인 잠금 임계·세션 사용여부.
/// 읽기 전용(배포/.env): 인증 모드·JWT 알고리즘 (서버 미반환/표시만).
/// — GOP_Session_Settings_Admin(클라) / PRD_GOP_Server_Session_Settings(서버). 서버 API 미배포 시 클라 graceful 처리.
/// <para>⚠ <b>PUT 본문은 부분 업데이트</b>이고 서버(8.0, <c>extra="forbid"</c>)는 모르는 키를 422 <c>UNKNOWN_FIELD</c> 로 거부한다.
/// 읽기 전용 <c>auth_mode</c>·<c>jwt_algorithm</c> 은 PUT 모델에 <b>없는 키</b>라 값이 null 이어도 키가 실리면 저장 전체가 422 였다
/// (라이브 실측 2026-09-24: 값을 하나도 바꾸지 않은 [저장]이 매번 422). 그래서 모든 키를 <b>null 이면 싣지 않는다</b> —
/// GET 응답은 두 값을 늘 채워 주므로 역직렬화에는 영향이 없다.</para>
/// </summary>
public class SessionSettingsDto
{
    /// <summary>액세스 토큰(세션) 만료시간(시간). 서버 JWT_EXPIRATION_HOURS. 편집 가능.</summary>
    [JsonProperty("session_timeout_hours", NullValueHandling = NullValueHandling.Ignore)] public int? SessionTimeoutHours { get; set; }

    /// <summary>Refresh 토큰 유효기간(일). 서버 JWT_REFRESH_EXPIRATION_DAYS. 편집 가능.</summary>
    [JsonProperty("refresh_expiration_days", NullValueHandling = NullValueHandling.Ignore)] public int? RefreshExpirationDays { get; set; }

    /// <summary>로그인 실패 잠금 임계(횟수, 0=비활성). 편집 가능.</summary>
    [JsonProperty("lockout_threshold", NullValueHandling = NullValueHandling.Ignore)] public int? LockoutThreshold { get; set; }

    /// <summary>잠금 자동해제 시간(분, 0=자동해제 없음=영구). 경과 후 로그인 시 자동해제+카운트 리셋. 편집 가능(1~1440). v6.3 신규.</summary>
    [JsonProperty("lockout_duration_minutes", NullValueHandling = NullValueHandling.Ignore)] public int? LockoutDurationMinutes { get; set; }

    /// <summary>세션 만료 enforce 사용여부. 편집 가능.</summary>
    [JsonProperty("session_enabled", NullValueHandling = NullValueHandling.Ignore)] public bool? SessionEnabled { get; set; }

    /// <summary>인증 모드(token/public). 읽기 전용 — 배포/.env 전용(UI 편집 금지).</summary>
    [JsonProperty("auth_mode", NullValueHandling = NullValueHandling.Ignore)] public string? AuthMode { get; set; }

    /// <summary>JWT 서명 알고리즘. 읽기 전용.</summary>
    [JsonProperty("jwt_algorithm", NullValueHandling = NullValueHandling.Ignore)] public string? JwtAlgorithm { get; set; }

    // ── v6.3 동시성 5키 (nullable — 구버전 서버 폴백). GUIDE §2 ──
    /// <summary>동시 세션 정책: evict_all(단일) | allow(다중 공존). 편집 가능.</summary>
    [JsonProperty("session_concurrency_policy", NullValueHandling = NullValueHandling.Ignore)] public string? SessionConcurrencyPolicy { get; set; }

    /// <summary>allow일 때 계정당 최대 동시 세션 수(0=무제한, 0~100). 편집 가능.</summary>
    [JsonProperty("max_concurrent_sessions", NullValueHandling = NullValueHandling.Ignore)] public int? MaxConcurrentSessions { get; set; }

    /// <summary>같은 client_id 재로그인 시 그 클라의 옛 세션만 교체(고유 client_id 선행 필요). 편집 가능.</summary>
    [JsonProperty("session_self_replace_enabled", NullValueHandling = NullValueHandling.Ignore)] public bool? SessionSelfReplaceEnabled { get; set; }

    /// <summary>오래된 비활성 세션 이력 보존일(0=정리 안 함, 0~3650). 편집 가능.</summary>
    [JsonProperty("session_history_retention_days", NullValueHandling = NullValueHandling.Ignore)] public int? SessionHistoryRetentionDays { get; set; }

    /// <summary>(예약) 로그인 이상탐지 이벤트 발행. 서버 배선 후속. 편집 가능.</summary>
    [JsonProperty("login_anomaly_event_enabled", NullValueHandling = NullValueHandling.Ignore)] public bool? LoginAnomalyEventEnabled { get; set; }
}

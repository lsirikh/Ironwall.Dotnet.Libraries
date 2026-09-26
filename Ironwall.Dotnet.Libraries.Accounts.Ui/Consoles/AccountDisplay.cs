using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;

/// <summary>
/// 계정 콘솔의 <b>표시 사전</b> — 서버 코드 값(역할 · 상태 · 감사 동작/대상/결과 · 부여 상태)을 운영자가 읽는 한국어로 바꾼다.
/// 저장 값은 그대로 두고 화면에만 쓴다. 사전에 없는 값은 원문 대신 <see cref="Unknown"/> — 원문은 툴팁으로 보인다.
/// </summary>
/// <remarks>
/// 어휘의 출처(2026-09-27 확인): 루프백 시험 서버 OpenAPI 8.0.2 의 닫힌 집합
/// <c>EnumAuditActionTypeFilter</c>(23) · <c>EnumAuditResourceType</c>(4) · <c>EnumAuditStatus</c>(2) ·
/// <c>GET /api/grants?status=</c>(ACTIVE/PENDING/EXPIRED/REVOKED), 그리고 같은 서버의 실제 감사 기록 621건.
/// </remarks>
public static class AccountDisplay
{
    /// <summary>사전에 없는 값 — 원문은 화면에 찍지 않고 툴팁으로만 보인다.</summary>
    public const string Unknown = "알 수 없음";

    #region - 역할 · 상태 -
    public static string Role(EnumUserRole role) => role switch
    {
        EnumUserRole.ADMIN => "관리자",
        EnumUserRole.USER => "사용자",
        EnumUserRole.MAINTAINER => "정비자(이전 등급)",
        EnumUserRole.OPERATOR => "운영자(이전 등급)",
        EnumUserRole.VIEWER => "조회자(이전 등급)",
        EnumUserRole.GUEST => "손님(이전 등급)",
        _ => Unknown,
    };

    /// <summary>서버 문자열 역할("ADMIN" · "USER" · 옛 감사 행의 "OPERATOR" 등).</summary>
    public static string Role(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return Enum.TryParse<EnumUserRole>(raw.Trim(), ignoreCase: true, out var role) && Enum.IsDefined(role)
            ? Role(role)
            : Unknown;
    }

    /// <summary>표시 글(또는 서버 이름) → 역할. 콘솔의 [구분] 콤보가 쓴다.</summary>
    public static bool TryParseRole(string? text, out EnumUserRole role)
    {
        role = EnumUserRole.UNDEFINED;
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var candidate in Enum.GetValues<EnumUserRole>())
        {
            if (candidate == EnumUserRole.UNDEFINED) continue;
            if (string.Equals(Role(candidate), text, StringComparison.Ordinal)) { role = candidate; return true; }
        }
        return Enum.TryParse(text.Trim(), ignoreCase: false, out role) && role != EnumUserRole.UNDEFINED && Enum.IsDefined(role);
    }

    public static string Used(EnumUsedType used) => used == EnumUsedType.USED ? "사용" : "미사용";
    #endregion

    #region - 감사 로그 -
    /// <summary>감사 동작 23종(서버 <c>EnumAuditActionTypeFilter</c>).</summary>
    public static IReadOnlyDictionary<string, string> AuditActions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["USER_CREATED"] = "사용자 등록",
        ["USER_UPDATED"] = "사용자 정보 변경",
        ["USER_DELETED"] = "사용자 삭제",
        ["USER_LOCKED"] = "계정 잠금",
        ["USER_UNLOCKED"] = "잠금 해제",
        ["USER_ACTIVATED"] = "사용으로 전환",
        ["USER_DEACTIVATED"] = "미사용으로 전환",
        ["USER_PHOTO_CHANGED"] = "사진 변경",
        ["USER_PHOTO_DELETED"] = "사진 삭제",
        ["PASSWORD_CHANGED"] = "비밀번호 변경",
        ["PASSWORD_RESET"] = "비밀번호 초기화",
        ["ROLE_CHANGED"] = "구분 변경",
        ["GROUP_ASSIGNED"] = "권한 그룹 배정",
        ["GROUP_CREATED"] = "권한 그룹 만듦",
        ["GROUP_UPDATED"] = "권한 그룹 수정",
        ["GROUP_DELETED"] = "권한 그룹 삭제",
        ["PERMISSION_CHANGED"] = "권한 변경",
        ["GRANT_CREATED"] = "한시 부여",
        ["GRANT_REVOKED"] = "한시 부여 회수",
        ["GRANT_EXPIRED"] = "한시 부여 만료",
        ["SESSION_CREATED"] = "로그인",
        ["SESSION_TERMINATED"] = "로그아웃",
        ["SESSION_FORCED_LOGOUT"] = "강제 로그아웃",
    };

    /// <summary>감사 대상 4종(서버 <c>EnumAuditResourceType</c>).</summary>
    public static IReadOnlyDictionary<string, string> AuditResources { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["USER"] = "사용자",
        ["USER_GROUP"] = "권한 그룹",
        ["USER_SESSION"] = "세션",
        ["PASSWORD"] = "비밀번호",
    };

    /// <summary>감사 결과 2종(서버 <c>EnumAuditStatus</c>).</summary>
    public static IReadOnlyDictionary<string, string> AuditStatuses { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SUCCESS"] = "성공",
        ["FAILURE"] = "실패",
    };

    /// <summary>한시 부여 상태 4종(서버 <c>GET /api/grants?status=</c>).</summary>
    public static IReadOnlyDictionary<string, string> GrantStatuses { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ACTIVE"] = "유효",
        ["PENDING"] = "시작 전",
        ["EXPIRED"] = "만료",
        ["REVOKED"] = "회수",
    };

    /// <summary>만료 임박으로 강조하는 창(A-34 · 목업 L1183).</summary>
    public static readonly TimeSpan ExpiringWindow = TimeSpan.FromHours(24);

    /// <summary>유효한 부여가 <see cref="ExpiringWindow"/> 안에 끝나는가. 상시(종료 없음)는 아니다.</summary>
    public static bool IsExpiringSoon(string? status, DateTime? validUntil, DateTime now)
        => string.Equals(status, "ACTIVE", StringComparison.Ordinal)
           && validUntil is { } until && until > now && until - now <= ExpiringWindow;

    public static string AuditAction(string? raw) => Lookup(AuditActions, raw);
    public static string AuditResource(string? raw) => Lookup(AuditResources, raw);
    public static string AuditStatus(string? raw) => Lookup(AuditStatuses, raw);
    public static string GrantStatus(string? raw) => Lookup(GrantStatuses, raw);

    /// <summary>닫힌 사전에서 찾는다 — 비었으면 빈 글, 모르면 <see cref="Unknown"/>.</summary>
    public static string Lookup(IReadOnlyDictionary<string, string> dictionary, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return dictionary.TryGetValue(raw.Trim(), out var text) ? text : Unknown;
    }

    /// <summary>사전에 없는 값이면 원문(툴팁용), 아니면 빈 글.</summary>
    public static string? RawIfUnknown(IReadOnlyDictionary<string, string> dictionary, string? raw)
        => string.IsNullOrWhiteSpace(raw) || dictionary.ContainsKey(raw.Trim()) ? null : raw;
    #endregion

    #region - 시각 -
    /// <summary>
    /// 서버 시각(ISO 8601, 오프셋 포함) → 이 PC 시각 "yyyy-MM-dd HH:mm:ss". 읽을 수 없으면 원문, 비었으면 빈 글.
    /// </summary>
    public static string Time(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var at)
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : raw;
    }
    #endregion

    #region - 감사 기록의 변경 전후 -
    /// <summary>변경 전후 표에 나오는 서버 필드 이름 → 화면 이름(사용자 콘솔의 표기와 같다).</summary>
    public static IReadOnlyDictionary<string, string> ChangeFields { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["login_id"] = "아이디",
        ["username"] = "아이디",
        ["name"] = "성명",
        ["employee_number"] = "사번",
        ["phone"] = "전화",
        ["email"] = "이메일",
        ["position"] = "직급",
        ["department"] = "부서",
        ["role"] = "구분",
        ["group_id"] = "권한 그룹",
        ["group_name"] = "권한 그룹",
        ["is_active"] = "상태",
        ["is_locked"] = "잠금",
        ["lock_reason"] = "잠금 사유",
        ["photo_url"] = "사진",
        ["description"] = "설명",
        ["valid_from"] = "유효 시작",
        ["valid_until"] = "유효 종료",
        ["status"] = "상태",
        ["revoked_at"] = "회수 시각",
        ["password"] = "비밀번호",
    };

    private static readonly IReadOnlyDictionary<string, string> VerbNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["view"] = "조회",
        ["edit"] = "편집",
        ["delete"] = "삭제",
        ["control"] = "제어",
    };

    /// <summary>
    /// 감사 기록의 <c>changes</c> → "항목 / 전 / 후" 행. 서버 모양은 <c>{"before": {...}, "after": {...}}</c> 다
    /// (실기록 확인). 중첩(권한 모듈 × 동작)은 펼쳐 <b>값이 바뀐 칸만</b> 싣는다 — 16모듈 × 4동작을 다 늘어놓지 않는다.
    /// 모양이 다르면(전후 쌍이 없는 평면 객체) 값을 "후" 칸에만 싣는다.
    /// </summary>
    public static IReadOnlyList<AuditChangeRow> Changes(JObject? changes)
    {
        var rows = new List<AuditChangeRow>();
        if (changes is null || !changes.HasValues) return rows;

        var hasPair = changes["before"] is JObject || changes["after"] is JObject;
        var before = new Dictionary<string, JToken?>(StringComparer.Ordinal);
        var after = new Dictionary<string, JToken?>(StringComparer.Ordinal);
        if (hasPair)
        {
            Flatten(changes["before"] as JObject, string.Empty, before);
            Flatten(changes["after"] as JObject, string.Empty, after);
        }
        else
        {
            Flatten(changes, string.Empty, after);
        }

        foreach (var path in before.Keys.Union(after.Keys).OrderBy(Order).ThenBy(p => p, StringComparer.Ordinal))
        {
            before.TryGetValue(path, out var b);
            after.TryGetValue(path, out var a);
            if (hasPair && before.ContainsKey(path) && after.ContainsKey(path) && JToken.DeepEquals(b, a)) continue;
            // 펼친 권한 칸은 "둘 다 꺼짐" 이 대부분이다 — 한쪽에만 있는 꺼진 칸은 바뀐 것이 아니다.
            if (hasPair && IsPermissionPath(path) && IsFalse(b) && IsFalse(a)) continue;
            rows.Add(new AuditChangeRow(FieldName(path), Value(path, b, hasPair && before.ContainsKey(path)),
                                        Value(path, a, after.ContainsKey(path)), path));
        }
        return rows;
    }

    /// <summary>필드 경로 → 화면 이름. 권한 칸은 "장비 · 편집", 모르면 <see cref="Unknown"/>.</summary>
    public static string FieldName(string path)
    {
        if (ChangeFields.TryGetValue(path, out var name)) return name;

        // permissions.modules.{module}.{verb}
        var parts = path.Split('.');
        if (parts.Length == 4 && parts[0] == "permissions" && parts[1] == "modules")
        {
            var module = PermissionCatalog.TryFromServerKey(parts[2], out var m) ? AccountModuleNames.Of(m) : Unknown;
            var verb = VerbNames.TryGetValue(parts[3], out var v) ? v : Unknown;
            return $"{module} · {verb}";
        }
        if (parts.Length >= 2 && parts[0] == "permissions" && parts[1] == "device_groups") return "장비 그룹 범위";
        return Unknown;
    }

    private static string Value(string path, JToken? token, bool present)
    {
        if (!present) return "—";
        if (token is null || token.Type == JTokenType.Null) return "(없음)";
        if (path == "password") return "(가림)";
        if (path == "role") return Role(token.ToString());
        if (path is "is_active") return token.Type == JTokenType.Boolean && (bool)token ? "사용" : "미사용";
        if (path is "is_locked") return token.Type == JTokenType.Boolean && (bool)token ? "잠김" : "정상";
        if (path is "valid_from" or "valid_until" or "revoked_at") return Time(token.ToString());
        if (token.Type == JTokenType.Boolean) return (bool)token ? "켜짐" : "꺼짐";
        if (token is JArray array) return array.Count == 0 ? "(없음)" : string.Join(", ", array.Select(x => x.ToString()));
        var text = token.ToString();
        return string.IsNullOrEmpty(text) ? "(없음)" : text;
    }

    private static void Flatten(JObject? source, string prefix, IDictionary<string, JToken?> into)
    {
        if (source is null) return;
        foreach (var property in source.Properties())
        {
            var path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            if (property.Value is JObject child && child.HasValues) Flatten(child, path, into);
            else into[path] = property.Value;
        }
    }

    private static bool IsPermissionPath(string path) => path.StartsWith("permissions.modules.", StringComparison.Ordinal);
    private static bool IsFalse(JToken? token) => token is null || token.Type == JTokenType.Null || (token.Type == JTokenType.Boolean && !(bool)token);
    private static int Order(string path) => IsPermissionPath(path) ? 1 : 0;
    #endregion
}

/// <summary>감사 기록 상세의 변경 전후 한 줄.</summary>
/// <param name="Field">화면 이름("부서", "장비 · 편집").</param>
/// <param name="Before">바뀌기 전 값("—" = 그 쪽에 없음).</param>
/// <param name="After">바뀐 뒤 값.</param>
/// <param name="RawPath">서버 필드 경로(툴팁).</param>
public sealed record AuditChangeRow(string Field, string Before, string After, string RawPath);

/// <summary>
/// 권한 모듈 표시명 — 사전(<see cref="PermissionCatalog.DisplayName(EnumPermissionModule)"/>)의 띄어쓰기를
/// 콘솔 레일 표기("감사 로그")에 맞춘다. 사전 자체는 다른 화면도 쓰므로 여기서만 고친다.
/// </summary>
public static class AccountModuleNames
{
    public static string Of(EnumPermissionModule module) => module switch
    {
        EnumPermissionModule.AuditLogs => "감사 로그",
        EnumPermissionModule.UserGroups => "그룹 · 권한",
        _ => PermissionCatalog.DisplayName(module),
    };
}

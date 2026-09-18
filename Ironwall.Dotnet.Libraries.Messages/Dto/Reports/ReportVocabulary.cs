using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Reports;

/// <summary>
/// 보고서 도메인 <b>닫힌 어휘</b> 모음 — 서버가 422 로 거부하는 값을 <b>보내기 전에</b> 걸러내는 단일 지점.
/// <para><b>왜 코드에 박아 두는가</b>: 서버 v8.0(배포 8.0.1 실측 2026-09-18)에서
/// <c>GET /api/reports/generations?status=</c> 의 <c>status</c> 가 <b>닫힌 어휘 5종</b>이 되었다.
/// 종전(6.3.2·7.0.x)에는 빈 값·소문자·오탈자가 모두 <b>200 + 빈 목록</b>으로 삼켜졌지만
/// 이제 셋 다 <b>422 <c>VALUE_NOT_ALLOWED</c></b> 다(실측:
/// <c>?status=</c> · <c>?status=completed</c> · <c>?status=BOGUS</c> 전부 422).
/// 문자열을 그대로 흘려보내는 호출부가 하나라도 있으면 그 화면은 <b>서버 업그레이드 순간</b> 빈 목록이 아니라 오류가 된다.</para>
/// <para>여기 값은 <b>서버 enum 과 1:1</b> 이다(운영 6.3.2 / 개발 8.0.1 양쪽 openapi 실측 일치) —
/// 따라서 정규화·검증은 <b>버전 분기 없이</b> 양쪽에 안전하다.</para>
/// </summary>
public static class ReportGenerationStatus
{
    public const string Pending = "PENDING";
    public const string Generating = "GENERATING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";

    /// <summary>서버 <c>EnumReportStatus</c> 전체(5종). 순서는 생성 수명주기 순.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Pending, Generating, Completed, Failed, Cancelled };

    /// <summary>어휘에 속하는가(대문자 정확 일치).</summary>
    public static bool IsValid(string? value)
        => value != null && All.Contains(value, StringComparer.Ordinal);

    /// <summary>
    /// 정규화 — 공백을 다듬고 <b>대문자로</b> 올린다. 어휘 밖이거나 비었으면 <c>null</c>(= "필터 없음").
    /// <para>⚠ 서버는 <b>대소문자를 관용하지 않는다</b>(<c>completed</c> 는 422). 그래서 여기서 올려 보낸다.
    /// 빈 문자열도 "필터 없음"이 아니라 <b>422</b> 다 — 파라미터 자체를 붙이지 않아야 한다(§10.4.3).</para>
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var upper = value.Trim().ToUpperInvariant();
        return IsValid(upper) ? upper : null;
    }
}

/// <summary>
/// 시스템 이벤트 심각도(<c>POST /api/reports/generate</c> 의 <c>severity_filter</c>) — 닫힌 어휘 4종.
/// <para>운영 6.3.2 는 <c>array[string]</c> 로 느슨하게 받고 개발 8.0.1 은 <c>EnumSystemEventSeverity</c> 로 받는다
/// (양쪽 openapi 실측). 즉 <b>대문자 4종만 보내면 양쪽 모두 안전</b>하다.
/// 필터는 <b>시스템 이벤트 계열</b> 집계·그리드·CSV 에만 걸린다(탐지/장애/조치는 대상 아님).</para>
/// </summary>
public static class ReportSeverity
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";
    public const string Critical = "CRITICAL";

    public static IReadOnlyList<string> All { get; } = new[] { Info, Warning, Error, Critical };

    public static bool IsValid(string? value)
        => value != null && All.Contains(value, StringComparer.Ordinal);

    /// <summary>어휘 밖 값을 버리고 중복을 제거한 목록. 남는 게 없으면 <c>null</c>(= 전 심각도).</summary>
    public static List<string>? Sanitize(IEnumerable<string>? values)
    {
        if (values is null) return null;
        var list = new List<string>();
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            var upper = v.Trim().ToUpperInvariant();
            if (IsValid(upper) && !list.Contains(upper, StringComparer.Ordinal)) list.Add(upper);
        }
        return list.Count == 0 ? null : list;
    }
}

/// <summary>
/// 상세 CSV 유형(<c>GET /api/reports/generations/{id}/detail.csv?type=</c>) — 닫힌 어휘 8종.
/// <para>어휘 밖 값은 <b>400 <c>BAD_REQUEST</c></b> 이고(실측 8.0.1:
/// <c>Unknown type 'bogus'. Valid: ['action','audit','config','detection','login','malfunction','session','system']</c>),
/// 파라미터를 <b>아예 빼면</b> 422 <c>MISSING_FIELD</c>(<c>query.type</c>)다. 두 사유가 다르므로 문구도 달라야 한다.</para>
/// </summary>
public static class ReportDetailCsvType
{
    public const string Detection = "detection";
    public const string Malfunction = "malfunction";
    public const string Action = "action";
    public const string System = "system";
    public const string Config = "config";
    public const string Audit = "audit";
    public const string Login = "login";
    public const string Session = "session";

    /// <summary>서버가 열거하는 8종(소문자).</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        Detection, Malfunction, Action, System, Config, Audit, Login, Session
    };

    public static bool IsValid(string? value)
        => value != null && All.Contains(value, StringComparer.Ordinal);

    /// <summary>정규화 — 소문자로 내린다. 어휘 밖이면 <c>null</c>.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var lower = value.Trim().ToLowerInvariant();
        return IsValid(lower) ? lower : null;
    }
}

/// <summary>
/// 보고서 오류 봉투의 <b>안정 sub-code</b>(<c>error.details.error_code</c>) 상수.
/// <para>HTTP 상태코드만 보면 "레코드 없음"과 "파일만 사라짐"을 구분할 수 없다.
/// 읽는 수단은 <c>ApiError.DetailsErrorCode</c>(details → message 순으로 양 판본 수용)다.</para>
/// </summary>
public static class ReportErrorCode
{
    /// <summary>
    /// 레코드는 있으나 PDF 파일이 저장소에서 사라짐 → <b>재생성</b>이 유일한 해결. HTTP 410 <c>GONE</c>.
    /// v7.0 은 <c>error.message</c> 객체 안에, v8.0 은 <c>error.details</c> 안에 실린다(§10.4.4).
    /// </summary>
    public const string PdfFileMissing = "PDF_FILE_MISSING";
}

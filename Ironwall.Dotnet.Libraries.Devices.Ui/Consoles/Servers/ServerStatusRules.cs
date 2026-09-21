using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 상태 판정 — REST 로 생존을 판정하지 않는다 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>서버 한 대의 상태 — <b>관측된 것</b>만 상태다.</summary>
public enum ServerStatusKind
{
    /// <summary>한 번도 보고가 없다. 정상도 고장도 아니다(스토리보드 L1358).</summary>
    NotReported,
    Normal,
    Warning,
    Error,
}

/// <summary>상태·시각 판정의 순수 함수(스토리보드 L1357-1360).</summary>
/// <remarks>
/// <para><b>REST 로 생존을 판정하지 않는다</b>(L1357). 이 클래스에는 경과 시간으로 상태를 바꾸는 분기가
/// <b>하나도 없다</b> — 어떤 임계로도 "죽었다" 를 만들어 내지 않는다.</para>
///
/// <para><b>"보고 없음" 의 근거는 판본마다 다르다</b>(읽기 전용 서버 소스 실측):</para>
/// <list type="bullet">
/// <item><b>7.0+</b> — <c>status_observed_at</c> 이 정본이다. <c>app/schemas/server.py:788</c> 가
///   그 키를 "null = 보고 없음" 으로 선언하고, <c>app/models/server.py:105-106</c> 은
///   <c>status</c> 기본값이 <c>UNKNOWN</c> · <c>status_observed_at</c> 이 nullable 임을 보인다.
///   같은 값 재보고는 이 시각을 <b>올리지 않는다</b>(<c>server.py:479-482</c>) — 그래서 이것이
///   <b>마지막 전이</b>의 시각이고 "마지막 변화" 칸의 정본이다.</item>
/// <item><b>6.3</b> — 전이 시각이 <b>없다</b>. 판정은 <c>status</c> 키의 <b>존재</b>로만 한다
///   (<see cref="ServerAxisView.HasStatusKey"/>). 값이 있으면 그 값을 믿고, 없으면 보고 없음이다.
///   <c>updated_at</c> 을 전이로 읽지 않는다 — 이름만 바꿔도 올라가는 값이다.</item>
/// </list>
/// </remarks>
public static class ServerStatusRules
{
    public const string NotReportedText = "보고 없음";

    /// <summary>6.3 에는 전이 시각이 없다 — 없는 사실을 지어내지 않고 이렇게 적는다.</summary>
    public const string NoTransitionClockText = "—";

    /// <summary>6.3 에서 "마지막 변화" 칸에 붙는 설명.</summary>
    public const string NoTransitionClockNote =
        "이 서버 판본(6.3)에는 상태 전이 시각이 없습니다 — 마지막 변화를 알 수 없습니다. "
        + "아래 '마지막 수정' 은 이름·접속을 고친 시각이지 상태가 바뀐 시각이 아닙니다.";

    /// <summary>등록 직후 안내(L1360) — 목록 칸이 아니라 상태 줄에 쓴다.</summary>
    public const string JustRegisteredNotice = "등록했습니다 — 서버가 보고하기 전까지 상태는 미확인입니다";

    /// <summary>
    /// 한 행의 상태. <b>키의 부재</b>까지 보고 판정한다.
    /// </summary>
    public static ServerStatusKind Classify(ServerAxisView view, EnumServerContract contract)
    {
        if (view is null) throw new ArgumentNullException(nameof(view));

        // 7.0+ — 전이 시각이 비어 있으면 그것이 곧 "아직 아무도 보고하지 않았다" 다.
        if (contract >= EnumServerContract.V7_0)
            return ParseTime(view.StatusObservedAt) is null
                ? ServerStatusKind.NotReported
                : FromVocabulary(view.Status);

        // 6.3 — 전이 시각이 없다. 키가 안 왔으면 보고 없음, 왔으면 그 값을 믿는다.
        return view.HasStatusKey ? FromVocabulary(view.Status) : ServerStatusKind.NotReported;
    }

    /// <summary>상태 어휘 → 종류. 모르는 값·<c>UNKNOWN</c>·빈 값은 <b>정상으로 읽지 않는다</b>.</summary>
    public static ServerStatusKind FromVocabulary(string? status) => (status ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "ERROR" or "CRITICAL" or "FAULT" => ServerStatusKind.Error,
        "WARNING" or "WARN" => ServerStatusKind.Warning,
        "NORMAL" or "OK" or "ACTIVATED" => ServerStatusKind.Normal,
        _ => ServerStatusKind.NotReported,
    };

    /// <summary>목록 "상태" 칸의 글자.</summary>
    public static string StatusText(ServerStatusKind kind) => kind switch
    {
        ServerStatusKind.Error => "장애",
        ServerStatusKind.Warning => "경고",
        ServerStatusKind.Normal => "정상",
        _ => NotReportedText,
    };

    /// <summary>
    /// 상태 칸의 글리프 — <b>색이 아니라 형태</b>로 넷을 가른다.
    /// 장애 ▲ · 경고 ◆ · 정상 ● · 보고 없음 ○.
    /// </summary>
    public static string StatusGlyph(ServerStatusKind kind) => kind switch
    {
        ServerStatusKind.Error => "▲",
        ServerStatusKind.Warning => "◆",
        ServerStatusKind.Normal => "●",
        _ => "○",
    };

    /// <summary>레일 배지의 <c>▲</c> 에 세는가 — 장애만 센다(경고는 아니다).</summary>
    public static bool IsFault(ServerStatusKind kind) => kind == ServerStatusKind.Error;

    /// <summary>
    /// "마지막 변화" 칸의 글자 — <b>마지막 상태 전이</b>다.
    /// 6.3 은 그 시각이 없으므로 <see cref="NoTransitionClockText"/> 를 낸다(수정 시각으로 대신하지 않는다).
    /// </summary>
    public static string LastChangeText(ServerAxisView view, EnumServerContract contract, IClock clock)
    {
        if (view is null) throw new ArgumentNullException(nameof(view));
        if (clock is null) throw new ArgumentNullException(nameof(clock));

        if (contract < EnumServerContract.V7_0) return NoTransitionClockText;
        var at = ParseTime(view.StatusObservedAt);
        return at is null ? NotReportedText : Elapsed(at.Value, clock);
    }

    /// <summary>"마지막 수정"(<c>updated_at</c>) — 상태와 무관한 사실이라 따로 이름을 붙여 보인다.</summary>
    public static string LastEditText(ServerAxisView view, IClock clock)
    {
        if (view is null) throw new ArgumentNullException(nameof(view));
        if (clock is null) throw new ArgumentNullException(nameof(clock));

        var at = ParseTime(view.UpdatedAt) ?? ParseTime(view.CreatedAt);
        return at is null ? NotReportedText : Elapsed(at.Value, clock);
    }

    /// <summary>경과 시간 글자. <b>생존 판정이 아니다</b> — 오래됐다고 뜻이 바뀌지 않는다.</summary>
    public static string Elapsed(DateTimeOffset at, IClock clock)
    {
        if (clock is null) throw new ArgumentNullException(nameof(clock));

        var elapsed = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)) - at;
        if (elapsed < TimeSpan.Zero) return Absolute(at);

        if (elapsed < TimeSpan.FromMinutes(1)) return "방금";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes}분 전";
        if (elapsed < TimeSpan.FromDays(1)) return $"{(int)elapsed.TotalHours}시간 전";
        if (elapsed < TimeSpan.FromDays(7)) return $"{(int)elapsed.TotalDays}일 전";
        return Absolute(at);
    }

    /// <summary>
    /// 서버가 준 ISO8601 문자열 → 시각. 오프셋이 없으면 <b>한국 시간</b>으로 읽는다.
    /// 못 읽으면 <c>null</c>.
    /// </summary>
    public static DateTimeOffset? ParseTime(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return null;
        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var withOffset)
            && iso.IndexOfAny(OffsetMarks) > 10)
            return withOffset;

        return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var naive)
            ? new DateTimeOffset(naive, KoreaOffset)
            : null;
    }

    private static string Absolute(DateTimeOffset at)
        => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static readonly char[] OffsetMarks = { 'Z', 'z', '+' };
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);
}

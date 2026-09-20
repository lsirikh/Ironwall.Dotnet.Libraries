using Ironwall.Dotnet.Libraries.Base.Services;
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

/// <summary>
/// 상태·시각 판정의 순수 함수(스토리보드 L1357-1360).
/// </summary>
/// <remarks>
/// <para><b>REST 로 생존을 판정하지 않는다</b>(L1357). 응답의 시각은 <b>마지막 전이</b>이지 마지막 수신이 아니다 —
/// 60초마다 성실히 보고하는 서버와 어제 죽은 서버가 응답에서 똑같이 보인다. 그래서 라벨은 "마지막 변화" 이고,
/// 이 클래스는 <b>어떤 임계로도 "죽었다" 를 만들어 내지 않는다</b>(경과 시간으로 상태를 바꾸는 분기가 없다).</para>
/// <para><b>K-1(계약 한계)</b> — <c>ServerDto.Status</c> 는 기본값이 <c>"NORMAL"</c> 이라
/// 서버가 <c>status</c> 키를 <b>안 보낸 것</b>과 <c>NORMAL</c> 을 보낸 것이 구분되지 않는다.
/// 그래서 "보고 없음" 은 ① 상태 문자열이 비었거나 <c>UNKNOWN</c> 이거나 ② 시각이 하나도 없을 때로 판정한다.
/// 완전한 해소는 DTO(범위 밖)나 서버 응답이 바뀌어야 한다.</para>
/// </remarks>
public static class ServerStatusRules
{
    public const string NotReportedText = "보고 없음";
    /// <summary>등록 직후 안내 문구(L1360) — 목록 칸이 아니라 상태 줄에 쓴다.</summary>
    public const string JustRegisteredNotice = "등록했습니다 — 서버가 보고하기 전까지 상태는 미확인입니다";

    /// <summary>상태 문자열 + 시각 → 상태 종류. 던지지 않는다.</summary>
    public static ServerStatusKind Classify(string? status, DateTimeOffset? lastChangeAt)
    {
        var text = (status ?? string.Empty).Trim();
        if (text.Length == 0 || text.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)) return ServerStatusKind.NotReported;
        if (lastChangeAt is null) return ServerStatusKind.NotReported;      // 전이 시각이 없다 = 전이를 본 적이 없다

        return text.ToUpperInvariant() switch
        {
            "ERROR" or "CRITICAL" or "FAULT" => ServerStatusKind.Error,
            "WARNING" or "WARN" => ServerStatusKind.Warning,
            "NORMAL" or "OK" or "ACTIVATED" => ServerStatusKind.Normal,
            _ => ServerStatusKind.NotReported,                              // 모르는 어휘를 정상으로 읽지 않는다
        };
    }

    /// <summary>목록 "상태" 칸의 글자.</summary>
    public static string StatusText(ServerStatusKind kind) => kind switch
    {
        ServerStatusKind.Error => "장애",
        ServerStatusKind.Warning => "경고",
        ServerStatusKind.Normal => "정상",
        _ => NotReportedText,
    };

    /// <summary>레일 배지의 <c>▲</c> 에 세는가 — 장애만 센다(경고는 아니다).</summary>
    public static bool IsFault(ServerStatusKind kind) => kind == ServerStatusKind.Error;

    /// <summary>
    /// 목록 "마지막 변화" 칸의 글자. <b>생존 판정이 아니다</b> — 오래됐다고 색이나 뜻이 바뀌지 않는다.
    /// </summary>
    public static string LastChangeText(DateTimeOffset? lastChangeAt, IClock clock)
    {
        if (clock is null) throw new ArgumentNullException(nameof(clock));
        if (lastChangeAt is null) return NotReportedText;

        var elapsed = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)) - lastChangeAt.Value;
        if (elapsed < TimeSpan.Zero) return lastChangeAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        if (elapsed < TimeSpan.FromMinutes(1)) return "방금";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes}분 전";
        if (elapsed < TimeSpan.FromDays(1)) return $"{(int)elapsed.TotalHours}시간 전";
        if (elapsed < TimeSpan.FromDays(7)) return $"{(int)elapsed.TotalDays}일 전";
        return lastChangeAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 서버가 준 ISO8601 문자열 → 시각. 오프셋이 없으면 <b>한국 시간</b>으로 읽는다(서버가 KST 로 내려 준 전례).
    /// 못 읽으면 <c>null</c> — 그러면 "보고 없음" 이 된다.
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

    /// <summary>
    /// 그 행의 "마지막 변화" 시각 — <c>updated_at</c> 우선, 없으면 <c>created_at</c>.
    /// 둘 다 없으면 <c>null</c>(= 보고 없음).
    /// </summary>
    public static DateTimeOffset? LastChangeOf(string? updatedAt, string? createdAt)
        => ParseTime(updatedAt) ?? ParseTime(createdAt);

    private static readonly char[] OffsetMarks = { 'Z', 'z', '+' };
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);
}

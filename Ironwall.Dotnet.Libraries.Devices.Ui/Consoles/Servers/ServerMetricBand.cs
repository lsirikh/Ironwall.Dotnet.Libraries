using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 지표 띠 · 지표 이력 매핑 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>지표 띠 한 칸.</summary>
/// <param name="Key">자동화 식별자 꼬리(<c>Servers.Console.Metric.{Key}</c>) — <c>cpu</c>·<c>ram</c>·<c>disk</c>·<c>network</c>.</param>
/// <param name="Label">사람이 읽는 이름.</param>
/// <param name="ValueText">주 값. 값이 없으면 <see cref="ServerStatusRules.NotReportedText"/>.</param>
/// <param name="DetailText">부가 값("12.4 / 32.0 GB"). 없으면 빈 글자.</param>
/// <param name="Ratio">막대 비율 0~1. 비율이 뜻을 갖지 않는 칸(네트워크)은 <c>null</c>.</param>
/// <param name="BadgeText">임계 배지. <b>서버가 판정해 보낸 것만</b> 채워진다 — 없으면 <c>null</c>.</param>
/// <param name="IsCritical">배지 심각도가 critical 계열인가(형태를 바꾸는 데 쓴다).</param>
/// <param name="HasValue">서버가 이 지표를 보냈는가.</param>
public sealed record ServerMetricCell(
    string Key, string Label, string ValueText, string DetailText, double? Ratio,
    string? BadgeText, bool IsCritical, bool HasValue);

/// <summary>지표 이력 한 줄 — <b>임계 배지가 없다</b>(스토리보드 L1359).</summary>
public sealed record ServerMetricHistoryRow(string TimeText, string Cpu, string Ram, string Disk, string Network);

/// <summary>
/// 지표 매핑의 순수 함수(스토리보드 L1345-1346 · L1359).
/// </summary>
/// <remarks>
/// <para><b>임계는 우리가 판정하지 않는다.</b> 배지는 서버가 보낸 <c>threshold_exceeded</c> 항목에서만 나온다.
/// 6.3 은 이 값을 dict 로 보내 <see cref="ServerMetricDto.ThresholdExceededItems"/> 가 <b>빈 목록</b>이므로
/// 그 판본에서는 배지가 아예 뜨지 않는다 — 그것이 옳다(우리 임계와 서버 임계가 갈리면 화면이 거짓말을 한다).</para>
/// <para><b>이력은 재판정하지 않는다</b>(L1359). <see cref="History"/> 는 배지 자리를 아예 만들지 않는다 —
/// 과거 행의 임계 배열은 언제나 비어 있어, 그리는 순간 "그때는 정상이었다" 는 거짓이 된다.</para>
/// </remarks>
public static class ServerMetricBand
{
    public const string CpuKey = "cpu";
    public const string RamKey = "ram";
    public const string DiskKey = "disk";
    public const string NetworkKey = "network";

    /// <summary>지표 띠 네 칸. <paramref name="metric"/> 이 <c>null</c> 이면 네 칸 모두 "보고 없음".</summary>
    public static IReadOnlyList<ServerMetricCell> Band(ServerMetricDto? metric)
    {
        var badges = BadgesOf(metric);

        return new[]
        {
            Percent(CpuKey, "CPU", metric?.CpuUsage, detail: string.Empty, badges),
            Percent(RamKey, "메모리", metric?.RamUsage, Size(metric?.RamUsedGb, metric?.RamTotalGb), badges),
            Percent(DiskKey, "디스크", metric?.DiskUsage, Size(metric?.DiskUsedGb, metric?.DiskTotalGb), badges),
            Network(metric, badges),
        };
    }

    /// <summary>
    /// 지표 이력 — 새 것이 위로. <b>임계 배지를 만들지 않는다.</b>
    /// </summary>
    public static IReadOnlyList<ServerMetricHistoryRow> History(IEnumerable<ServerMetricDto>? metrics, IClock clock)
    {
        if (clock is null) throw new ArgumentNullException(nameof(clock));
        if (metrics is null) return Array.Empty<ServerMetricHistoryRow>();

        return metrics
            .Where(m => m is not null)
            .Select(m => (Metric: m, At: ServerStatusRules.ParseTime(m.ObservedAtEffective)))
            .OrderByDescending(x => x.At ?? DateTimeOffset.MinValue)
            .Select(x => new ServerMetricHistoryRow(
                x.At is null ? ServerStatusRules.NotReportedText : x.At.Value.ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                PercentText(x.Metric.CpuUsage),
                PercentText(x.Metric.RamUsage),
                PercentText(x.Metric.DiskUsage),
                NetworkText(x.Metric.NetworkInMbps, x.Metric.NetworkOutMbps)))
            .ToArray();
    }

    /// <summary>서버가 보낸 임계 초과 항목 — 지표 키(<c>cpu</c>…)로 찾는다. 배열이 아니면 비어 있다.</summary>
    public static IReadOnlyDictionary<string, ServerThresholdExceededDto> BadgesOf(ServerMetricDto? metric)
    {
        var map = new Dictionary<string, ServerThresholdExceededDto>(StringComparer.OrdinalIgnoreCase);
        if (metric is null) return map;

        foreach (var item in metric.ThresholdExceededItems)
        {
            var field = (item?.Field ?? string.Empty).Trim();
            if (field.Length == 0 || map.ContainsKey(field)) continue;
            map[field] = item!;
        }
        return map;
    }

    private static ServerMetricCell Percent(string key, string label, double? value, string detail,
        IReadOnlyDictionary<string, ServerThresholdExceededDto> badges)
    {
        var (badgeText, isCritical) = Badge(key, badges);
        return value is null
            ? new ServerMetricCell(key, label, ServerStatusRules.NotReportedText, string.Empty, null, badgeText, isCritical, HasValue: false)
            : new ServerMetricCell(key, label, PercentText(value), detail, Math.Clamp(value.Value / 100d, 0d, 1d), badgeText, isCritical, HasValue: true);
    }

    private static ServerMetricCell Network(ServerMetricDto? metric, IReadOnlyDictionary<string, ServerThresholdExceededDto> badges)
    {
        var (badgeText, isCritical) = Badge(NetworkKey, badges);
        var hasValue = metric?.NetworkInMbps is not null || metric?.NetworkOutMbps is not null;

        // 부가 값은 단위만 — 압축형 띠(2026-09-27)에서 "Mbps (수신 / 송신)" 은 값 옆에 들지 않았다. 수신 · 송신은 값의 ↓ · ↑ 가 말한다.
        // 네트워크는 상한이 없어 비율을 만들지 않는다 — 막대를 그리면 눈금 없는 그래프가 된다.
        return new ServerMetricCell(
            NetworkKey, "네트워크",
            hasValue ? NetworkText(metric?.NetworkInMbps, metric?.NetworkOutMbps) : ServerStatusRules.NotReportedText,
            hasValue ? "Mbps" : string.Empty, null, badgeText, isCritical, hasValue);
    }

    private static (string? Text, bool IsCritical) Badge(string key, IReadOnlyDictionary<string, ServerThresholdExceededDto> badges)
    {
        if (!badges.TryGetValue(key, out var badge)) return (null, false);

        var severity = (badge.Severity ?? string.Empty).Trim();
        var critical = severity.StartsWith("crit", StringComparison.OrdinalIgnoreCase);
        var label = critical ? "임계 초과" : severity.Length > 0 ? "임계 경고" : "임계 초과";
        return ($"{label} · {Round(badge.Threshold)} 초과", critical);
    }

    private static string PercentText(double? value)
        => value is null ? ServerStatusRules.NotReportedText : $"{Round(value.Value)}%";

    private static string NetworkText(double? inMbps, double? outMbps)
        => inMbps is null && outMbps is null
            ? ServerStatusRules.NotReportedText
            : $"↓{Round(inMbps ?? 0)} / ↑{Round(outMbps ?? 0)}";

    private static string Size(double? used, double? total)
        => used is null || total is null ? string.Empty : $"{Round(used.Value)} / {Round(total.Value)} GB";

    private static string Round(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

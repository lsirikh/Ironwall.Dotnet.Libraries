using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터 레일 배지 집계 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>레일 집계가 보는 최소한 — 행 뷰모델이 이것만 만족하면 센다.</summary>
public interface IServerRailItem
{
    /// <summary>이 서버가 속한 레일 칸(<see cref="ServerTypeCatalog.RailKeyOf"/>).</summary>
    string RailKey { get; }

    ServerStatusKind Status { get; }
}

/// <summary>레일 한 칸의 집계.</summary>
/// <param name="Key">레일 칸 키.</param>
/// <param name="Total">그 칸의 서버 수.</param>
/// <param name="Fault">그중 장애(<see cref="ServerStatusKind.Error"/>).</param>
/// <param name="NotReported">그중 한 번도 보고가 없는 수 — 장애와 <b>따로</b> 센다.</param>
public sealed record ServerRailCount(string Key, int Total, int Fault, int NotReported);

/// <summary>
/// 좌측 레일 배지 집계 — 순수 함수. WPF · 서버 호출 없이 이미 받아 둔 행만 훑는다.
/// </summary>
/// <remarks>
/// <see cref="ServerTypeCatalog.RailOrder"/> 그대로 <b>항상 같은 칸</b>을 돌려준다 — 0대인 칸도 자리를 지킨다
/// (칸이 사라지면 그 유형이 존재하는지조차 알 수 없다). "시스템 이벤트" 는 서버 목록이 아니라 개수를 내지 않는다.
/// </remarks>
public static class ServerRailCounter
{
    public static IReadOnlyList<ServerRailCount> Count(IEnumerable<IServerRailItem> servers)
    {
        if (servers is null) throw new ArgumentNullException(nameof(servers));

        var totals = new Dictionary<string, (int Total, int Fault, int NotReported)>(StringComparer.Ordinal);
        foreach (var spec in ServerTypeCatalog.RailOrder) totals[spec.Key] = (0, 0, 0);

        foreach (var server in servers)
        {
            if (server is null) continue;

            var fault = ServerStatusRules.IsFault(server.Status) ? 1 : 0;
            var silent = server.Status == ServerStatusKind.NotReported ? 1 : 0;

            Add(totals, ServerTypeCatalog.AllKey, fault, silent);

            // 모르는 칸(레일에 자리가 없는 키)은 조용히 건너뛴다 — 여기서 던지면 목록 전체가 죽는다.
            var key = totals.ContainsKey(server.RailKey) && ServerTypeCatalog.IsServerList(server.RailKey)
                ? server.RailKey
                : ServerTypeCatalog.EtcKey;
            Add(totals, key, fault, silent);
        }

        return ServerTypeCatalog.RailOrder
            .Select(spec => new ServerRailCount(spec.Key, totals[spec.Key].Total, totals[spec.Key].Fault, totals[spec.Key].NotReported))
            .ToArray();
    }

    /// <summary>레일 아래 요약 — "전체 N대 / 장애 M대 / 보고 없음 K대".</summary>
    public static string FooterText(IEnumerable<ServerRailCount> counts)
    {
        if (counts is null) throw new ArgumentNullException(nameof(counts));

        var all = counts.FirstOrDefault(c => string.Equals(c.Key, ServerTypeCatalog.AllKey, StringComparison.Ordinal))
                  ?? new ServerRailCount(ServerTypeCatalog.AllKey, 0, 0, 0);
        return $"전체 {all.Total}대 / 장애 {all.Fault}대 / 보고 없음 {all.NotReported}대";
    }

    private static void Add(IDictionary<string, (int Total, int Fault, int NotReported)> map, string key, int fault, int silent)
    {
        if (!map.TryGetValue(key, out var current)) return;
        map[key] = (current.Total + 1, current.Fault + fault, current.NotReported + silent);
    }
}

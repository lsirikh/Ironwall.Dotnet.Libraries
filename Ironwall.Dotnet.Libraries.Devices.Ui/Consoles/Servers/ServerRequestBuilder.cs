using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 편집 폼의 지역 검사 · 임계 읽기 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>한 칸의 잘못 — 칸 키와 사람이 읽는 까닭.</summary>
public sealed record ServerFieldError(string Key, string Message);

/// <summary>
/// 화면이 보내기 <b>전에</b> 하는 판정 — 순수 함수. 본문 조립은 API 계층
/// (<see cref="ServerAxisWriter"/>)이 판본을 보고 한다.
/// </summary>
/// <remarks>
/// 본문 조립을 여기 두지 않는 까닭: 같은 뜻의 쓰기가 6.3 은 평면 스칼라, 7.0+ 는 축이라
/// <b>한 본문으로 양립하지 않는다</b>. 판본 분기는 통로 한 곳에만 둔다.
/// </remarks>
public static class ServerRequestBuilder
{
    public const string NameKey = "name";
    public const string IpKey = "ip_address";
    public const string PortKey = "port";
    public const string HostnameKey = "hostname";
    public const string UserNameKey = "user_name";

    /// <summary>보내기 전 지역 검사. 빈 목록이면 보내도 된다.</summary>
    /// <param name="intent">사람이 고친 것.</param>
    /// <param name="fetched">기준선(등록 중이면 <c>null</c>).</param>
    /// <param name="contract">판본 — 6.3 은 "비우기" 를 보낼 수 없다.</param>
    public static IReadOnlyList<ServerFieldError> Validate(
        ServerWriteIntent intent, ServerAxisView? fetched, EnumServerContract contract = EnumServerContract.V8_0)
    {
        if (intent is null) throw new ArgumentNullException(nameof(intent));

        var errors = new List<ServerFieldError>();
        var name = (intent.Name ?? fetched?.Name ?? string.Empty).Trim();
        var ip = (intent.IpAddress ?? fetched?.IpAddress ?? string.Empty).Trim();
        var port = intent.Port ?? fetched?.Port ?? 0;

        if (name.Length == 0) errors.Add(new ServerFieldError(NameKey, "이름은 비울 수 없습니다"));
        if (ip.Length == 0) errors.Add(new ServerFieldError(IpKey, "주소는 비울 수 없습니다"));
        if (port is < 1 or > 65535) errors.Add(new ServerFieldError(PortKey, "포트는 1~65535 입니다"));

        // 6.3 에는 키 삭제(RFC 7396) 입구가 없다 — 비우려는 시도를 조용히 무시하지 않고 막는다.
        if (!ServerAxisWriter.SupportsClearing(contract))
        {
            if (intent.ClearHostname) errors.Add(new ServerFieldError(HostnameKey, CannotClear("호스트명")));
            if (intent.ClearUserName) errors.Add(new ServerFieldError(UserNameKey, CannotClear("계정")));
        }

        foreach (var (group, warning, critical, label) in new[]
        {
            ("cpu", intent.CpuWarning, intent.CpuCritical, "CPU"),
            ("ram", intent.RamWarning, intent.RamCritical, "메모리"),
            ("disk", intent.DiskWarning, intent.DiskCritical, "디스크"),
        })
        {
            var effectiveWarning = warning ?? ReadThreshold(fetched?.Thresholds, group, "warning");
            var effectiveCritical = critical ?? ReadThreshold(fetched?.Thresholds, group, "critical");
            if (effectiveWarning is not null && effectiveCritical is not null && effectiveWarning >= effectiveCritical)
                errors.Add(new ServerFieldError($"threshold.{group}", $"{label} 경고는 위험보다 작아야 합니다"));
        }

        var netWarning = intent.NetworkWarningMbps ?? ReadThreshold(fetched?.Thresholds, "network", "warning_mbps");
        var netCritical = intent.NetworkCriticalMbps ?? ReadThreshold(fetched?.Thresholds, "network", "critical_mbps");
        if (netWarning is not null && netCritical is not null && netWarning >= netCritical)
            errors.Add(new ServerFieldError("threshold.network", "네트워크 경고는 위험보다 작아야 합니다"));

        return errors;
    }

    /// <summary>받아 온 임계에서 한 값을 읽는다. 없으면 <c>null</c>.</summary>
    public static double? ReadThreshold(JObject? thresholds, string group, string key)
        => thresholds?[group] is JObject section && section[key] is JValue value
           && value.Type is JTokenType.Float or JTokenType.Integer
            ? value.Value<double>()
            : null;

    /// <summary>받아 온 모드에서 한 값을 읽는다.</summary>
    public static string? ReadMode(JObject? modes, string key)
        => modes?[key] is JValue value && value.Type == JTokenType.String ? value.Value<string>() : null;

    /// <summary>6.3 에서 비우기가 막히는 까닭 — 칸 주석과 오류 문구가 같은 문장을 쓴다.</summary>
    public static string CannotClear(string label)
        => Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanParticles.Resolve($"현재 서버에서는 {label}을(를) 비울 수 없습니다. 다른 값으로 바꾸기만 할 수 있습니다");
}

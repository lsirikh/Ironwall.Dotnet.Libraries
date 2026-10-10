using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>링 위 센서 한 대 — 저장된 체인 위치(Ch1(A) 쪽에서 센 수)와 이름.</summary>
public sealed record WiringRingSensor(int Order, string Name);

/// <summary>
/// 장애 이벤트의 고장 구간(선 + 두 정수)을 <b>제어기 링의 센서 이름</b>으로 옮긴다(카탈로그 WIR-504 · 정적 판독 D3).
/// </summary>
/// <remarks>
/// <para>결선 창 밖(이벤트 상세)에서 쓰므로 장비 캐시의 센서 모델이 들고 있는 <c>hardware_spec.spec.wiring</c> 만 읽는다 — 서버를 다시 부르지 않는다.</para>
/// <para>셈법은 결선 창의 고장 구간 예시(<see cref="WiringValidation.DescribeFaultSection"/>)와 같다 — 링의 1차 n = Ch1(A) 쪽에서 센 체인 위치 n ·
/// 2차 n = 체인 위치 N+1−n(Ch2(B) 쪽에서 센 수). 구간 정수가 순번인지 버스 주소인지는 서버팀 확인 항목(PRD W-D4)이라 여기서도 순번으로 읽는다.</para>
/// <para>저장된 결선이 없거나 구간이 0 이면 아무 말도 하지 않는다(null) — 상세 칸은 숫자만 보인다.</para>
/// </remarks>
public static class WiringFaultLocator
{
    /// <summary>
    /// 제어기 <paramref name="controllerId"/> 에 붙은 센서 중 결선 자리가 저장된 것(1차 선)을 체인 위치 순으로.
    /// 같은 자리에 두 대가 있으면(저장값이 어긋남) 앞의 것만 쓴다.
    /// </summary>
    public static IReadOnlyList<WiringRingSensor> RingOf(IEnumerable<IBaseDeviceModel>? devices, int controllerId)
    {
        if (devices is null || controllerId <= 0) return Array.Empty<WiringRingSensor>();

        return devices.OfType<ISensorDeviceModel>()
            .Where(s => s.Controller?.Id == controllerId)
            .Select(s => (Sensor: s, Placement: WiringSpec.Read(s.Axes?.HardwareSpec?.Spec)))
            .Where(p => p.Placement is { Line: WiringSpec.LINE_PRIMARY })
            .GroupBy(p => p.Placement!.Order)
            .OrderBy(g => g.Key)
            .Select(g => new WiringRingSensor(g.Key, NameOf(g.First().Sensor)))
            .ToList();
    }

    /// <summary>
    /// "Ch1(A) 쪽 4~5 → 북측 4구간과 북측 5구간 사이" — <paramref name="line"/> 은 장애의 1차(1) · 2차(2).
    /// 링이 비었거나 구간이 0~0 이면 null, 저장된 링 밖이면 그 사실을 말한다.
    /// </summary>
    public static string? Describe(IReadOnlyList<WiringRingSensor>? ring, int line, int start, int end)
    {
        if (ring is null || ring.Count == 0 || (start == 0 && end == 0)) return null;

        var secondary = line == WiringSpec.LINE_SECONDARY;
        var count = ring.Max(r => r.Order);
        var port = secondary ? "Ch2(B) 쪽" : "Ch1(A) 쪽";
        var a = NameAt(start);
        var b = NameAt(end);
        if (a is null || b is null)
            return WiringValidation.Particles($"{port} {start}~{end}은(는) 저장된 결선 범위(1~{count}) 밖입니다");
        return start == end
            ? $"{port} {start}번 = {a}"
            : WiringValidation.Particles($"{port} {start}~{end} → {a}과(와) {b} 사이");

        string? NameAt(int position)
        {
            var order = secondary ? count + 1 - position : position;
            return ring.FirstOrDefault(r => r.Order == order)?.Name;
        }
    }

    private static string NameOf(ISensorDeviceModel sensor)
        => string.IsNullOrWhiteSpace(sensor.DeviceName) ? $"센서 {sensor.DeviceNumber}" : sensor.DeviceName!;
}

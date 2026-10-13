using System.Collections.Generic;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 센서 설치 위치 · 속성 프리셋(sensor-mount-preset FR-14 · W3 "알림" 쓰임) 그림 — 권장 간격 넘음 표지.
/// </summary>
public static partial class FenceScene
{
    /// <summary>권장 간격 넘음 표지의 깊이(세계 단위) — 빈틈 표지(30)보다 조금 앞(겹치지 않게).</summary>
    public const double SPACING_ALERT_DEPTH = 44;

    /// <summary>
    /// 권장 간격 넘음 표지 — 두 센서 가운데 땅 위에 경고 삼각형 + "간격 22.5m · 권장 20m". 탐지 범위를 끄고 있어도 그린다(알림 쓰임).
    /// 알림이 없으면 빈 목록.
    /// </summary>
    public static IReadOnlyList<FenceShape> SpacingAlertMarks(FenceWorld world, FenceProjector p)
    {
        var o = new List<FenceShape>();
        if (world is null) return o;
        foreach (var alert in world.SpacingAlerts)
        {
            var c = p.P(world.SpacingAlertX(alert), 0, SPACING_ALERT_DEPTH);
            o.Add(Poly(FenceInk.SpacingAlertMark, new Point(c.X, c.Y - 8), new Point(c.X + 8, c.Y + 6), new Point(c.X - 8, c.Y + 6)));
            o.Add(Text(FenceInk.SpacingAlertText, new Point(c.X, c.Y + 4), "!", 9));
            o.Add(Text(FenceInk.SpacingAlertText, new Point(c.X, c.Y + 20), alert.Text, 10));
        }
        return o;
    }
}

using System.Windows;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 "모니터" 칸 → 모니터 식별 카드 (순수 함수)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 모니터 목록 칸이 보여 주는 것과 <b>똑같은</b> 번호 · 글자로 식별 카드를 만든다 —
/// 화면에 뜬 "2" 가 목록의 "모니터 2" 와 다르면 식별이 아니라 혼란이다.
/// </summary>
public static class CameraPopupMonitorIdentify
{
    /// <summary>카드 창 접근성 식별자 머리 — <c>CameraPopup.MonitorIdentify.{번호}</c>.</summary>
    public const string AutomationPrefix = "CameraPopup.MonitorIdentify";

    /// <summary>[다시 조회] — 모든 모니터에 카드, 지금 고른 모니터는 강조.</summary>
    public static IReadOnlyList<MonitorIdentifyCard> ForAll(IReadOnlyList<CameraPopupMonitorChoice> monitors,
                                                            CameraPopupMonitorChoice? selected)
    {
        if (monitors is null || monitors.Count == 0) return Array.Empty<MonitorIdentifyCard>();
        return monitors.Select((m, i) => Card(m, i + 1, ReferenceEquals(m, selected))).ToList();
    }

    /// <summary>목록에서 모니터를 바꿨다 — 그 모니터에만 카드(강조).</summary>
    public static IReadOnlyList<MonitorIdentifyCard> ForSelected(IReadOnlyList<CameraPopupMonitorChoice> monitors,
                                                                 CameraPopupMonitorChoice? selected)
    {
        if (monitors is null || selected is null) return Array.Empty<MonitorIdentifyCard>();
        for (var i = 0; i < monitors.Count; i++)
        {
            if (ReferenceEquals(monitors[i], selected)) return new[] { Card(selected, i + 1, isSelected: true) };
        }
        return Array.Empty<MonitorIdentifyCard>();
    }

    private static MonitorIdentifyCard Card(CameraPopupMonitorChoice choice, int ordinal, bool isSelected)
    {
        var m = choice.Monitor;
        return new MonitorIdentifyCard(
            m.Number(ordinal),
            choice.Label,
            m.DeviceName,
            new Int32Rect(m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height),
            m.Dpi,
            isSelected);
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 오른쪽 클릭 메뉴(fence-wiring-editor FR-08) — 뷰모델이 준 항목(<see cref="FenceMenuEntry"/>)을 <see cref="ContextMenu"/> 로 그린다.
/// 펜스 캔버스 · 개념도가 함께 쓴다. <c>MenuItem</c> 은 peer 가 있어 자동화가 AutomationId 로 누를 수 있다.
/// </summary>
public static class FenceMenuPresenter
{
    public const string AUTOMATION_ID = "Devices.Wiring.Fence.Menu";

    /// <summary><paramref name="owner"/> 좌표 <paramref name="at"/> 에 메뉴를 연다.</summary>
    /// <param name="onError">항목 동작이 실패했을 때 — 뷰모델이 상태 줄 · 앱 로그로 알린다(뷰모델이 준 항목은 이미 감싸져 있어 마지막 그물).</param>
    public static ContextMenu Show(FrameworkElement owner, IReadOnlyList<FenceMenuEntry> entries, Point at, Action<FenceMenuEntry, Exception>? onError = null)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = owner,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = at.X,
            VerticalOffset = at.Y,
        };
        AutomationProperties.SetAutomationId(menu, AUTOMATION_ID);
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                menu.Items.Add(new Separator());
                continue;
            }
            var item = new MenuItem
            {
                Header = entry.Text,
                IsEnabled = entry.IsEnabled,
                InputGestureText = entry.Gesture ?? string.Empty,
            };
            AutomationProperties.SetAutomationId(item, entry.AutomationId);
            var run = entry.Run!;
            item.Click += async (_, _) =>
            {
                try { await run(); }
                catch (Exception ex) { onError?.Invoke(entry, ex); }
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
        return menu;
    }
}

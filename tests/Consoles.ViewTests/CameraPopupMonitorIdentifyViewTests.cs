using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Accounts.Ui.ViewTests;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/****************************************************************************
   Purpose      : [다시 조회] 버튼 → 모니터 식별 요청 배선 (실제 뷰, 화면에 붙이지 않는다)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 뷰를 <b>창에 붙이지 않고</b> 버튼만 누른다 — 배선(버튼 → <see cref="CameraPopupSettingsViewModel.RescanMonitors"/> → 요청)을 보고,
/// 화면에 붙지 않은 뷰는 식별 창을 하나도 만들지 않는지(떨어진 옛 뷰 · 오프스크린 렌더 보호) 본다.
/// 실제 모니터에 카드를 띄우는 시험은 하지 않는다(개발 PC 화면을 건드리지 않는다).
/// </summary>
public class CameraPopupMonitorIdentifyViewTests
{
    private const string RescanId = "Settings.CameraPopup.Event.RefreshMonitors";

    [Fact]
    public void should_request_identify_for_every_monitor_when_rescan_button_is_clicked() => AppHost.Run(() =>
    {
        // Arrange
        var vm = NewViewModel();
        var view = new CameraPopupSettingsView { DataContext = vm };
        var requested = new List<IReadOnlyList<MonitorIdentifyCard>>();
        vm.MonitorIdentifyRequested += (_, cards) => requested.Add(cards);
        var windowsBefore = Application.Current.Windows.Count;

        // Act
        var button = Logical<Button>(view).Single(b => AutomationProperties.GetAutomationId(b) == RescanId);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        AppHost.Pump();

        // Assert
        var cards = Assert.Single(requested);
        Assert.Equal(new[] { 1, 2 }, cards.Select(c => c.Number));
        Assert.Equal(1, cards.Count(c => c.IsSelected));
        Assert.Equal(windowsBefore, Application.Current.Windows.Count);                       // 붙지 않은 뷰는 창을 만들지 않는다
        Assert.DoesNotContain(Application.Current.Windows.OfType<MonitorIdentifyWindow>(), _ => true);
    });

    private static CameraPopupSettingsViewModel NewViewModel()
    {
        var port = new Mock<ICameraPopupSettingsPort>();
        port.Setup(p => p.LoadCameraPopup()).Returns(new CameraPopupSettings().Normalize());
        port.SetupGet(p => p.CameraPopupClientId).Returns("gis-test");
        var monitors = new Mock<IDisplayMonitorProvider>();
        monitors.Setup(m => m.GetMonitors()).Returns(new List<DisplayMonitorInfo>
        {
            new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true),
            new(@"\\.\DISPLAY2", new PixelRect(1920, 0, 2560, 1440), new PixelRect(1920, 0, 2560, 1400), false),
        });
        return new CameraPopupSettingsViewModel(port.Object, monitors.Object);
    }

    private static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject d) continue;
            if (d is T hit) yield return hit;
            foreach (var deeper in Logical<T>(d)) yield return deeper;
        }
    }
}

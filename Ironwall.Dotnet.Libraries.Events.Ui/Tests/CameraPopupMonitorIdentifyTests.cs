using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 [다시 조회] → 모니터 식별 카드 (헤드리스 — 창을 띄우지 않는다)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카드의 번호 · 글자가 모니터 목록 칸과 똑같은지, 어느 때 누구에게 띄우는지(다시 조회 = 전부 · 목록 변경 = 그 하나 ·
/// 절 진입 = 없음)를 뷰모델 이벤트로 잠근다.
/// </summary>
public class CameraPopupMonitorIdentifyTests
{
    private sealed class Port : ICameraPopupSettingsPort
    {
        public CameraPopupSettings Stored = new CameraPopupSettings().Normalize();
        public CameraPopupSettings LoadCameraPopup() => Stored;
        public void SaveCameraPopup(CameraPopupSettings settings) => Stored = settings;
        public string CameraPopupClientId => "gis-test";
    }

    private sealed class Monitors : IDisplayMonitorProvider
    {
        public List<DisplayMonitorInfo> List = new()
        {
            new(@"\\.\DISPLAY2", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true),
            new(@"\\.\DISPLAY1", new PixelRect(-2560, 0, 2560, 1440), new PixelRect(-2560, 0, 2560, 1400), false, 144),
            new("GHOST", new PixelRect(1920, 0, 1280, 1024), new PixelRect(1920, 0, 1280, 984), false),
        };

        public IReadOnlyList<DisplayMonitorInfo> GetMonitors() => List;
    }

    private static (CameraPopupSettingsViewModel Vm, Monitors Monitors, List<IReadOnlyList<MonitorIdentifyCard>> Raised) Make()
    {
        var monitors = new Monitors();
        var vm = new CameraPopupSettingsViewModel(new Port(), monitors);
        var raised = new List<IReadOnlyList<MonitorIdentifyCard>>();
        vm.MonitorIdentifyRequested += (_, cards) => raised.Add(cards);
        return (vm, monitors, raised);
    }

    [Fact]
    public void should_use_combo_label_and_device_number_when_rescan_builds_cards()
    {
        var (vm, _, raised) = Make();

        vm.RescanMonitors();

        var cards = Assert.Single(raised);
        Assert.Equal(vm.Monitors.Select(m => m.Label), cards.Select(c => c.Label));          // 목록 글자 그대로
        Assert.Equal(new[] { 2, 1, 3 }, cards.Select(c => c.Number));                       // 장치 번호, 없으면 순번
        Assert.All(cards, c => Assert.StartsWith($"모니터 {c.Number} ", c.Label));            // 큰 번호 = 목록 번호
    }

    [Fact]
    public void should_carry_device_name_physical_bounds_and_dpi_when_cards_are_built()
    {
        var (vm, _, raised) = Make();

        vm.RescanMonitors();

        var left = raised[0].Single(c => c.Number == 1);
        Assert.Equal(@"\\.\DISPLAY1", left.DeviceName);
        Assert.Equal(new System.Windows.Int32Rect(-2560, 0, 2560, 1440), left.PhysicalBounds);
        Assert.Equal(144, left.Dpi);
    }

    [Fact]
    public void should_emphasize_only_the_selected_monitor_when_rescan_shows_all()
    {
        var (vm, _, raised) = Make();
        vm.SelectedMonitor = vm.Monitors[1];
        raised.Clear();

        vm.RescanMonitors();

        var cards = Assert.Single(raised);
        Assert.Equal(3, cards.Count);
        Assert.Equal(new[] { 1 }, cards.Where(c => c.IsSelected).Select(c => c.Number));
    }

    [Fact]
    public void should_identify_only_the_new_monitor_when_selection_changes()
    {
        var (vm, _, raised) = Make();

        vm.SelectedMonitor = vm.Monitors[2];

        var card = Assert.Single(Assert.Single(raised));
        Assert.Equal(3, card.Number);
        Assert.True(card.IsSelected);
        Assert.Equal(vm.Monitors[2].Label, card.Label);
    }

    [Fact]
    public void should_not_identify_when_same_monitor_is_selected_again()
    {
        var (vm, _, raised) = Make();
        var current = vm.SelectedMonitor;

        vm.SelectedMonitor = current;
        vm.SelectedMonitor = null;

        Assert.Empty(raised);
    }

    [Fact]
    public void should_not_identify_when_monitors_are_reread_on_create_or_section_enter()
    {
        var (vm, _, raised) = Make();

        vm.Reload();
        vm.RefreshMonitors();

        Assert.Empty(raised);
    }

    [Fact]
    public void should_not_identify_when_rescan_finds_no_monitors()
    {
        var (vm, monitors, raised) = Make();
        monitors.List.Clear();

        vm.RescanMonitors();

        Assert.Empty(raised);
        Assert.False(vm.HasMonitors);
    }

    [Fact]
    public void should_return_no_cards_when_selected_monitor_is_not_in_list()
    {
        var (vm, _, _) = Make();
        var stranger = new CameraPopupMonitorChoice(vm.Monitors[0].Monitor, vm.Monitors[0].Label);   // 같은 값 · 다른 항목

        Assert.Empty(CameraPopupMonitorIdentify.ForSelected(vm.Monitors, stranger));
        Assert.Empty(CameraPopupMonitorIdentify.ForSelected(vm.Monitors, null));
        Assert.Empty(CameraPopupMonitorIdentify.ForAll(Array.Empty<CameraPopupMonitorChoice>(), null));
    }

    [Fact]
    public void should_explain_rescan_identify_in_help_when_event_section_help_is_read()
    {
        var text = Ironwall.Dotnet.Libraries.Events.Ui.Help.EventsHelp.Entries.Single(e => e.Key == "Settings.CameraPopup.Event").ToPlainText();

        Assert.Contains("[다시 조회]", text);
        Assert.Contains("번호 카드", text);
        Assert.Contains("선택됨", text);
    }
}

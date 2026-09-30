using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 설정 "브로커 요청" — [모니터 목록 가져오기](POPUP_LAYOUT_GET) 헤드리스 (camera-popup-modes T-07 · FR-08)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 가져오기 성공 → 모니터 칸이 관제석 목록으로 바뀐다 · 실패 → 칸 아래 한 줄(목록 · 초안 그대로) ·
/// 창구가 없으면(기본 구현) "가져올 수 없음" · 창구가 던져도 뷰모델은 던지지 않는다.
/// </summary>
public class CameraPopupBrokerMonitorsTests
{
    private sealed class LayoutPort : ICameraPopupSettingsPort
    {
        public CameraPopupSettings Stored = new CameraPopupSettings { Mode = CameraPopupMode.Broker }.Normalize();
        public Func<int, NvrPopupLayoutResult> Answer = _ => NvrPopupLayoutResult.Unavailable;
        public bool Throw;
        public int Calls;
        public int LastTimeout;

        public CameraPopupSettings LoadCameraPopup() => Stored;
        public void SaveCameraPopup(CameraPopupSettings settings) => Stored = settings;
        public string CameraPopupClientId => "gis-7f3a9c2e41b6";

        public Task<NvrPopupLayoutResult> FetchBrokerLayoutAsync(int timeoutSeconds, CancellationToken ct = default)
        {
            Calls++;
            LastTimeout = timeoutSeconds;
            if (Throw) throw new InvalidOperationException("broker gone");
            return Task.FromResult(Answer(timeoutSeconds));
        }
    }

    /// <summary>창구 기본 구현만 가진 가짜(시험 · 미리보기 가짜가 그대로 컴파일되는지도 이것으로 본다).</summary>
    private sealed class PlainPort : ICameraPopupSettingsPort
    {
        public CameraPopupSettings LoadCameraPopup() => new CameraPopupSettings { Mode = CameraPopupMode.Broker };
        public void SaveCameraPopup(CameraPopupSettings settings) { }
        public string CameraPopupClientId => "gis-0";
    }

    private sealed class NoMonitors : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors() => Array.Empty<DisplayMonitorInfo>();
    }

    private static (CameraPopupSettingsViewModel Vm, LayoutPort Port) Make(int brokerMonitor = 1, int timeout = 7)
    {
        var port = new LayoutPort();
        port.Stored = (port.Stored with { BrokerMonitor = brokerMonitor, BrokerResponseTimeoutSeconds = timeout }).Normalize();
        return (new CameraPopupSettingsViewModel(port, new NoMonitors()), port);
    }

    [Fact]
    public async Task should_replace_monitor_choices_with_nvr_list_when_layout_fetched()
    {
        // Arrange
        var (vm, port) = Make(brokerMonitor: 2, timeout: 7);
        port.Answer = _ => NvrPopupLayoutResult.Ok(new[]
        {
            new NvrPopupMonitor(1, true, 1920, 1080),
            new NvrPopupMonitor(2, false, 2560, 1440),
            new NvrPopupMonitor(3, false, 1920, 1080),
        }, defaultMonitor: 1, defaultCell: 5);

        // Act
        await vm.FetchBrokerMonitorsAsync();

        // Assert
        Assert.Equal(1, port.Calls);
        Assert.Equal(7, port.LastTimeout);                    // 초안의 "응답 기다림" 초를 넘긴다
        Assert.Equal(new[] { 1, 2, 3 }, vm.BrokerMonitorChoices.Select(c => c.Value));
        Assert.Equal("모니터 1 · 1920×1080 (주)", vm.BrokerMonitorChoices[0].Label);
        Assert.Equal(2, vm.SelectedBrokerMonitor?.Value);      // 초안 값은 그대로 · 목록에서 다시 골라진다
        Assert.False(vm.BrokerMonitorNeedsAttention);
        Assert.Contains("3대", vm.BrokerMonitorNote);
        Assert.Contains("모니터 1 · 칸 5", vm.BrokerMonitorNote);
        Assert.False(vm.IsDirty);                              // 가져오기는 초안을 바꾸지 않는다
        Assert.True(vm.CanFetchBrokerMonitors);
    }

    [Fact]
    public async Task should_keep_saved_monitor_as_missing_entry_when_nvr_list_lacks_it()
    {
        var (vm, port) = Make(brokerMonitor: 4);
        port.Answer = _ => NvrPopupLayoutResult.Ok(new[] { new NvrPopupMonitor(1, true, 1920, 1080) });

        await vm.FetchBrokerMonitorsAsync();

        Assert.Equal(new[] { 1, 4 }, vm.BrokerMonitorChoices.Select(c => c.Value));
        Assert.Contains("목록에 없음", vm.BrokerMonitorChoices[1].Label);
        Assert.Equal(4, vm.SelectedBrokerMonitor?.Value);
        Assert.True(vm.BrokerMonitorNeedsAttention);
        Assert.Contains("없습니다", vm.BrokerMonitorNote);
    }

    [Fact]
    public async Task should_show_inline_reason_and_keep_choices_when_fetch_fails()
    {
        var (vm, port) = Make();
        var before = vm.BrokerMonitorChoices.Select(c => c.Value).ToList();
        port.Answer = s => NvrPopupLayoutResult.Fail($"NVR Manager 응답 없음({s}초) — 이 관제석에서 돌고 있는지 확인하세요");

        await vm.FetchBrokerMonitorsAsync();

        Assert.Equal(before, vm.BrokerMonitorChoices.Select(c => c.Value));
        Assert.True(vm.BrokerMonitorNeedsAttention);
        Assert.Equal("NVR Manager 응답 없음(7초) — 이 관제석에서 돌고 있는지 확인하세요", vm.BrokerMonitorNote);
        Assert.True(vm.CanFetchBrokerMonitors);
    }

    [Fact]
    public async Task should_not_throw_and_show_reason_when_port_throws()
    {
        var (vm, port) = Make();
        port.Throw = true;

        var ex = await Record.ExceptionAsync(() => vm.FetchBrokerMonitorsAsync());

        Assert.Null(ex);
        Assert.True(vm.BrokerMonitorNeedsAttention);
        Assert.Contains("가져오지 못했습니다", vm.BrokerMonitorNote);
        Assert.False(vm.IsFetchingBrokerMonitors);
    }

    [Fact]
    public async Task should_report_unavailable_when_port_has_no_broker()
    {
        var vm = new CameraPopupSettingsViewModel(new PlainPort(), new NoMonitors());

        await vm.FetchBrokerMonitorsAsync();

        Assert.Equal(NvrPopupLayoutResult.Unavailable.Message, vm.BrokerMonitorNote);
        Assert.Equal(Enumerable.Range(1, 8), vm.BrokerMonitorChoices.Select(c => c.Value));
    }

    [Fact]
    public async Task should_ignore_second_fetch_when_first_is_pending()
    {
        // Arrange — 첫 요청이 끝나지 않게 붙잡는다
        var gate = new TaskCompletionSource<NvrPopupLayoutResult>();
        var port = new PendingPort(gate.Task);
        var vm = new CameraPopupSettingsViewModel(port, new NoMonitors());

        // Act
        var first = vm.FetchBrokerMonitorsAsync();
        Assert.True(vm.IsFetchingBrokerMonitors);
        Assert.False(vm.CanFetchBrokerMonitors);
        await vm.FetchBrokerMonitorsAsync();                   // 연타 — 두 번째는 보내지 않는다
        gate.SetResult(NvrPopupLayoutResult.Ok(new[] { new NvrPopupMonitor(1, true, 800, 600) }));
        await first;

        // Assert
        Assert.Equal(1, port.Calls);
        Assert.True(vm.CanFetchBrokerMonitors);
    }

    private sealed class PendingPort : ICameraPopupSettingsPort
    {
        private readonly Task<NvrPopupLayoutResult> _answer;
        public int Calls;
        public PendingPort(Task<NvrPopupLayoutResult> answer) => _answer = answer;
        public CameraPopupSettings LoadCameraPopup() => new CameraPopupSettings { Mode = CameraPopupMode.Broker };
        public void SaveCameraPopup(CameraPopupSettings settings) { }
        public string CameraPopupClientId => "gis-1";
        public Task<NvrPopupLayoutResult> FetchBrokerLayoutAsync(int timeoutSeconds, CancellationToken ct = default)
        {
            Calls++;
            return _answer;
        }
    }
}

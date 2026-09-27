using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : TEST-29 — 심볼 상세 · 마커 우클릭의 "소속 부대" 줄과 [관계도에서 보기](FR-46)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-M013 · M014 · M015 · M016 · M017 · M018.
                  부대는 장비 모델의 UnitId(서버 unit_id — probe log V-12)에서 읽는다. 심볼과 장비의 연결은 객체(LinkedDevice)다.
****************************************************************************/
public class SymbolDetailUnitLineTests
{
    private const string SevenPath = "7중대 · 2대대 › 1연대 › 제○○사단";

    #region - 순수 판정 -
    [Fact]
    public void should_show_name_and_ancestor_path_with_an_open_button_when_unit_is_known()
    {
        var line = SymbolDetailUnitLine.Evaluate(27, new FakeDirectory { [27] = SevenPath }, loadAttempted: false);

        Assert.True(line.IsVisible);
        Assert.Equal($"소속 부대 {SevenPath}", line.Text);                    // SIM-M013
        Assert.Equal(27, line.UnitId);
        Assert.True(line.CanOpen);
    }

    [Fact]
    public void should_say_no_unit_information_when_the_device_has_no_unit()
    {
        var line = SymbolDetailUnitLine.Evaluate(null, new FakeDirectory(), loadAttempted: true);

        Assert.True(line.IsVisible);
        Assert.Equal(SymbolDetailUnitLine.NoUnitText, line.Text);
        Assert.False(line.CanOpen);
    }

    [Fact]
    public void should_hide_the_line_when_there_is_no_directory_or_the_server_is_older_than_8_0()
    {
        Assert.False(SymbolDetailUnitLine.Evaluate(27, null, false).IsVisible);                                    // DI 미등록
        Assert.False(SymbolDetailUnitLine.Evaluate(27, new FakeDirectory { Available = false }, false).IsVisible); // SIM-M015
    }

    [Fact]
    public void should_say_loading_until_the_directory_was_read_once()
    {
        var line = SymbolDetailUnitLine.Evaluate(27, new FakeDirectory(), loadAttempted: false);

        Assert.Equal(SymbolDetailUnitLine.LoadingText, line.Text);            // SIM-M014
        Assert.False(line.CanOpen);
    }

    [Fact]
    public void should_show_the_raw_id_without_the_button_when_the_unit_is_not_in_the_graph_after_loading()
    {
        var line = SymbolDetailUnitLine.Evaluate(27, new FakeDirectory(), loadAttempted: true);

        Assert.StartsWith(SymbolDetailUnitLine.Prefix, line.Text);          // SIM-M016 — 이름을 지어내지 않는다
        Assert.Contains("#27", line.Text);
        Assert.False(line.CanOpen);
    }
    #endregion

    #region - 뷰모델 -
    [Fact]
    public async Task should_publish_open_unit_console_request_once_with_map_rail_when_button_pressed()
    {
        // Arrange
        var events = new EventAggregator();
        var received = new List<OpenUnitConsoleRequest>();
        var sink = new Sink(received);
        events.SubscribeOnPublishedThread(sink);
        var vm = new SymbolDetailViewModel(new FakeDirectory { [27] = SevenPath }, events);
        vm.Load(Marker(unitId: 27), new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));
        await vm.PendingUnitLoad;

        // Act
        Assert.True(vm.OpenUnitMapCommand.CanExecute(null));
        vm.OpenUnitMapCommand.Execute(null);

        // Assert — SIM-M017 · M018
        var request = Assert.Single(received);
        Assert.Equal(new OpenUnitConsoleRequest(27, OpenMap: true), request);
        Assert.True(vm.IsUnitLineVisible);
        Assert.Equal($"소속 부대 {SevenPath}", vm.UnitLineText);
    }

    [Fact]
    public async Task should_load_the_directory_once_then_update_the_text_when_the_name_was_not_cached()
    {
        var gate = new TaskCompletionSource();
        var directory = new FakeDirectory { LoadsOnDemand = { [27] = SevenPath }, LoadGate = gate };
        var vm = new SymbolDetailViewModel(directory, new EventAggregator());

        vm.Load(Marker(unitId: 27), new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));
        Assert.Equal(SymbolDetailUnitLine.LoadingText, vm.UnitLineText);    // 먼저 "불러오는 중"
        Assert.False(vm.OpenUnitMapCommand.CanExecute(null));
        gate.SetResult();
        await vm.PendingUnitLoad;

        Assert.Equal(1, directory.LoadCalls);
        Assert.Equal($"소속 부대 {SevenPath}", vm.UnitLineText);
        Assert.True(vm.OpenUnitMapCommand.CanExecute(null));
    }

    [Fact]
    public async Task should_refresh_the_text_when_the_directory_reports_a_change()
    {
        var directory = new FakeDirectory { [27] = SevenPath };
        var vm = new SymbolDetailViewModel(directory, new EventAggregator());
        vm.Load(Marker(unitId: 27), new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));
        await vm.PendingUnitLoad;

        directory[27] = "7중대(개칭) · 2대대 › 1연대 › 제○○사단";
        directory.RaiseChanged();

        Assert.Equal("소속 부대 7중대(개칭) · 2대대 › 1연대 › 제○○사단", vm.UnitLineText);
    }

    [Fact]
    public async Task should_stop_listening_to_the_directory_when_unloaded()
    {
        var directory = new FakeDirectory { [27] = SevenPath };
        var vm = new SymbolDetailViewModel(directory, new EventAggregator());
        vm.Load(Marker(unitId: 27), new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));
        await vm.PendingUnitLoad;

        vm.Unload();

        Assert.Equal(0, directory.ChangedSubscribers);                        // 닫힌 창이 사전을 붙잡지 않는다
    }

    [Fact]
    public void should_keep_the_line_hidden_and_the_button_disabled_without_a_directory()
    {
        var vm = new SymbolDetailViewModel();

        vm.Load(Marker(unitId: 27), new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));

        Assert.False(vm.IsUnitLineVisible);
        Assert.False(vm.OpenUnitMapCommand.CanExecute(null));
    }
    #endregion

    #region - Fakes -
    private static IPidsEditableMarker Marker(int? unitId)
    {
        var device = new CameraDeviceModel { Id = 379, UnitId = unitId };
        var marker = new Mock<IPidsEditableMarker>();
        marker.SetupGet(m => m.LinkedDevice).Returns(device);
        marker.SetupGet(m => m.LinkedDeviceId).Returns(379);
        marker.SetupGet(m => m.DeviceType).Returns(EnumDeviceType.IpCamera);
        marker.SetupGet(m => m.Title).Returns("카메라-1");
        return marker.Object;
    }

    private sealed class Sink : IHandle<OpenUnitConsoleRequest>
    {
        private readonly List<OpenUnitConsoleRequest> _received;
        public Sink(List<OpenUnitConsoleRequest> received) => _received = received;
        public Task HandleAsync(OpenUnitConsoleRequest message, CancellationToken cancellationToken)
        {
            _received.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDirectory : IUnitDirectory
    {
        private readonly Dictionary<int, string> _names = new();
        private EventHandler? _changed;

        public bool Available { get; set; } = true;
        public Dictionary<int, string> LoadsOnDemand { get; } = new();
        public int LoadCalls { get; private set; }
        public int ChangedSubscribers => _changed?.GetInvocationList().Length ?? 0;

        public string this[int id] { set => _names[id] = value; }

        public bool IsAvailable => Available;
        public string? Describe(int unitId) => _names.TryGetValue(unitId, out var text) ? text : null;

        public TaskCompletionSource? LoadGate { get; set; }

        public async Task EnsureLoadedAsync(CancellationToken token = default)
        {
            LoadCalls++;
            if (LoadGate != null) await LoadGate.Task;
            foreach (var (id, text) in LoadsOnDemand) _names[id] = text;
        }

        public event EventHandler? Changed { add => _changed += value; remove => _changed -= value; }
        public void RaiseChanged() => _changed?.Invoke(this, EventArgs.Empty);
    }
    #endregion
}

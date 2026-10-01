using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 입구가 펜스 구성 로컬 저장소를 다루는가(fence-wiring-editor FR-11 · FR-16) — 등록이 없거나 만들다 실패해도 창은 열리고(로컬 저장 칸만 숨음),
/// 저장된 구성이 있으면 그것으로 연다.
/// </summary>
public class WiringLauncherFenceTests
{
    private sealed class CapturingWindows : IWindowManager
    {
        public WiringViewModel? Shown { get; private set; }

        public Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            if (rootModel is WiringViewModel vm) Shown = vm;
            return Task.FromResult<bool?>(false);
        }

        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
    }

    private sealed class ThrowingStore : IFenceLayoutStore
    {
        public Task<FenceLayoutLoadResult> LoadAsync(FenceLayoutKey key, CancellationToken token = default) => throw new InvalidOperationException("DB 없음");
        public Task<FenceLayoutSaveResult> SaveAsync(FenceLayoutKey key, FenceLayoutDocument document, FenceLayoutSaveMode mode = FenceLayoutSaveMode.Normal,
                                                     CancellationToken token = default) => throw new InvalidOperationException("DB 없음");
    }

    private static (WiringLauncher Launcher, CapturingWindows Windows, ControllerDeviceModel Controller) Build(Lazy<IFenceLayoutStore>? store,
                                                                                                          WiringServerIdentity? server = null)
    {
        var log = new MockLogService();
        var devices = new DeviceProvider();
        var controller = new ControllerDeviceModel { Id = 10, DeviceNumber = 1, DeviceName = "북측 제어기 B" };
        devices.Add(controller);
        for (var i = 0; i < 3; i++)
            devices.Add(new SensorDeviceModel { Id = 101 + i, DeviceNumber = 1101 + i, DeviceName = $"북측 {i + 1}구간", Controller = controller });
        var windows = new CapturingWindows();
        var launcher = new WiringLauncher(windows, new MockDeviceApiService(), new MockDeviceProviderService(), devices, WiringDoubles.AxisPolicy(),
                                          log: log, fenceStore: store, server: server);
        return (launcher, windows, controller);
    }

    [Fact]
    public async Task should_open_the_window_with_a_proposed_fence_and_no_local_saving_when_the_store_cannot_be_created()
    {
        var (launcher, windows, controller) = Build(new Lazy<IFenceLayoutStore>(() => throw new InvalidOperationException("GMaps.Db 모듈 없음")));

        await launcher.OpenAsync(controller);

        Assert.NotNull(windows.Shown);
        Assert.False(windows.Shown!.HasFenceStore);
        Assert.True(windows.Shown.FenceLayout.IsProposed);
    }

    [Fact]
    public async Task should_still_open_the_window_when_loading_the_stored_fence_fails()
    {
        var (launcher, windows, controller) = Build(new Lazy<IFenceLayoutStore>(() => new ThrowingStore()));

        await launcher.OpenAsync(controller);

        Assert.NotNull(windows.Shown);
        Assert.True(windows.Shown!.HasFenceStore);                            // 저장소는 있다 — 읽기만 실패
        Assert.True(windows.Shown.FenceLayout.IsProposed);
        Assert.Equal(WiringViewModel.FENCE_READ_FAILED_NOTICE, windows.Shown.FenceNoticeText);   // "없음" 이 아니라 "읽지 못함"
    }

    [Fact]
    public async Task should_load_with_the_api_server_host_and_port_in_the_key_when_a_server_identity_is_registered()
    {
        // Arrange
        var store = new FakeFenceStore();
        var (launcher, _, controller) = Build(new Lazy<IFenceLayoutStore>(() => store), new WiringServerIdentity(() => "https://10.0.0.5:8000/api"));

        // Act
        await launcher.OpenAsync(controller);

        // Assert
        Assert.Equal(new FenceLayoutKey("10.0.0.5:8000", 10), Assert.Single(store.Keys));
    }

    [Fact]
    public async Task should_open_with_the_stored_fence_when_the_store_has_one()
    {
        var store = new FakeFenceStore
        {
            Stored = new FenceLayoutDocument
            {
                ControllerId = 10,
                Panels = Enumerable.Repeat(FencePanelSpec.Default(Ironwall.Dotnet.Libraries.Enums.EnumFenceStyle.DesignFence), 4).ToList(),
                Mounts = new Dictionary<int, SensorMountSpec>
                {
                    [101] = new(0, FenceMountSpot.PostMiddle),
                    [102] = new(1, FenceMountSpot.PanelCenter),
                    [103] = new(3, FenceMountSpot.PostTop),
                },
                Bands = NumberBandSet.Tier3,
                Revision = 2,
            },
        };
        var (launcher, windows, controller) = Build(new Lazy<IFenceLayoutStore>(() => store));

        await launcher.OpenAsync(controller);

        var vm = windows.Shown!;
        Assert.False(vm.FenceLayout.IsProposed);
        Assert.Equal(4, vm.FenceLayout.Panels.Count);
        Assert.Equal(new SensorMountSpec(1, FenceMountSpot.PanelCenter), vm.FenceLayout.MountOf(102));
        Assert.Equal(NumberBandSet.Tier3, vm.FenceLayout.Bands);
        Assert.False(vm.HasChanges);                                          // 불러오기만으로 바뀐 줄이 생기지 않는다(번호는 저장 때 · 편집 때만)
    }
}

using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 — 창 입구. <b>이 창이 열리지 못하는 서버에서 호스트를 다치게 하지 않는다</b>는 것이 요지다.
/// </summary>
/// <remarks>
/// 호스트는 <c>IUnitConsoleLauncher</c> 하나만 안다. Autofac 은 등록된 서비스의 <c>Lazy&lt;T&gt;</c> 를
/// 추가 등록 없이 내주므로, 호스트는 <c>Lazy&lt;IUnitConsoleLauncher&gt;</c> 로 받아 <b>누를 때</b> 해석할 수 있다 —
/// 부대 표면이 없는 서버에서 이 창의 의존이 하나도 만들어지지 않는다.
/// </remarks>
[Collection("CaliburnIoC")]
public class UnitConsoleLauncherTests
{
    private sealed class RecordingWindows : IWindowManager
    {
        public List<object> Dialogs { get; } = new();

        public Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Dialogs.Add(rootModel);
            return Task.FromResult<bool?>(true);
        }

        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Dialogs.Add(rootModel);
            return Task.CompletedTask;
        }

        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Dialogs.Add(rootModel);
            return Task.CompletedTask;
        }
    }

    private sealed class StubUnits : IUnitGraphApi
    {
        public StubUnits(bool available) => IsAvailable = available;

        public bool IsAvailable { get; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(new UnitGraphDto()));

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto()));

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto()));
    }

    private sealed class StubDevices : IUnitDeviceApi
    {
        public bool IsAvailable => true;

        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(UnitDeviceLoadResult.Empty);

        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "ok"));
    }

    [Fact]
    public void should_hide_the_entry_when_the_server_has_no_unit_surface()
        => Assert.False(new UnitConsoleLauncher(new RecordingWindows(), new StubUnits(false), new StubDevices()).IsAvailable);

    [Fact]
    public void should_offer_the_entry_when_the_server_has_the_unit_surface()
        => Assert.True(new UnitConsoleLauncher(new RecordingWindows(), new StubUnits(true), new StubDevices()).IsAvailable);

    [Fact]
    public async Task should_open_no_window_when_the_server_has_no_unit_surface()
    {
        var windows = new RecordingWindows();

        await new UnitConsoleLauncher(windows, new StubUnits(false), new StubDevices()).OpenAsync();

        Assert.Empty(windows.Dialogs);      // 6.3 에서 눌려도 창을 만들지 않는다(이중 방어)
    }

    [Fact]
    public async Task should_open_a_fresh_console_each_time()
    {
        var windows = new RecordingWindows();
        var launcher = new UnitConsoleLauncher(windows, new StubUnits(true), new StubDevices());

        await launcher.OpenAsync();
        await launcher.OpenAsync();

        Assert.Equal(2, windows.Dialogs.Count);
        Assert.IsType<UnitConsoleViewModel>(windows.Dialogs[0]);
        Assert.NotSame(windows.Dialogs[0], windows.Dialogs[1]);     // 싱글턴이 아니다 — 열 때마다 새로 만든다
    }

    [Fact]
    public void should_refuse_to_be_built_without_a_window_manager()
        => Assert.Throws<ArgumentNullException>(() => new UnitConsoleLauncher(null!, new StubUnits(true), new StubDevices()));
}

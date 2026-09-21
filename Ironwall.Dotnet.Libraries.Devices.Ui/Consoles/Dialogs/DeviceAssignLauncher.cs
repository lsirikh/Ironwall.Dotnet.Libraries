using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;

/// <summary>장비 배정 창을 여는 입구. 콘솔 뷰모델은 이 인터페이스만 안다.</summary>
public interface IDeviceAssignLauncher
{
    /// <summary>그룹 하나의 소속을 고친다. 서버에 실제로 보냈으면 true.</summary>
    Task<bool> OpenAsync(int groupId, string? groupName, CancellationToken token = default);
}

/// <summary>
/// 창 관리자(<see cref="IWindowManager"/>)로 라이브러리가 직접 연다 — 호스트의 메시지 계약을 늘리지 않는다
/// (N-03 <c>AssemblyLauncher</c> · N-04 <c>WiringLauncher</c> 와 같은 관용구).
/// </summary>
/// <remarks>
/// <para>창 폭은 <see cref="DialogSizeRules.WindowWidth"/> 가 정한다 — 규격(L 720)에 여백을 더한 값이고,
/// 여기에 숫자를 따로 적지 않는다.</para>
/// <para>보내기 직전 재조회는 <see cref="IDeviceProviderService.FetchAllDevicesAsync"/> 다. 그 한 번이
/// 다른 창이 그 사이 그룹을 바꿨는지 보는 유일한 눈이다.</para>
/// </remarks>
public sealed class DeviceAssignLauncher : IDeviceAssignLauncher
{
    private readonly IWindowManager _windows;
    private readonly IDeviceApiService _api;
    private readonly IDeviceProviderService _providerService;
    private readonly DeviceProvider _devices;
    private readonly ILogService? _log;

    public DeviceAssignLauncher(IWindowManager windows, IDeviceApiService api, IDeviceProviderService providerService,
        DeviceProvider devices, ILogService? log = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _log = log;
    }

    public async Task<bool> OpenAsync(int groupId, string? groupName, CancellationToken token = default)
    {
        var vm = new DeviceAssignDialogViewModel(_api, () => _devices.OfType<IBaseDeviceModel>(),
            ct => _providerService.FetchAllDevicesAsync(ct), _log);
        vm.Initialize(groupId, groupName);

        await _windows.ShowDialogAsync(vm, null, WindowSettings()).ConfigureAwait(true);
        return vm.Saved;
    }

    private static IDictionary<string, object> WindowSettings()
    {
        var width = DialogSizeRules.WindowWidth(DialogSize.Large);
        return new Dictionary<string, object>
        {
            ["Width"] = width,
            ["Height"] = 620d,
            ["MinWidth"] = DialogSizeRules.MinWidth,
            ["MinHeight"] = 420d,
            ["SizeToContent"] = SizeToContent.Manual,
            ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
            ["ResizeMode"] = ResizeMode.CanResize,
            ["ShowInTaskbar"] = false,
        };

        // 창의 바탕은 여기서 정하지 않는다 — 한 번 찾아 넣은 브러시는 테마를 바꿔도 옛 색으로 굳는다.
        // 틀이 제 바탕(Backdrop)을 DynamicResource 로 칠한다.
    }
}

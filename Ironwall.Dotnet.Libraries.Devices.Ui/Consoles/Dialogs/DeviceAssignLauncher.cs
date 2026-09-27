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
/// <para>보내기 직전 재조회는 <see cref="DeviceGroupMembershipProbe"/> 로 <b>그 그룹 하나만</b> 읽는다.
/// 전량 재조회(<c>FetchAllDevicesAsync</c>)는 9단계 조회 끝에 <c>AllDevicesLoadedMessage</c> 를 뿌리는데,
/// 그것은 로그인 게이팅의 가림막 해제 · 심볼 대량 동기화 신호다 — 저장 한 번마다 울릴 것이 아니다.</para>
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
            new DeviceGroupMembershipProbe(_api, _log), _log)
        {
            // 저장하지 않은 배정을 두고 닫으면 묻는 창 — 조립기 · 부대 창과 같은 확인 창(작은 모달, 이 창을 소유자로).
            Confirm = ConfirmAsync,
        };
        vm.Initialize(groupId, groupName);

        await _windows.ShowDialogAsync(vm, null, WindowSettings()).ConfigureAwait(true);
        return vm.Saved;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        try
        {
            var prompt = new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.ConfirmPromptViewModel(title, message);
            await _windows.ShowDialogAsync(prompt, null, new Dictionary<string, object>
            {
                ["Width"] = 420.0,
                ["Height"] = 260.0,
                ["SizeToContent"] = SizeToContent.Manual,
                ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
                ["ResizeMode"] = ResizeMode.NoResize,
                ["ShowInTaskbar"] = false,
            }).ConfigureAwait(true);
            return prompt.Result;
        }
        catch (Exception ex)
        {
            // 물을 수 없으면 닫지 않는다 — 사람이 옮겨 둔 것을 말없이 버리는 쪽으로 기울지 않는다.
            _log?.Error($"[DeviceAssign] 닫기 확인 창을 열지 못했습니다 — 창을 닫지 않습니다: {ex.Message}");
            return false;
        }
    }

    private static IDictionary<string, object> WindowSettings()
    {
        var width = DialogSizeRules.WindowWidth(DialogSize.Large);
        return new Dictionary<string, object>
        {
            ["Width"] = width,
            ["Height"] = 560d,
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

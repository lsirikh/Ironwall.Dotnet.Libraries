using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Api.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 워크벤치 창 입구 — 열기만 안다
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 맵핑 워크벤치를 여는 입구.
/// </summary>
/// <remarks>
/// <para>창 뷰모델은 <b>싱글턴이 아니다</b> — 열 때마다 새로 만든다. 그래야 Draft 가 창을 넘어 살아남지 않는다.</para>
/// <para>이벤트 콘솔은 이 입구를 <c>Lazy&lt;&gt;</c> 로 받는다. 의존 하나가 컨테이너에서 안 풀려도
/// 이벤트 콘솔 전체가 못 열리는 일이 없도록, 실패하면 <b>입구만 감춘다</b>.</para>
/// </remarks>
public sealed class MappingWorkbenchLauncher : IMappingWorkbenchLauncher
{
    private const double WIDTH = 1280;      // 콘솔 기준 크기 — 레일 184 + 목록 300 + 본문 420 + 팔레트 340
    private const double HEIGHT = 760;
    private const double MIN_WIDTH = 1120;  // 레일 접힘(56) 기준 최소
    private const double MIN_HEIGHT = 640;

    private readonly IWindowManager _windows;
    private readonly IMappingWorkbenchGateway _gateway;
    private readonly IMappingDeviceSource _devices;
    private readonly IPermissionService? _permissions;
    private readonly IServerContractProbe? _probe;
    private readonly ILogService? _log;

    /// <summary>생성자.</summary>
    public MappingWorkbenchLauncher(
        IWindowManager windows,
        IMappingWorkbenchGateway gateway,
        IMappingDeviceSource devices,
        IPermissionService? permissions = null,
        IServerContractProbe? probe = null,
        ILogService? log = null)
    {
        _windows = windows;
        _gateway = gateway;
        _devices = devices;
        _permissions = permissions;
        _probe = probe;
        _log = log;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 운영 6.3.2 서버에는 이 계약의 연동 경로(<c>{id, category_device}</c> 참조 · 벌크 6종)가 없다.
    /// 그 판본에서는 <b>감춘다</b> — 비활성으로 두면 "왜 안 눌리는지" 를 물어보게 된다.
    /// </remarks>
    public bool IsAvailable
    {
        get
        {
            var contract = _probe?.Contract ?? EnumServerContract.V6_3;
            if (contract < EnumServerContract.V7_0) return false;
            return _permissions?.CanView(MappingWorkbenchViewModel.PermissionModule) ?? true;
        }
    }

    /// <inheritdoc/>
    public async Task OpenAsync()
    {
        if (!IsAvailable) return;
        try
        {
            var model = new MappingWorkbenchViewModel(_gateway, _devices, _permissions);
            await _windows.ShowDialogAsync(model, null, WindowSettings());
        }
        catch (Exception ex)
        {
            // 창 하나가 못 떠도 이벤트 콘솔은 살아 있어야 한다.
            _log?.Error($"[MappingWorkbench] 창을 열지 못했다: {ex.Message}");
        }
    }

    /// <summary>
    /// 창 설정. <b><c>Background</c> 를 정하지 않는다</b> — 뷰가 <c>DynamicResource</c> 로 스스로 칠해야
    /// 테마 전환이 따라온다.
    /// </summary>
    private static IDictionary<string, object> WindowSettings() => new Dictionary<string, object>
    {
        [nameof(Window.Width)] = WIDTH,
        [nameof(Window.Height)] = HEIGHT,
        [nameof(Window.MinWidth)] = MIN_WIDTH,
        [nameof(Window.MinHeight)] = MIN_HEIGHT,
        [nameof(Window.SizeToContent)] = SizeToContent.Manual,
        [nameof(Window.WindowStartupLocation)] = WindowStartupLocation.CenterOwner,
        [nameof(Window.ResizeMode)] = ResizeMode.CanResize,
        [nameof(Window.ShowInTaskbar)] = false,
    };
}

using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 창 입구 (N-11 FR-01)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>호스트(메뉴·셸)가 부대 콘솔을 여는 유일한 입구. 호스트는 이 인터페이스만 안다.</summary>
public interface IUnitConsoleLauncher
{
    /// <summary>서버 8.0 이상에서만 <c>true</c> — 6.3·7.0 에서는 메뉴에 내지 않는다.</summary>
    bool IsAvailable { get; }

    Task OpenAsync();
}

/// <summary>
/// 창 관리자로 라이브러리가 직접 콘솔 창을 연다 — 호스트의 메시지 계약을 늘리지 않는다(선례: <c>AssemblyLauncher</c>).
/// </summary>
/// <remarks>콘솔 뷰모델은 싱글턴이 아니다 — 열 때마다 새로 만든다. 치수는 T1 규약 1280×760.</remarks>
public sealed class UnitConsoleLauncher : IUnitConsoleLauncher
{
    private const double WIDTH = 1280;
    private const double HEIGHT = 760;

    private readonly IWindowManager _windows;
    private readonly IUnitGraphApi _units;
    private readonly IUnitDeviceApi _devices;
    private readonly INatsSetupModel? _nats;
    private readonly ILogService? _log;

    public UnitConsoleLauncher(
        IWindowManager windows,
        IUnitGraphApi units,
        IUnitDeviceApi devices,
        INatsSetupModel? nats = null,
        ILogService? log = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _nats = nats;
        _log = log;
    }

    public bool IsAvailable => _units.IsAvailable;

    public async Task OpenAsync()
    {
        if (!IsAvailable)
        {
            _log?.Warning("[UnitConsole] 서버 판본이 8.0 미만이라 부대 콘솔을 열지 않습니다.");
            return;
        }

        // GroupNats 가 '내 부대 코드' 의 유일한 출처다 — 트리에서 그 부대를 강조하는 데만 쓴다.
        var viewModel = new UnitConsoleViewModel(_units, _devices, _log, () => _nats?.GroupNats);
        await _windows.ShowDialogAsync(viewModel, null, Settings());
    }

    private static IDictionary<string, object> Settings() => new Dictionary<string, object>
    {
        ["Width"] = WIDTH,
        ["Height"] = HEIGHT,
        ["MinWidth"] = 860.0,
        ["MinHeight"] = 480.0,
        ["SizeToContent"] = SizeToContent.Manual,
        ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
        ["ResizeMode"] = ResizeMode.CanResize,
        ["ShowInTaskbar"] = false,
        // 창의 바탕은 여기서 정하지 않는다 — 한 번 찾아 넣은 브러시는 테마를 바꿔도 옛 색으로 굳는다.
    };
}

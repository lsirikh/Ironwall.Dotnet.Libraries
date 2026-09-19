using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>콘솔이 셋업 · 결선 창을 여는 입구. 콘솔 뷰모델은 이 인터페이스만 안다.</summary>
public interface IWiringLauncher
{
    /// <summary>결선을 담을 자리가 있는 서버(7.0+)에서만 입구를 낸다(PRD FR-04).</summary>
    bool IsAvailable { get; }

    /// <summary>제어기 한 대의 셋업 · 결선 창을 연다. 무언가 저장했으면 <c>true</c>.</summary>
    Task<bool> OpenAsync(IBaseDeviceModel controller);
}

/// <summary>
/// 창 관리자(<see cref="IWindowManager"/>)로 라이브러리가 직접 창을 연다 — 호스트의 메시지 계약을 늘리지 않는다(조립기 선례).
/// </summary>
public sealed class WiringLauncher : IWiringLauncher, IWiringDialogs
{
    private readonly IWindowManager _windows;
    private readonly IDeviceApiService _api;
    private readonly IDeviceProviderService _providerService;
    private readonly DeviceProvider _devices;
    private readonly DeviceQueryPolicy _policy;
    private readonly ICatalogService? _catalog;
    private readonly ILogService? _log;

    public WiringLauncher(IWindowManager windows,
                          IDeviceApiService api,
                          IDeviceProviderService providerService,
                          DeviceProvider devices,
                          DeviceQueryPolicy policy,
                          ICatalogService? catalog = null,
                          ILogService? log = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _catalog = catalog;
        _log = log;
    }

    public bool IsAvailable => _policy.IsAxisContract;

    public async Task<bool> OpenAsync(IBaseDeviceModel controller)
    {
        if (!IsAvailable || controller is null || controller.Id <= 0) return false;

        var info = new WiringControllerInfo(controller.Id, controller.DeviceNumber, Name(controller), Address(controller));
        var seeds = SeedsFor(controller.Id);
        var types = SensorTypeCodes(seeds);

        var apply = new WiringApplyService(new DeviceApiSensorGateway(_api), _providerService, _log, _policy);
        var vm = WiringViewModel.ForController(info, seeds, types, apply, this);

        return await _windows.ShowDialogAsync(vm, null, WindowSettings(1280, 820, resizable: true)) == true;
    }

    #region - Seeds -
    /// <summary>제어기에 달린 센서를 프로바이더 캐시에서 모은다 — 결선맵은 캐시만으로 그린다(WS L476).</summary>
    private IReadOnlyList<WiringSensorSeed> SeedsFor(int controllerId)
    {
        var sensors = _devices.OfType<ISensorDeviceModel>()
            .Where(s => s is IBaseDeviceModel model && ControllerIdOf(s) == controllerId)
            .Cast<IBaseDeviceModel>()
            .OrderBy(s => s.DeviceNumber)
            .ToList();

        var seeds = new List<WiringSensorSeed>(sensors.Count);
        foreach (var sensor in sensors)
        {
            var spec = sensor.Axes?.HardwareSpec?.Spec;
            seeds.Add(new WiringSensorSeed(
                sensor.Id,
                sensor.Axes?.Connection?.Channel,
                new SensorFacts(sensor.DeviceNumber, sensor.DeviceName ?? string.Empty, TypeTextOf(sensor), sensor.Location ?? string.Empty),
                WiringSpec.Read(spec),
                WiringSpec.Validate(spec)));
        }
        return seeds;
    }

    private static int ControllerIdOf(ISensorDeviceModel sensor) => sensor.Controller?.Id ?? 0;

    private static string TypeTextOf(IBaseDeviceModel sensor)
        => !string.IsNullOrWhiteSpace(sensor.TypeAxisCode) ? sensor.TypeAxisCode!
           : sensor.DeviceType == EnumDeviceType.NONE ? string.Empty
           : sensor.DeviceType.ToString();

    /// <summary>
    /// 고를 수 있는 센서 종류 — 카탈로그가 있으면 그것이 정본이고, 없으면 지금 이 제어기에 실제로 쓰인 값들이다.
    /// </summary>
    /// <remarks>값을 지어내지 않는다 — 서버가 모르는 종류를 보내면 422 이고, 그 422 가 진단 정보다.</remarks>
    private IReadOnlyList<string> SensorTypeCodes(IReadOnlyList<WiringSensorSeed> seeds)
    {
        var codes = new List<string>();
        try
        {
            var axis = _catalog?.TypeAxis(EnumDeviceCategory.Sensor);
            if (axis?.Values is { Count: > 0 } options)
                codes.AddRange(options.Where(o => !o.IsDeprecated).Select(o => o.Code).Where(c => !string.IsNullOrWhiteSpace(c)));
        }
        catch (Exception ex) { _log?.Warning($"[Wiring] 센서 종류 카탈로그를 읽지 못했습니다: {ex.Message}"); }

        foreach (var code in seeds.Select(s => s.Facts.TypeText).Where(c => !string.IsNullOrWhiteSpace(c)))
            if (!codes.Contains(code, StringComparer.Ordinal)) codes.Add(code);

        return codes;
    }

    private static string Name(IBaseDeviceModel controller)
        => string.IsNullOrWhiteSpace(controller.DeviceName) ? $"제어기 {controller.DeviceNumber}" : controller.DeviceName!;

    private static string Address(IBaseDeviceModel controller)
    {
        var ip = controller.Axes?.Connection?.IpAddress;
        return string.IsNullOrWhiteSpace(ip) ? string.Empty : ip!;
    }
    #endregion

    #region - IWiringDialogs -
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var vm = new WiringPromptViewModel(title, message);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(520, 340, resizable: false));
        return vm.Result;
    }

    public async Task<string?> AskTextAsync(string title, string label, string initial)
    {
        var vm = new WiringTextPromptViewModel(title, label, initial);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(440, 220, resizable: false));
        return vm.Result;
    }

    public async Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart)
    {
        var vm = new MakeSensorsViewModel(types, defaultType, defaultZone, existingNumbers, suggestedStart);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(520, 640, resizable: false));
        return vm.Result;
    }

    public async Task<bool> ShowPasteReportAsync(PasteReport report)
    {
        var vm = new PasteReportViewModel(report);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(620, 620, resizable: true));
        return vm.Result;
    }

    /// <summary>
    /// 클립보드는 화면 계층에서만 만진다 — 다른 앱이 쥐고 있으면 예외가 나므로 조용히 <c>null</c> 로 떨어뜨린다.
    /// </summary>
    public string? ReadClipboardText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (Exception ex)
        {
            _log?.Warning($"[Wiring] 클립보드를 읽지 못했습니다: {ex.Message}");
            return null;
        }
    }
    #endregion

    private static IDictionary<string, object> WindowSettings(double width, double height, bool resizable)
        => new Dictionary<string, object>
        {
            ["Width"] = width,
            ["Height"] = height,
            ["MinWidth"] = Math.Min(width, 460),
            ["MinHeight"] = Math.Min(height, 220),
            ["SizeToContent"] = SizeToContent.Manual,
            ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
            ["ResizeMode"] = resizable ? ResizeMode.CanResize : ResizeMode.NoResize,
            ["ShowInTaskbar"] = false,
            // 창의 바탕은 뷰가 DynamicResource 로 칠한다 — 여기서 넣으면 테마를 바꿔도 옛 색으로 굳는다.
        };
}

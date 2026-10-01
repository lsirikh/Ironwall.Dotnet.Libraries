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
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private readonly DeviceGroupProvider? _groups;
    private readonly DeviceQueryPolicy _policy;
    private readonly ICatalogService? _catalog;
    private readonly ILogService? _log;
    private readonly IEventAggregator? _eventAggregator;
    private readonly Lazy<IFenceLayoutStore>? _fenceStore;
    private readonly WiringServerIdentity? _server;

    /// <summary>펜스 구성을 불러올 때 기다리는 한도 — 로컬 DB 가 늦어도 창은 곧 열린다(없으면 제안 구성).</summary>
    public static readonly TimeSpan FENCE_LOAD_TIMEOUT = TimeSpan.FromSeconds(3);

    /// <param name="fenceStore">
    /// 펜스 구성 로컬 저장소(fence-wiring-editor FR-11 · FR-16) — <c>GMaps.Db</c> 모듈이 등록하면 Autofac 이 채운다(선택 인자).
    /// 없거나 만들다 실패하면 로컬 저장 칸만 숨고 창은 그대로 열린다(조립기 선례 · Lazy).
    /// </param>
    /// <param name="server">지금 접속한 서버(펜스 구성 열쇠의 앞쪽) — 없으면 <see cref="FenceLayoutRows.UNKNOWN_SERVER"/>.</param>
    public WiringLauncher(IWindowManager windows,
                          IDeviceApiService api,
                          IDeviceProviderService providerService,
                          DeviceProvider devices,
                          DeviceQueryPolicy policy,
                          DeviceGroupProvider? groups = null,
                          ICatalogService? catalog = null,
                          ILogService? log = null,
                          IEventAggregator? eventAggregator = null,
                          Lazy<IFenceLayoutStore>? fenceStore = null,
                          WiringServerIdentity? server = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _groups = groups;
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _catalog = catalog;
        _log = log;
        _eventAggregator = eventAggregator;
        _fenceStore = fenceStore;
        _server = server;
    }

    public bool IsAvailable => _policy.IsAxisContract;

    public async Task<bool> OpenAsync(IBaseDeviceModel controller)
    {
        if (!IsAvailable || controller is null || controller.Id <= 0) return false;

        // 종류축 원값(type_controller) 이 결선 모양을 정한다 — SmartController = 링(wiring-fence-view FR-16).
        var info = new WiringControllerInfo(controller.Id, controller.DeviceNumber, Name(controller), Address(controller), controller.TypeAxisCode);
        var seeds = SeedsFor(controller.Id);
        var types = SensorTypeCodes(seeds);

        var apply = new WiringApplyService(new DeviceApiSensorGateway(_api), _providerService, _log, _policy, _eventAggregator);
        var store = ResolveFenceStore();
        var key = new FenceLayoutKey(_server?.Key ?? FenceLayoutRows.UNKNOWN_SERVER, controller.Id);
        var load = store is null ? FenceLayoutLoadResult.NotFound : await LoadFenceLayoutAsync(store, key);
        var fence = new WiringFenceContext(load.Document, store, new IcmpPingProbe(), key, load);
        var vm = WiringViewModel.ForController(info, seeds, types, apply, this, GroupsFor(), fence, _log);
        AttachLayoutPrefs(vm);

        var closedWith = await _windows.ShowDialogAsync(vm, null, WindowSettings(1280, 820, resizable: true));
        return SavedAnything(closedWith, vm);
    }

    /// <summary>개인 표시 설정 열쇠 — 결선 창(3D 보기 : 개념도 나눔 · 속성 칸 접기).</summary>
    public const string PREFS_KEY = "devices.wiring";

    /// <summary>
    /// 나눔 비율 · 속성 칸 접힘을 사람마다 기억한다(<see cref="ConsolePrefs"/> · 이 PC 의 이 사용자). 읽거나 쓰다 실패해도 창은 기본값으로 연다.
    /// </summary>
    private void AttachLayoutPrefs(WiringViewModel vm)
    {
        try
        {
            var store = new ConsolePrefs(ConsolePrefs.DefaultPath);
            var entry = store.Get(PREFS_KEY);
            vm.UseDistancePrefs(entry.ShowDistances, show =>
            {
                entry.ShowDistances = show;
                if (!store.Save()) _log?.Warning($"[Wiring] 표시 설정 저장 실패(무시): {store.LastSaveError}");
            });
            vm.UseLayoutPrefs(entry.SplitRatio, !entry.DetailCollapsed, (ratio, open) =>
            {
                entry.SplitRatio = ratio;
                entry.DetailCollapsed = !open;
                if (!store.Save()) _log?.Warning($"[Wiring] 표시 설정 저장 실패(무시): {store.LastSaveError}");
            });
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Wiring] 표시 설정을 읽지 못했습니다(기본값으로 엽니다): {ex.Message}");
        }
    }

    /// <summary>
    /// 창이 무언가 저장했는가. 창에는 닫기 단추가 없어 OS ✕ 로 닫히고 그때 대화 결과는 <c>false</c> 다 —
    /// 대화 결과만 보던 종전에는 저장에 성공해도 콘솔이 다시 읽지 않고 "센서 · 결선을 저장했습니다." 도 뜨지 않았다.
    /// </summary>
    internal static bool SavedAnything(bool? closedWith, WiringViewModel vm) => closedWith == true || vm.HasSaved;

    #region - Fence layout (fence-wiring-editor FR-11 · FR-16) -
    /// <summary>로컬 저장소를 꺼낸다 — 등록이 없거나 만들다 실패하면 <c>null</c>(로그 한 줄 · 창은 연다).</summary>
    private IFenceLayoutStore? ResolveFenceStore()
    {
        if (_fenceStore is null) return null;
        try { return _fenceStore.Value; }
        catch (Exception ex)
        {
            _log?.Warning($"[Wiring] 펜스 구성 저장소를 쓸 수 없습니다 — 로컬 저장 없이 엽니다: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 그 서버 · 제어기의 펜스 구성 — <see cref="FENCE_LOAD_TIMEOUT"/> 안에 오지 않거나 예외면 <b>읽기 실패</b>(행이 없다는 뜻이 아니다).
    /// 창은 제안 구성으로 열고, 저장은 사람이 확인한 덮어쓰기로만 한다(없는 것으로 여기면 판 0 저장이 기존 행에 부딪혀 충돌로 오보된다).
    /// </summary>
    internal async Task<FenceLayoutLoadResult> LoadFenceLayoutAsync(IFenceLayoutStore store, FenceLayoutKey key)
    {
        using var cts = new CancellationTokenSource(FENCE_LOAD_TIMEOUT);
        try
        {
            var load = store.LoadAsync(key, cts.Token);
            var done = await Task.WhenAny(load, Task.Delay(FENCE_LOAD_TIMEOUT)).ConfigureAwait(true);
            if (done != load)
            {
                _log?.Warning($"[Wiring] {key} 펜스 구성을 {FENCE_LOAD_TIMEOUT.TotalSeconds:0}초 안에 읽지 못했습니다 — 제안 구성으로 엽니다(저장은 덮어쓰기 확인 뒤).");
                return FenceLayoutLoadResult.Failed("시간 초과");
            }
            return await load.ConfigureAwait(true) ?? FenceLayoutLoadResult.Failed("결과 없음");
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Wiring] {key} 펜스 구성 불러오기 실패 — 제안 구성으로 엽니다(저장은 덮어쓰기 확인 뒤): {ex.Message}");
            return FenceLayoutLoadResult.Failed(ex.Message);
        }
    }
    #endregion

    #region - Seeds -
    /// <summary>제어기에 달린 센서를 프로바이더 캐시에서 모은다 — 결선맵은 캐시만으로 그린다(WS L476).</summary>
    /// <summary>고를 수 있는 그룹(id + 이름) — 프로바이더가 없으면 그룹 절은 통째로 안 보인다(W2).</summary>
    private IReadOnlyList<WiringGroupInfo> GroupsFor()
    {
        if (_groups is null) return Array.Empty<WiringGroupInfo>();
        try
        {
            return _groups.OfType<IDeviceGroupModel>()
                          .Where(g => g.Id > 0)
                          .OrderBy(g => g.Name, StringComparer.CurrentCulture)
                          .Select(g => new WiringGroupInfo(g.Id, string.IsNullOrWhiteSpace(g.Name) ? $"그룹 {g.Id}" : g.Name!))
                          .ToList();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Wiring] 그룹 목록을 읽지 못했습니다: {ex.Message}");
            return Array.Empty<WiringGroupInfo>();
        }
    }

    private IReadOnlyList<WiringSensorSeed> SeedsFor(int controllerId)
    {
        // 센서의 상위는 `controller_id` 하나다(서버 스키마 필수) — 모델에서는
        // `DtoToModelHelper.ToSensorDeviceModel` 이 중첩 객체가 없어도 Id 만으로 씨를 뿌리고
        // `NavigationMappingHelper.SetupBidirectionalReferences` 가 실제 객체로 다시 잇는다.
        // 그래도 두 경로 중 하나가 비는 판본을 만나면 창이 통째로 비므로, 제어기 쪽 목록도 같이 본다(C12).
        var sensors = _devices.OfType<ISensorDeviceModel>()
            .Where(s => s is IBaseDeviceModel && ControllerIdOf(s) == controllerId)
            .Cast<IBaseDeviceModel>()
            .ToList();

        if (sensors.Count == 0)
        {
            var owner = _devices.OfType<IControllerDeviceModel>().FirstOrDefault(c => c.Id == controllerId);
            if (owner?.Devices is { Count: > 0 } children)
                sensors = children.OfType<ISensorDeviceModel>().Cast<IBaseDeviceModel>().ToList();
        }

        sensors = sensors.OrderBy(s => s.DeviceNumber).ToList();

        var seeds = new List<WiringSensorSeed>(sensors.Count);
        foreach (var sensor in sensors)
        {
            var spec = sensor.Axes?.HardwareSpec?.Spec;
            var connection = sensor.Axes?.Connection;
            seeds.Add(new WiringSensorSeed(
                sensor.Id,
                connection?.Channel,
                new SensorFacts(sensor.DeviceNumber, sensor.DeviceName ?? string.Empty, TypeTextOf(sensor), sensor.Location ?? string.Empty),
                WiringSpec.Read(spec),
                WiringSpec.Validate(spec),
                sensor.DeviceGroups?.ToList(),
                WiringSpec.ReadShape(spec),
                // 접속 축(FR-15) · 센서 링크 상태(FR-14 — 매니저가 NETWORK_INTERFACE 부품 health 를 보고하면)
                connection?.Type,
                connection?.IpAddress,
                LinkHealthOf(sensor)));
        }
        return seeds;
    }

    private static int ControllerIdOf(ISensorDeviceModel sensor) => sensor.Controller?.Id ?? 0;

    /// <summary>센서 링크 상태 — 부품 <c>NETWORK_INTERFACE</c> 의 health(없으면 <c>null</c> = 모름).</summary>
    private static string? LinkHealthOf(IBaseDeviceModel sensor)
    {
        try { return sensor.Axes?.FindStatusByType(SignalMath.NETWORK_COMPONENT)?.Health; }
        catch (Exception) { return null; }
    }

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
    /// <summary>저장 전 바뀌는 번호 표(FR-11) — 표 창으로 보인다.</summary>
    public async Task<bool> ConfirmNumberChangesAsync(string title, IReadOnlyList<NumberChange> changes, string warning, string details)
    {
        var vm = new WiringNumberChangesViewModel(title, changes, warning, details);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Medium), 620, resizable: true));
        return vm.Result;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var vm = new WiringPromptViewModel(title, message);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Small), 360, resizable: false));
        return vm.Result;
    }

    public async Task<string?> AskTextAsync(string title, string label, string initial)
    {
        var vm = new WiringTextPromptViewModel(title, label, initial);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Small), 260, resizable: false));
        return vm.Result;
    }

    public async Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart)
    {
        var vm = new MakeSensorsViewModel(types, defaultType, defaultZone, existingNumbers, suggestedStart);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Medium), 700, resizable: false));
        return vm.Result;
    }

    public async Task<bool> ShowPasteReportAsync(PasteReport report)
    {
        // 열 매핑을 바꾸면 그 자리에서 다시 읽는다 — 다시 읽을 재료(이미 있는 번호 · 기본값)를 같이 넘긴다(W7).
        var vm = new PasteReportViewModel(report, _lastPasteNumbers, _lastPasteType, _lastPasteZone);
        await _windows.ShowDialogAsync(vm, null, WindowSettings(DialogSizeRules.WindowWidth(DialogSize.Medium), 680, resizable: true));
        return vm.Result;
    }

    /// <summary>붙여넣기 보고를 다시 읽을 때 쓰는 재료 — 창이 보고를 만들 때 같이 적어 둔다.</summary>
    public void RememberPasteContext(IReadOnlyCollection<int> numbers, string defaultType, string defaultZone)
    {
        _lastPasteNumbers = numbers;
        _lastPasteType = defaultType;
        _lastPasteZone = defaultZone;
    }

    private IReadOnlyCollection<int> _lastPasteNumbers = Array.Empty<int>();
    private string _lastPasteType = string.Empty;
    private string _lastPasteZone = string.Empty;

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

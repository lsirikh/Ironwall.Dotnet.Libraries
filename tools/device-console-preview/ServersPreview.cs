using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using IronwallClock = Ironwall.Dotnet.Libraries.Base.Services.IClock;

namespace DeviceConsolePreview;

/// <summary>
/// 서버 모니터 미리보기(N-12) — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 통로 위에 띄운다. 서버 호출 0.
/// </summary>
internal sealed class ServersPreview
{
    private readonly FakeServerConsole _service;
    private readonly DeviceProvider _devices = new();

    public ServersPreview(EnumServerContract contract = EnumServerContract.V8_0) => _service = new FakeServerConsole(contract);

    public ServerMonitorViewModel ViewModel { get; private set; } = null!;
    public ServerMonitorView View { get; private set; } = null!;

    /// <summary>화면을 만든다. <paramref name="withData"/> 가 거짓이면 "아직 아무것도 없는" 첫 화면이다.</summary>
    public async Task<FrameworkElement> BuildAsync(bool withData)
    {
        if (withData) Fill();

        ViewModel = new ServerMonitorViewModel(
            new EventAggregator(), new MockLogService(), _service, _devices,
            new FrozenClock(new DateTime(2026, 9, 20, 0, 5, 0, DateTimeKind.Utc)),
            new Lazy<IServerConsoleDialogs>(() => new SilentDialogs()));

        View = new ServerMonitorView { DataContext = ViewModel };
        await ((IActivate)ViewModel).ActivateAsync();
        return View;
    }

    /// <summary>데이터가 붙은 뒤 다시 읽어 들인다(빈 화면 → 목록 화면).</summary>
    public async Task LoadAsync()
    {
        Fill();
        await ViewModel.ReloadAsync(CancellationToken.None);
    }

    public ServerRowViewModel Row(string name) => ViewModel.Rows.First(r => r.Name == name);

    /// <summary>
    /// 실제 클릭처럼 그리드를 거쳐 고른다 — <c>ViewModel.OnRowsSelected</c> 를 직접 부르면
    /// <see cref="DataGridRow.IsSelected"/> 가 한 번도 서지 않아 선택 시각효과가 찍히지 않는다.
    /// </summary>
    public void Select(ServerRowViewModel row)
    {
        var grid = Descendants<DataGrid>(View).First();
        grid.SelectedItem = row;
    }

    /// <summary>지표 이력 창 — 임계 배지를 그리지 않는 화면을 그대로 만든다.</summary>
    public async Task<FrameworkElement> MetricHistoryAsync(int serverId, string serverName)
    {
        var vm = new ServerMetricHistoryViewModel(_service, new FrozenClock(new DateTime(2026, 9, 20, 0, 5, 0, DateTimeKind.Utc)), serverId, serverName);
        var view = new ServerMetricHistoryView { DataContext = vm };
        await ((IActivate)vm).ActivateAsync();
        return view;
    }

    /// <summary>
    /// 입력 없이 끌기를 재현한다 — 실제 마우스를 건드리지 않고, 끝에는 <b>취소</b>로 놓아
    /// 서버 호출이 한 번도 나가지 않게 한다.
    /// </summary>
    /// <param name="overRow">포인터를 올려 둘 행(받는 행이면 Hover, 아니면 Blocked 가 된다).</param>
    public IDisposable BeginDrag(FrameworkElement root, ServerRowViewModel overRow, int chipIndex = 0)
    {
        var handles = Descendants<DragHandle>(root).ToList();
        var grid = Descendants<DataGrid>(root).First();
        var container = grid.ItemContainerGenerator.ContainerFromItem(overRow) as FrameworkElement;
        if (handles.Count <= chipIndex || container is null) return new DragScope(null);

        var handle = handles[chipIndex];
        var pointer = new Point();
        DragPointer.Override = relativeTo => root.TranslatePoint(pointer, (UIElement)relativeTo);

        pointer = Center(handle, root);
        handle.RaiseEvent(new DragStartedEventArgs(0, 0));

        pointer = Center(container, root);
        handle.RaiseEvent(new DragDeltaEventArgs(0, 0));
        return new DragScope(handle);
    }

    /// <summary>상세 칸을 끝까지 굴린다 — 절이 접혀 보이지 않으면 스냅숏이 상태를 증명하지 못한다.</summary>
    public static void ScrollDetailToEnd(FrameworkElement root) => Scroll(root, toEnd: true);

    /// <summary>상세 칸을 맨 위로 되돌린다 — 앞 장면의 스크롤이 다음 스냅숏을 가리지 않게.</summary>
    public static void ScrollDetailToTop(FrameworkElement root) => Scroll(root, toEnd: false);

    private static void Scroll(FrameworkElement root, bool toEnd)
    {
        root.UpdateLayout();
        foreach (var viewer in Descendants<ScrollViewer>(root).Where(v => v.ActualWidth is > 0 and < 420))
        {
            viewer.ScrollToVerticalOffset(toEnd ? viewer.ScrollableHeight : 0);
            viewer.UpdateLayout();
        }
    }

    private static Point Center(FrameworkElement element, FrameworkElement root)
        => element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), root);

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) yield return found;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private void Fill()
    {
        if (_service.Servers.Count > 0) return;

        _service.Categories.Add(new ServerCategoryOption(1, "방송", EnumServerType.SPEAKER_API, "SPEAKER_API"));
        _service.Categories.Add(new ServerCategoryOption(2, "영상", EnumServerType.NVR_API, "NVR_API"));
        _service.Categories.Add(new ServerCategoryOption(3, "프록시", EnumServerType.PROXY, "PROXY"));
        _service.Units.Add(new ServerUnitOption(4, "1대대", "unit001"));

        _service.Servers.Add(Entry(11, "방송서버-01", EnumServerType.SPEAKER_API, "NORMAL", "2026-09-20T00:03:30+00:00"));
        _service.Servers.Add(Entry(12, "방송서버-02", EnumServerType.SPEAKER_API, "WARNING", "2026-09-19T23:02:00+00:00"));
        _service.Servers.Add(Entry(21, "NVR-01", EnumServerType.NVR_API, "ERROR", "2026-09-18T02:00:00+00:00"));
        _service.Servers.Add(Entry(31, "PIDS 프록시", EnumServerType.PROXY, "NORMAL", "2026-09-19T21:05:00+00:00", withModes: true));
        _service.Servers.Add(Entry(41, "함체 게이트웨이", EnumServerType.ENCLOSURE_API, "NORMAL", "2026-09-17T10:00:00+00:00"));
        // 한 번도 보고가 없는 서버 — 7.0+ 는 status=UNKNOWN 이고 status_observed_at 이 null 이다(실제 응답 모양).
        _service.Servers.Add(Entry(51, "백업서버", EnumServerType.BACKUP, "UNKNOWN", observedAt: null));

        _service.LatestMetric = new ServerMetricDto
        {
            ServerId = 11,
            CpuUsage = 91.5,
            RamUsage = 63.2,
            RamUsedGb = 20.2,
            RamTotalGb = 32,
            DiskUsage = 78,
            DiskUsedGb = 780,
            DiskTotalGb = 1000,
            NetworkInMbps = 124.6,
            NetworkOutMbps = 12.3,
            ObservedAt = "2026-09-20T09:04:00+09:00",
            ThresholdExceeded = new JArray(JObject.FromObject(new
            {
                field = "cpu",
                value = 91.5,
                threshold = 90.0,
                direction = "HIGH",
                severity = "critical",
            })),
        };

        _service.History.AddRange(new[]
        {
            Metric(91.5, 63.2, 78, "2026-09-20T09:04:00+09:00"),
            Metric(88.0, 62.0, 78, "2026-09-20T09:03:00+09:00"),
            Metric(72.4, 60.5, 77, "2026-09-20T09:02:00+09:00"),
            Metric(65.1, 59.9, 77, "2026-09-20T09:01:00+09:00"),
        });

        foreach (var (id, name, server) in new[] { (101, "정문 스피커", 11), (102, "후문 스피커", 0), (103, "감시탑 스피커", 12) })
            _devices.CollectionEntity.Add(new SpeakerDeviceModel
            {
                Id = id,
                DeviceName = name,
                CategoryDevice = EnumDeviceCategory.Speaker,
                Server = server == 0 ? null : new ServerModel { Id = server, Name = $"방송서버-{server - 10:00}" },
            });

        // 축 계약에서는 카메라도 NVR 에 배정할 수 있다(서버 표 app/schemas/device.py:85-93).
        _devices.CollectionEntity.Add(new CameraDeviceModel { Id = 201, DeviceName = "정문 카메라", CategoryDevice = EnumDeviceCategory.Camera });

        // 현장 밀도 — GIS 실창(2026-09-27)에는 배정 칩이 약 40개였다. 칩 칸이 목록을 굶기지 않는지 여기서 본다.
        for (var i = 1; i <= 36; i++)
            _devices.CollectionEntity.Add(new CameraDeviceModel { Id = 300 + i, DeviceName = $"GOP-CAM-{i:00}", CategoryDevice = EnumDeviceCategory.Camera });
    }

    private static ServerMetricDto Metric(double cpu, double ram, double disk, string observedAt) => new()
    {
        CpuUsage = cpu,
        RamUsage = ram,
        DiskUsage = disk,
        NetworkInMbps = 120,
        NetworkOutMbps = 11,
        ObservedAt = observedAt,
    };

    private static ServerAxisView Entry(int id, string name, EnumServerType type, string status, string? observedAt, bool withModes = false)
        => new()
        {
            Id = id,
            TypeServer = type.ToString(),
            Name = name,
            IsEnable = true,
            UnitId = 4,
            Status = status,
            HasStatusKey = true,
            StatusObservedAt = observedAt,
            HasStatusObservedAtKey = true,
            // 프록시는 현장 최장 주소 모양("192.168.100.100:8100") — 주소 열이 줄임표 없이 잘리지 않는지 본다(GIS 실창 #3).
            IpAddress = type == EnumServerType.PROXY ? "192.168.100.100" : $"10.0.{id / 10}.{id % 10 + 1}",
            Port = type == EnumServerType.PROXY ? 8100 : 8000 + id,
            Hostname = $"host-{id}",
            UserName = "admin",
            HasConnectionSection = true,
            HasConfigSection = true,
            CreatedAt = observedAt ?? "2026-09-01T00:00:00+00:00",
            UpdatedAt = observedAt ?? "2026-09-01T00:00:00+00:00",
            Thresholds = JObject.FromObject(new
            {
                cpu = new { warning = 70.0, critical = 90.0 },
                ram = new { warning = 75.0, critical = 92.0 },
                disk = new { warning = 80.0, critical = 95.0 },
            }),
            Modes = withModes ? JObject.FromObject(new { operation_mode = "NORMAL", windy_mode = "wind0" }) : null,
        };

    /// <summary>끌기를 반드시 <b>취소</b>로 끝낸다 — 미리보기에서 쓰기가 나가지 않게.</summary>
    private sealed class DragScope : IDisposable
    {
        private readonly Thumb? _handle;
        public DragScope(Thumb? handle) => _handle = handle;

        public void Dispose()
        {
            _handle?.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: true));
            DragPointer.Override = null;
        }
    }

    private sealed class FrozenClock : IronwallClock
    {
        public FrozenClock(DateTime utcNow) { UtcNow = utcNow; Now = utcNow.ToLocalTime(); }
        public DateTime Now { get; }
        public DateTime UtcNow { get; }
    }

    /// <summary>미리보기에서는 아무것도 묻지 않는다 — 어차피 쓰기가 나가지 않는다.</summary>
    private sealed class SilentDialogs : IServerConsoleDialogs
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task ShowMetricHistoryAsync(int serverId, string serverName) => Task.CompletedTask;
    }

    /// <summary>서버를 부르지 않는 가짜 통로.</summary>
    private sealed class FakeServerConsole : IServerConsoleService
    {
        public FakeServerConsole(EnumServerContract contract) => Contract = contract;

        public List<ServerAxisView> Servers { get; } = new();
        public List<ServerUnitOption> Units { get; } = new();
        public List<ServerCategoryOption> Categories { get; } = new();
        public List<ServerMetricDto> History { get; } = new();
        public ServerMetricDto? LatestMetric { get; set; }

        public EnumServerContract Contract { get; }
        public bool IsUnitEra => Contract >= EnumServerContract.V8_0;
        public bool IsAxisEra => Contract >= EnumServerContract.V7_0;

        public Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
            => Task.FromResult(new ServerLoadResult(Servers.ToList(), Units.ToList(), Categories.ToList(), false, null));

        public Task<ServerAxisView?> GetAsync(int id, CancellationToken token = default)
            => Task.FromResult(Servers.FirstOrDefault(s => s.Id == id));

        public Task<ServerWriteResult> SaveAsync(int id, ServerWriteIntent intent, CancellationToken token = default)
            => Task.FromResult(new ServerWriteResult(true, "설정을 저장했습니다"));

        public Task<(ServerWriteResult Result, int NewId)> CreateAsync(
            ServerCategoryOption category, ServerWriteIntent intent, CancellationToken token = default)
            => Task.FromResult((new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), 0));

        public Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default)
            => Task.FromResult(id == 11 ? LatestMetric : null);

        public Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<ServerMetricDto>>(History.ToList());

        public Task<(ProxySettingDto? Setting, string? Note)> LegacyOperationModeAsync(int id, CancellationToken token = default)
            => Task.FromResult<(ProxySettingDto?, string?)>(
                (IsAxisEra ? null : new ProxySettingDto { ServerId = id, OperationMode = "NORMAL", WindyMode = "wind0" }, null));

        public Task<ServerWriteResult> AssignDeviceAsync(
            EnumDeviceCategory category, int deviceId, int? serverId, CancellationToken token = default)
            => throw new InvalidOperationException("미리보기는 서버에 쓰지 않는다");
    }
}

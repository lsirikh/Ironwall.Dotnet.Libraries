using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json.Linq;
using System.IO;
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
    private readonly FakeServerConsole _service = new();
    private readonly DeviceProvider _devices = new();

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

    public void Select(ServerRowViewModel row) => ViewModel.OnRowsSelected(new List<object> { row });

    /// <summary>
    /// 입력 없이 끌기를 재현해 <b>받지 않는 행</b>이 어떻게 보이는지 찍는다 — 실제 마우스를 건드리지 않고,
    /// 끝에는 <b>취소</b>로 놓아 서버 호출이 한 번도 나가지 않게 한다.
    /// </summary>
    public IDisposable BeginRefusedDrag(FrameworkElement root, ServerRowViewModel blockedRow)
    {
        var handle = Descendants<DragHandle>(root).FirstOrDefault();
        var grid = Descendants<DataGrid>(root).First();
        var container = grid.ItemContainerGenerator.ContainerFromItem(blockedRow) as FrameworkElement;
        if (handle is null || container is null) return new DragScope(null);

        var pointer = new Point();
        DragPointer.Override = relativeTo => root.TranslatePoint(pointer, (UIElement)relativeTo);

        pointer = Center(handle, root);
        handle.RaiseEvent(new DragStartedEventArgs(0, 0));

        pointer = Center(container, root);
        handle.RaiseEvent(new DragDeltaEventArgs(0, 0));
        return new DragScope(handle);
    }

    /// <summary>상세 칸을 끝까지 굴린다 — 절이 접혀 보이지 않으면 스냅숏이 상태를 증명하지 못한다.</summary>
    public static void ScrollDetailToEnd(FrameworkElement root)
    {
        root.UpdateLayout();
        foreach (var viewer in Descendants<ScrollViewer>(root).Where(v => v.ActualWidth is > 0 and < 420 && v.ScrollableHeight > 0))
        {
            viewer.ScrollToVerticalOffset(viewer.ScrollableHeight);
            viewer.UpdateLayout();
        }
    }

    /// <summary>상세 칸을 맨 위로 되돌린다 — 앞 장면의 스크롤이 다음 스냅숏을 가리지 않게.</summary>
    public static void ScrollDetailToTop(FrameworkElement root)
    {
        root.UpdateLayout();
        foreach (var viewer in Descendants<ScrollViewer>(root).Where(v => v.ActualWidth is > 0 and < 420))
        {
            viewer.ScrollToVerticalOffset(0);
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

        _service.Categories.Add(new ServerCategoryOption(1, "방송", EnumServerType.SPEAKER_API));
        _service.Categories.Add(new ServerCategoryOption(2, "영상", EnumServerType.NVR_API));
        _service.Units.Add(new ServerUnitOption(4, "1대대", "unit001"));

        _service.Servers.Add(Entry(11, "방송서버-01", EnumServerType.SPEAKER_API, "NORMAL", "2026-09-20T00:03:30+00:00"));
        _service.Servers.Add(Entry(12, "방송서버-02", EnumServerType.SPEAKER_API, "WARNING", "2026-09-19T23:02:00+00:00"));
        _service.Servers.Add(Entry(21, "NVR-01", EnumServerType.NVR_API, "ERROR", "2026-09-18T02:00:00+00:00"));
        _service.Servers.Add(Entry(31, "PIDS 프록시", EnumServerType.PROXY, "NORMAL", "2026-09-19T21:05:00+00:00"));
        _service.Servers.Add(Entry(41, "함체 게이트웨이", EnumServerType.ENCLOSURE_API, "NORMAL", "2026-09-17T10:00:00+00:00"));
        _service.Servers.Add(Entry(51, "백업서버", EnumServerType.BACKUP, "NORMAL", updatedAt: null));   // 보고 없음

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

        foreach (var (id, name, server) in new[]
        {
            (101, "정문 스피커", 11),
            (102, "후문 스피커", 0),
            (103, "감시탑 스피커", 12),
        })
        {
            _devices.CollectionEntity.Add(new SpeakerDeviceModel
            {
                Id = id,
                DeviceName = name,
                CategoryDevice = EnumDeviceCategory.Speaker,
                Server = server == 0 ? null : new ServerModel { Id = server, Name = $"방송서버-{server - 10:00}" },
            });
        }
    }

    private static ServerListEntry Entry(int id, string name, EnumServerType type, string status, string? updatedAt)
        => new(new ServerDto
        {
            Id = id,
            CategoryId = type == EnumServerType.SPEAKER_API ? 1 : 2,
            Name = name,
            Status = status,
            IpAddress = $"10.0.{id / 10}.{id % 10 + 1}",
            Port = 8000 + id,
            Hostname = $"host-{id}",
            UserName = "admin",
            UpdatedAt = updatedAt,
            CreatedAt = updatedAt,
            UnitId = 4,
            ThresholdConfig = JObject.FromObject(new
            {
                cpu = new { warning = 70.0, critical = 90.0 },
                ram = new { warning = 75.0, critical = 92.0 },
                disk = new { warning = 80.0, critical = 95.0 },
            }),
        }, type, type.ToString(), "1대대");

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
        public List<ServerListEntry> Servers { get; } = new();
        public List<ServerUnitOption> Units { get; } = new();
        public List<ServerCategoryOption> Categories { get; } = new();
        public ServerMetricDto? LatestMetric { get; set; }

        public EnumServerContract Contract => EnumServerContract.V8_0;
        public bool IsUnitEra => true;
        public bool CanReadProxySettings => false;

        public Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
            => Task.FromResult(new ServerLoadResult(Servers.ToList(), Units.ToList(), Categories.ToList(), false, null));

        public Task<ServerDto?> GetAsync(int id, CancellationToken token = default)
            => Task.FromResult(Servers.FirstOrDefault(s => s.Dto.Id == id)?.Dto);

        public Task<ServerWriteResult> SaveAsync(int id, ServerEditDraft draft, CancellationToken token = default)
            => Task.FromResult(new ServerWriteResult(true, "설정을 저장했습니다"));

        public Task<(ServerWriteResult Result, int NewId)> CreateAsync(int categoryId, ServerEditDraft draft, CancellationToken token = default)
            => Task.FromResult((new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), 0));

        public Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default)
            => Task.FromResult(id == 11 ? LatestMetric : null);

        public Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<ServerMetricDto>>(Array.Empty<ServerMetricDto>());

        public Task<(ProxySettingDto? Setting, string? Note)> OperationModeAsync(int id, CancellationToken token = default)
            => Task.FromResult<(ProxySettingDto?, string?)>((null,
                "프록시 설정 창은 없어졌습니다(410) — 운용 모드는 이 서버의 server_config 로 옮겨졌습니다."));

        public Task<ServerWriteResult> AssignSpeakerAsync(int speakerId, int serverId, CancellationToken token = default)
            => throw new InvalidOperationException("미리보기는 서버에 쓰지 않는다");
    }
}

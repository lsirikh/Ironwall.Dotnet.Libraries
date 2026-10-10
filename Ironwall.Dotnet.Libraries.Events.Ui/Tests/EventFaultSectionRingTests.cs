using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 장애 고장 구간 → 링 A/B 센서 이름(카탈로그 WIR-504 · 정적 판독 D3). 이벤트 상세가 장비 캐시의 센서 결선 자리(<c>spec.wiring</c>)로
/// "1차 4~5" 를 "Ch1(A) 쪽 4~5 → X와 Y 사이" 로도 보인다. 창 없음 · 가짜 API · 서버 없음.
/// </summary>
[Collection("IoC-Dependent")]
public class EventFaultSectionRingTests : IDisposable
{
    private const int CONTROLLER_ID = 10;

    /// <summary>행 뷰모델은 만들 때 IoC 에서 알림 통로 · 로그를 꺼낸다 — 콘솔 없이 상세만 세우는 시험도 최소 IoC 를 둔다.</summary>
    public EventFaultSectionRingTests()
    {
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? ea : type == typeof(ILogService) ? log : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
    }

    /// <summary>제어기 1대 + 센서 <paramref name="count"/>대(체인 위치 1…N 저장) + 결선 없는 센서 1대 + 다른 제어기 센서 1대.</summary>
    private static (ControllerDeviceModel Controller, List<IBaseDeviceModel> Devices) Ring(int count)
    {
        var controller = new ControllerDeviceModel { Id = CONTROLLER_ID, DeviceNumber = 1, DeviceName = "CTRL-북측-01", DeviceType = EnumDeviceType.Controller, CategoryDevice = EnumDeviceCategory.Controller };
        var other = new ControllerDeviceModel { Id = 11, DeviceNumber = 2, DeviceName = "CTRL-남측-01", DeviceType = EnumDeviceType.Controller, CategoryDevice = EnumDeviceCategory.Controller };
        var devices = new List<IBaseDeviceModel> { controller, other };
        for (var i = 1; i <= count; i++)
            devices.Add(Sensor(100 + i, $"북측 {i}구간", controller, order: i));
        devices.Add(Sensor(200, "북측 미배치", controller, order: null));                    // 결선 없음 — 링에 들지 않는다
        devices.Add(Sensor(300, "남측 1구간", other, order: 1));                             // 다른 제어기 — 섞이지 않는다
        return (controller, devices);
    }

    private static SensorDeviceModel Sensor(int id, string name, IControllerDeviceModel controller, int? order)
    {
        var spec = new JObject { ["lens"] = "벤더 값" };
        if (order is int o) spec[WiringSpec.SPEC_KEY] = new JObject { ["v"] = 3, ["line"] = 1, ["order"] = o };
        return new SensorDeviceModel
        {
            Id = id, DeviceNumber = id, DeviceName = name, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor,
            Controller = controller,
            Axes = new DeviceAxesModel { HardwareSpec = new HardwareSpecModel { Spec = spec } },
        };
    }

    private static MalfunctionEventViewModel Fault(IBaseDeviceModel device, int firstStart, int firstEnd, int secondStart, int secondEnd)
        => new(new MalfunctionEventModel
        {
            Id = 801, MessageType = EnumEventType.Fault, Status = EnumTrueFalse.False, Reason = EnumFaultType.FAULT_CONTROLLER,
            DateTime = new DateTime(2026, 10, 11, 9, 0, 0), Device = device,
            FirstStart = firstStart, FirstEnd = firstEnd, SecondStart = secondStart, SecondEnd = secondEnd,
        });

    private static IReadOnlyList<EventDetailField> FaultFields(EventDetailViewModel detail)
        => detail.Sections.Single(s => s.Title == "고장 구간").Fields;

    private static string? Field(EventDetailViewModel detail, string label)
        => FaultFields(detail).FirstOrDefault(f => f.Label == label)?.Text;

    #region - 상세 칸 (WIR-504) -
    [Fact]
    [Trait("Scenario", "WIR-504")]
    public void should_name_the_ring_sensors_on_both_ends_of_a_controller_fault_section_WIR504()
    {
        var (controller, devices) = Ring(6);
        var detail = new EventDetailViewModel(new ConsoleDetailPresenter(), ringOf: id => WiringFaultLocator.RingOf(devices, id));

        detail.Load(EventDetailKind.Malfunction, new object[] { Fault(controller, 4, 5, 4, 5) }, canEdit: true, canReport: true, actionCount: 0);

        // 결선 창의 고장 구간 예시(WIR-500)와 같은 셈 — 1차 n = A 쪽에서 n번째 · 2차 n = B 쪽에서 n번째(체인 위치 N+1−n)
        Assert.Equal("4 → 5", Field(detail, "1차 선"));
        Assert.Equal("Ch1(A) 쪽 4~5 → 북측 4구간과 북측 5구간 사이", Field(detail, "1차 센서"));
        Assert.Equal("Ch2(B) 쪽 4~5 → 북측 3구간과 북측 2구간 사이", Field(detail, "2차 센서"));
        Assert.All(FaultFields(detail), f => Assert.False(f.IsEditable));                   // 발생 기록 — 잠김
        Assert.All(FaultFields(detail), f => Assert.Equal("제어기 선 위의 지점이라 수정할 수 없습니다.", f.LockReason));
    }

    [Fact]
    [Trait("Scenario", "WIR-504")]
    public void should_use_the_ring_of_the_sensors_controller_and_say_when_the_section_is_outside_the_saved_ring_WIR504()
    {
        var (_, devices) = Ring(5);
        var sensor = devices.OfType<SensorDeviceModel>().First(s => s.Id == 102);
        var detail = new EventDetailViewModel(new ConsoleDetailPresenter(), ringOf: id => WiringFaultLocator.RingOf(devices, id));

        detail.Load(EventDetailKind.Malfunction, new object[] { Fault(sensor, 2, 2, 7, 8) }, true, true, 0);

        Assert.Equal("Ch1(A) 쪽 2번 = 북측 2구간", Field(detail, "1차 센서"));
        Assert.Equal("Ch2(B) 쪽 7~8은 저장된 결선 범위(1~5) 밖입니다", Field(detail, "2차 센서"));
    }

    [Fact]
    [Trait("Scenario", "WIR-504")]
    public void should_keep_only_the_numbers_when_the_controller_has_no_saved_ring_or_the_section_is_empty_WIR504()
    {
        var (controller, devices) = Ring(0);
        var noRing = new EventDetailViewModel(new ConsoleDetailPresenter(), ringOf: id => WiringFaultLocator.RingOf(devices, id));
        noRing.Load(EventDetailKind.Malfunction, new object[] { Fault(controller, 4, 5, 0, 0) }, true, true, 0);

        var (c2, d2) = Ring(6);
        var empty = new EventDetailViewModel(new ConsoleDetailPresenter(), ringOf: id => WiringFaultLocator.RingOf(d2, id));
        empty.Load(EventDetailKind.Malfunction, new object[] { Fault(c2, 0, 0, 0, 0) }, true, true, 0);

        var withoutSource = new EventDetailViewModel(new ConsoleDetailPresenter());
        withoutSource.Load(EventDetailKind.Malfunction, new object[] { Fault(c2, 4, 5, 4, 5) }, true, true, 0);

        Assert.Equal(new[] { "1차 선", "2차 선" }, FaultFields(noRing).Select(f => f.Label));
        Assert.Equal(new[] { "— 해당 없음", "— 해당 없음" }, FaultFields(empty).Select(f => f.Text));
        Assert.Equal(new[] { "1차 선", "2차 선" }, FaultFields(withoutSource).Select(f => f.Label));
    }

    [Fact]
    [Trait("Scenario", "WIR-504")]
    public void should_read_only_saved_primary_placements_of_that_controller_in_chain_order_WIR504()
    {
        var (_, devices) = Ring(3);
        devices.Reverse();                                                                  // 캐시 순서와 무관하게 체인 위치 순

        var ring = WiringFaultLocator.RingOf(devices, CONTROLLER_ID);

        Assert.Equal(new[] { (1, "북측 1구간"), (2, "북측 2구간"), (3, "북측 3구간") }, ring.Select(r => (r.Order, r.Name)));
        Assert.Empty(WiringFaultLocator.RingOf(devices, 0));
        Assert.Empty(WiringFaultLocator.RingOf(null, CONTROLLER_ID));
    }
    #endregion

    #region - 제품 경로: 이벤트 콘솔이 장비 캐시로 상세를 세운다 (WIR-503 · 504) -
    private EventDashboardViewModel? _console;

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private EventDashboardViewModel Console(DeviceProvider devices)
    {
        var events = new EventProvider();
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("tester");

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(EventProvider) ? events
            : type == typeof(DeviceProvider) ? devices
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();

        var providerService = new EventProviderService(log, api, devices, events);
        var console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, devices, events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, devices, events),
            new ConnectionEventPanelViewModel(ea, log, providerService, devices, events),
            new ActionEventPanelViewModel(ea, log, providerService, events),
            new EventInfoViewModel(devices, events, providerService, ea, log),
            new CameraEventInfoViewModel(events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService),
            deviceProvider: devices);
        console.UseUiThread(ImmediateUiThread.Instance);
        return _console = console;
    }

    [Fact]
    [Trait("Scenario", "WIR-504")]
    public async Task should_show_the_ring_sensor_names_in_the_event_console_detail_from_the_device_cache_WIR504()
    {
        // Arrange — 장비 캐시(DeviceProvider)에 링 6대가 있는 제어기 · 그 제어기의 1차 · 2차 장애
        var (controller, list) = Ring(6);
        var devices = new DeviceProvider();
        foreach (var d in list) devices.Add(d);
        var console = Console(devices);
        await ((IActivate)console).ActivateAsync();
        await console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        var row = Fault(controller, 4, 5, 4, 5);
        console.MalfunctionPanelViewModel.ViewModelProvider.Add(row);

        // Act — 행을 고른다(상세 칸이 선다)
        Assert.True(console.SetSelection(new object[] { row }));

        // Assert — 숫자와 함께 링 A/B 센서 이름
        Assert.Equal("Ch1(A) 쪽 4~5 → 북측 4구간과 북측 5구간 사이", Field(console.DetailView, "1차 센서"));
        Assert.Equal("Ch2(B) 쪽 4~5 → 북측 3구간과 북측 2구간 사이", Field(console.DetailView, "2차 센서"));
    }
    #endregion

    private static IEventApiService BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<DetectionEventDto>());
        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<MalfunctionEventDto>());
        mock.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<ConnectionEventDto>());
        mock.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                                               It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<ActionEventDto>());
        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });
        return mock.Object;

        static ApiListResponse<T> Empty<T>()
            => new() { Success = true, Data = new List<T>(), Pagination = new PaginationDto { Page = 1, Limit = 100, Total = 0, TotalPages = 1 } };
    }
}

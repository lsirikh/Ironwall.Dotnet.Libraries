using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 탐지·장애 신호등 상태 로직 단위 테스트 (map-topbar-trafficlight TEST-01)
                  - 상태 매트릭스: 미초기화 소등 / green 단독 / fault·detection 단독 / 동시 점등
                  - green 불변식: green = ready && fault==0 && detection==0
                  - 펄스 조건 == IsTrafficFaultOn (XAML DataTrigger 게이트)
                  - FR-A4 램프 클릭 종류 필터 토글 / FR-A5 보기 토글 EA 수신
   Created On   : 2026-08-06 · Sensorway Co., Ltd.
 ****************************************************************************/
[Collection("IoC-Dependent")]   // IoC.GetInstance는 전역 정적 — 병렬 스텁 덮어쓰기 방지
public class TrafficLightStateTests : IDisposable
{
    private readonly Mock<IEventAggregator> _mockEa = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IEventApiService> _mockApiService = new();
    private readonly Mock<IAccountModel> _mockUserModel = new();
    private readonly Mock<ISymbolEventManager> _mockSymbolEventManager = new();
    private readonly Mock<IEventQueueManager> _mockEqm = new();
    private readonly DeviceProvider _deviceProvider = new();

    public TrafficLightStateTests()
    {
        var mockSetup = new Mock<IEventSetupModel>();
        mockSetup.Setup(s => s.TimeDiscardSec).Returns(30);
        mockSetup.Setup(s => s.IsAutoEventDiscard).Returns(false);
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return _mockEa.Object;
            if (type == typeof(ILogService)) return _mockLog.Object;
            if (type == typeof(EventSetupModel)) return new EventSetupModel(mockSetup.Object);
            if (type == typeof(DeviceProvider)) return _deviceProvider;
            return null!;
        };
        IoC.GetAllInstances = type => System.Linq.Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = (type, key) => throw new InvalidOperationException("IoC is not initialized.");
        IoC.GetAllInstances = type => throw new InvalidOperationException("IoC is not initialized.");
        IoC.BuildUp = obj => throw new InvalidOperationException("IoC is not initialized.");
    }

    private EventCardListPanelViewModel CreateSut()
        => new(_mockEa.Object,
               _mockLog.Object,
               null!,   // EventProviderService — 신호등 경로 미사용
               _mockUserModel.Object,
               _mockApiService.Object,
               _mockSymbolEventManager.Object,
               _mockEqm.Object,
               new Services.ActionReportGuard());

    /// <summary>활성화(EQM 구독+초기 GetActiveCounts) 후 SUT 반환.</summary>
    private async Task<EventCardListPanelViewModel> CreateActivatedSutAsync(int detection = 0, int fault = 0)
    {
        _mockEqm.Setup(m => m.GetActiveCounts()).Returns((detection, fault));
        var sut = CreateSut();
        await ((IActivate)sut).ActivateAsync(CancellationToken.None);
        return sut;
    }

    private void RaiseCounts(int detection, int fault)
        => _mockEqm.Raise(m => m.OnActiveCountChanged += null, detection, fault);

    // ─────────────── 상태 매트릭스 ───────────────

    [Fact]
    public void should_keep_all_lamps_off_when_counts_not_ready()
    {
        // Arrange/Act — 활성화 전(첫 집계 수신 전) = 데이터 없음 상태
        var sut = CreateSut();

        // Assert — 전체 소등 + "—" 게이트(ready=false), green 점등 금지
        Assert.False(sut.IsTrafficCountsReady);
        Assert.False(sut.IsTrafficFaultOn);
        Assert.False(sut.IsTrafficDetectionOn);
        Assert.False(sut.IsTrafficGreenOn);
    }

    [Fact]
    public async Task should_turn_on_green_only_when_ready_and_both_zero()
    {
        var sut = await CreateActivatedSutAsync(detection: 0, fault: 0);

        Assert.True(sut.IsTrafficCountsReady);
        Assert.True(sut.IsTrafficGreenOn);
        Assert.False(sut.IsTrafficFaultOn);
        Assert.False(sut.IsTrafficDetectionOn);
    }

    [Fact]
    public async Task should_turn_on_fault_lamp_only_when_fault_positive_and_detection_zero()
    {
        var sut = await CreateActivatedSutAsync();

        RaiseCounts(detection: 0, fault: 3);

        // 펄스 조건 == IsTrafficFaultOn (XAML DataTrigger가 이 프로퍼티 하나로 점등+펄스 게이트)
        Assert.True(sut.IsTrafficFaultOn);
        Assert.False(sut.IsTrafficDetectionOn);
        Assert.False(sut.IsTrafficGreenOn);
        Assert.Equal(3, sut.TrafficFaultCount);
    }

    [Fact]
    public async Task should_turn_on_detection_lamp_only_when_detection_positive_and_fault_zero()
    {
        var sut = await CreateActivatedSutAsync();

        RaiseCounts(detection: 2, fault: 0);

        Assert.True(sut.IsTrafficDetectionOn);
        Assert.False(sut.IsTrafficFaultOn);
        Assert.False(sut.IsTrafficGreenOn);
        Assert.Equal(2, sut.TrafficDetectionCount);
    }

    [Fact]
    public async Task should_allow_simultaneous_lamps_when_both_counts_positive()
    {
        var sut = await CreateActivatedSutAsync();

        RaiseCounts(detection: 2, fault: 3);

        // 동시 점등 허용(장비 상태등 의미론) — green 불변식으로 초록은 구조적으로 꺼짐
        Assert.True(sut.IsTrafficFaultOn);
        Assert.True(sut.IsTrafficDetectionOn);
        Assert.False(sut.IsTrafficGreenOn);
    }

    [Fact]
    public async Task should_turn_green_back_on_when_counts_return_to_zero()
    {
        var sut = await CreateActivatedSutAsync();
        RaiseCounts(detection: 2, fault: 3);

        RaiseCounts(detection: 0, fault: 0);

        Assert.True(sut.IsTrafficGreenOn);
        Assert.False(sut.IsTrafficFaultOn);
        Assert.False(sut.IsTrafficDetectionOn);
    }

    // ─────────────── FR-A4 램프 클릭 종류 필터 ───────────────

    [Fact]
    public void should_clear_fault_filter_when_lamp_clicked_twice()
    {
        var sut = CreateSut();
        var view = CollectionViewSource.GetDefaultView(sut.ViewModelProvider);

        sut.ToggleTrafficFaultFilter();
        Assert.True(sut.IsTrafficFilterFault);
        Assert.NotNull(view.Filter);

        sut.ToggleTrafficFaultFilter();
        Assert.False(sut.IsTrafficFilterFault);
        Assert.Null(view.Filter);
    }

    [Fact]
    public void should_switch_filter_when_other_lamp_clicked()
    {
        var sut = CreateSut();

        sut.ToggleTrafficFaultFilter();
        sut.ToggleTrafficDetectionFilter();

        Assert.False(sut.IsTrafficFilterFault);
        Assert.True(sut.IsTrafficFilterDetection);
    }

    [Fact]
    public void should_filter_only_matching_card_type_when_fault_filter_active()
    {
        var sut = CreateSut();
        var detCard = CreateDetectionCard();
        var malfCard = CreateMalfunctionCard();
        sut.ViewModelProvider.Add(detCard);
        sut.ViewModelProvider.Add(malfCard);

        sut.ToggleTrafficFaultFilter();
        var view = CollectionViewSource.GetDefaultView(sut.ViewModelProvider);

        Assert.NotNull(view.Filter);
        Assert.True(view.Filter!(malfCard));
        Assert.False(view.Filter!(detCard));
    }

    // ─────────────── FR-A5 보기 토글 EA 수신 ───────────────

    [Fact]
    public async Task should_update_visibility_when_changed_message_received()
    {
        var sut = CreateSut();
        Assert.True(sut.IsTrafficLightVisible);   // 기본 표시

        await sut.HandleAsync(new TrafficLightVisibilityChangedMessage(false), CancellationToken.None);
        Assert.False(sut.IsTrafficLightVisible);

        await sut.HandleAsync(new TrafficLightVisibilityChangedMessage(true), CancellationToken.None);
        Assert.True(sut.IsTrafficLightVisible);
    }

    [Fact]
    public async Task should_clear_filter_and_unsubscribe_when_deactivated()
    {
        var sut = await CreateActivatedSutAsync();
        sut.ToggleTrafficFaultFilter();

        await ((IDeactivate)sut).DeactivateAsync(false, CancellationToken.None);

        var view = CollectionViewSource.GetDefaultView(sut.ViewModelProvider);
        Assert.False(sut.IsTrafficFilterFault);
        Assert.Null(view.Filter);

        // 구독 해제 후 EQM 통지는 무시되어야 한다(카운트 불변)
        var before = (sut.TrafficDetectionCount, sut.TrafficFaultCount);
        RaiseCounts(detection: 9, fault: 9);
        Assert.Equal(before, (sut.TrafficDetectionCount, sut.TrafficFaultCount));
    }

    // ─────────────── 헬퍼 ───────────────

    private static DetectionEventCardViewModel CreateDetectionCard(int eventId = 1)
    {
        var mockModel = new Mock<IDetectionEventModel>();
        mockModel.Setup(m => m.Id).Returns(eventId);
        mockModel.Setup(m => m.MessageType).Returns(EnumEventType.Intrusion);
        return new DetectionEventCardViewModel(mockModel.Object);
    }

    private static MalfunctionEventCardViewModel CreateMalfunctionCard(int eventId = 2)
        => new(new MalfunctionEventModel
        {
            Id = eventId,
            Reason = EnumFaultType.FAULT_FENCE,
            Device = new SensorDeviceModel
            {
                Id = 1802,
                DeviceNumber = 2,
                DeviceType = EnumDeviceType.Fence,
                Controller = new ControllerDeviceModel { Id = 1351, DeviceNumber = 1 },
            },
            MessageType = EnumEventType.Fault,
            DateTime = DateTime.Now,
        });
}

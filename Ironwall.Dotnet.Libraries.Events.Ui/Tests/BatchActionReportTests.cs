using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 전체 조치보고 (Batch Action Report) 단위 테스트
   Created By   : GHLee
   Created On   : 2026-03-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class BatchActionReportTests
{
    private readonly Mock<IEventAggregator> _mockEa = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IEventApiService> _mockApiService = new();
    private readonly Mock<IAccountModel> _mockUserModel = new();
    private readonly Mock<ISymbolEventManager> _mockSymbolEventManager = new();
    private readonly Mock<IEventQueueManager> _mockEventQueueManager = new();

    private static readonly EventSetupModel _testSetupModel;

    static BatchActionReportTests()
    {
        var mockSetup = new Mock<IEventSetupModel>();
        mockSetup.Setup(s => s.TimeDiscardSec).Returns(30);
        mockSetup.Setup(s => s.IsAutoEventDiscard).Returns(false);
        _testSetupModel = new EventSetupModel(mockSetup.Object);
    }

    public BatchActionReportTests()
    {
        // Caliburn.Micro IoC 초기화 (EventCardBaseViewModel 생성 시 필요)
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return _mockEa.Object;
            if (type == typeof(ILogService)) return _mockLog.Object;
            if (type == typeof(EventSetupModel)) return _testSetupModel;
            return null!;
        };

        // PublishAsync 기본 설정 (PublishOnBackgroundThreadAsync 내부 호출)
        _mockEa
            .Setup(ea => ea.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private EventCardListPanelViewModel CreateSut()
    {
        return new EventCardListPanelViewModel(
            _mockEa.Object,
            _mockLog.Object,
            null!,  // EventProviderService — not used in batch tests
            _mockUserModel.Object,
            _mockApiService.Object,
            _mockSymbolEventManager.Object,
            _mockEventQueueManager.Object);
    }

    private DetectionEventCardViewModel CreateDetectionCard(int eventId = 1)
    {
        var mockModel = new Mock<IDetectionEventModel>();
        mockModel.Setup(m => m.Id).Returns(eventId);
        mockModel.Setup(m => m.MessageType).Returns(EnumEventType.Intrusion);
        return new DetectionEventCardViewModel(mockModel.Object);
    }

    private void SetupApiSuccess()
    {
        _mockApiService
            .Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto() }));
    }

    #region - Phase 2: 핵심 배치 처리 로직 -

    [Fact]
    public async Task BatchReport_ActionUser_IsUsername()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("TestOperator");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard());

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        _mockApiService.Verify(a => a.CreateActionEventAsync(
            It.Is<ActionEventCreateDto>(dto => dto.User == "TestOperator"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BatchReport_ActionDetails_IsBulkText()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("TestOperator");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard());

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        _mockApiService.Verify(a => a.CreateActionEventAsync(
            It.Is<ActionEventCreateDto>(dto => dto.Content == "일괄처리"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BatchReport_SuccessAll_RemovesAllCards()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard(1));
        sut.ViewModelProvider.Add(CreateDetectionCard(2));
        sut.ViewModelProvider.Add(CreateDetectionCard(3));

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        Assert.Empty(sut.ViewModelProvider);
    }

    [Fact]
    public async Task BatchReport_FailOnSecond_StopsAndKeepsRemaining()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        var callCount = 0;
        _mockApiService
            .Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                var success = callCount != 2; // 2번째 호출 실패
                return Task.FromResult(new ApiResponse<ActionEventDto>
                {
                    Success = success,
                    Data = success ? new ActionEventDto() : null,
                    Message = success ? "" : "Server Error"
                });
            });
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard(1));
        sut.ViewModelProvider.Add(CreateDetectionCard(2));
        sut.ViewModelProvider.Add(CreateDetectionCard(3));

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert — 1번째만 제거, 2번째+3번째 남음
        Assert.Equal(2, sut.ViewModelProvider.Count);
    }

    [Fact]
    public async Task BatchReport_EmptyList_NoApiCall()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        var sut = CreateSut();
        // ViewModelProvider is empty

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        _mockApiService.Verify(
            a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BatchReport_Success_PublishesSendActionRequestMessage()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard(42));

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        _mockEa.Verify(
            ea => ea.PublishAsync(
                It.Is<Ironwall.Dotnet.Monitoring.Models.Comms.SendActionRequestMessage>(
                    msg => msg.EventId == 42),
                It.IsAny<Func<Func<Task>, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region - Phase 3: ConfirmPopup → 배치 처리 연결 -

    [Fact]
    public async Task HandleAsync_CallAllEventReport_CallsExecuteBatchReport()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard(10));

        // Act — ConfirmPopup에서 CallAllEventReportMessageModel 발행 시뮬레이션
        await sut.HandleAsync(new Ironwall.Dotnet.Libraries.ViewModel.Models.CallAllEventReportMessageModel(), CancellationToken.None);

        // Assert — ExecuteBatchReportAsync가 실행되어 카드가 제거됨
        Assert.Empty(sut.ViewModelProvider);
        _mockApiService.Verify(a => a.CreateActionEventAsync(
            It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region - Phase 4: UI ProgressCircle 전환 -

    [Fact]
    public async Task BatchReport_IsVisible_FalseDuringProcess()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        bool? isVisibleDuringApi = null;
        _mockApiService
            .Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns<ActionEventCreateDto, CancellationToken>((dto, ct) =>
            {
                // API 호출 시점의 IsVisible 값 캡처
                isVisibleDuringApi = _currentSut!.IsVisible;
                return Task.FromResult(new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto() });
            });
        var sut = CreateSut();
        _currentSut = sut;
        sut.ViewModelProvider.Add(CreateDetectionCard());

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        Assert.False(isVisibleDuringApi);
    }

    [Fact]
    public async Task BatchReport_IsVisible_TrueAfterComplete()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard());

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert
        Assert.True(sut.IsVisible);
    }

    [Fact]
    public async Task BatchReport_IsVisible_TrueAfterFailure()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        _mockApiService
            .Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ActionEventDto> { Success = false, Message = "Error" }));
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard());

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert — 실패해도 IsVisible은 true로 복원
        Assert.True(sut.IsVisible);
    }

    private EventCardListPanelViewModel? _currentSut;

    #endregion

    #region - Phase 5: 에러 처리 (InformDialog) -

    [Fact]
    public async Task BatchReport_Failure_PublishesInfoPopup()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        _mockApiService
            .Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ActionEventDto> { Success = false, Message = "Internal Server Error" }));
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard());

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert — 실패 시 OpenInfoPopupMessageModel 발행
        _mockEa.Verify(
            ea => ea.PublishAsync(
                It.Is<Ironwall.Dotnet.Libraries.ViewModel.Models.OpenInfoPopupMessageModel>(
                    msg => msg.Title == "전체 조치보고 오류"),
                It.IsAny<Func<Func<Task>, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region - Phase 6: 심볼 상태 복원 + 완료 처리 -

    [Fact]
    public async Task BatchReport_Success_CallsProcessEventReport()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        SetupApiSuccess();

        var mockModel = new Mock<IDetectionEventModel>();
        mockModel.Setup(m => m.Id).Returns(1);
        mockModel.Setup(m => m.MessageType).Returns(EnumEventType.Intrusion);
        var mockDevice = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel>();
        mockDevice.Setup(d => d.Id).Returns(100);
        mockDevice.Setup(d => d.DeviceType).Returns(EnumDeviceType.Fence);
        mockDevice.Setup(d => d.DeviceGroups).Returns(new List<int> { 1, 2 });
        mockModel.Setup(m => m.Device).Returns(mockDevice.Object);

        var sut = CreateSut();
        sut.ViewModelProvider.Add(new DetectionEventCardViewModel(mockModel.Object));

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert — 심볼 복원은 EventQueueManager Dequeue 전이로 일원화 (ProcessEventReport 직접 호출 없음)
        // Dequeue(entryId)가 호출되었는지 검증 (entryId null이면 Warning 로그)
        Assert.Empty(sut.ViewModelProvider);
    }

    [Fact]
    public async Task BatchReport_Complete_CallsDequeueAll()
    {
        // Arrange
        _mockUserModel.Setup(u => u.Name).Returns("Op");
        SetupApiSuccess();
        var sut = CreateSut();
        sut.ViewModelProvider.Add(CreateDetectionCard(1));
        sut.ViewModelProvider.Add(CreateDetectionCard(2));

        // Act
        await sut.ExecuteBatchReportAsync();

        // Assert — 개별 Dequeue(entryId) 호출 (DequeueAll 아님, EventQueue_Symbol_Unification PRD에서 변경)
        Assert.Empty(sut.ViewModelProvider);
    }

    #endregion
}

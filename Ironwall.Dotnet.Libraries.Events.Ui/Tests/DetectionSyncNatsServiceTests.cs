using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : SYNC_DETECTION(UPDATED) 수신 시 — EQM 활성 게이트 → GET 재조회 →
                  DetectionThumbnailSyncedMessage 발행 흐름 + 무시/게이트 조건 검증.
   Created By   : GHLee
   Created On   : 2026-07-31
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionSyncNatsServiceTests
{
    private const int DetectionId = 42;

    private Func<MessageArgsModel, Task>? _capturedHandler;

    private DetectionSyncNatsService CreateService(
        out Mock<IEventQueueManager> mockQueue,
        out Mock<IEventApiService> mockApi,
        out Mock<IEventAggregator> mockEa,
        bool entryActive = true,
        ApiResponse<DetectionEventDto>? getResponse = null,
        ITokenStorageService? tokenStorage = null)
    {
        var mockNats = new Mock<INatsService>();
        mockQueue = new Mock<IEventQueueManager>();
        mockApi = new Mock<IEventApiService>();
        mockEa = new Mock<IEventAggregator>();

        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);

        // EQM 활성 게이트: 활성이면 EventId+Intrusion 매칭 엔트리 반환, 아니면 null
        mockQueue.Setup(q => q.FindEntryByEventId(DetectionId, EnumEventType.Intrusion))
                 .Returns(entryActive ? new EventEntry { EventId = DetectionId, EventType = EnumEventType.Intrusion } : null);

        mockApi.Setup(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(getResponse ?? BuildSuccessResponse());

        // PublishOnCurrentThreadAsync 확장은 내부적으로 PublishAsync 호출 → null Task 방지
        mockEa.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        return new DetectionSyncNatsService(
            null, mockNats.Object, mockQueue.Object, mockApi.Object, mockEa.Object, tokenStorage);
    }

    private static ApiResponse<DetectionEventDto> BuildSuccessResponse(string thumbnail = "/api/thumbnails/rotated_42.jpg")
        => new()
        {
            Success = true,
            Data = new DetectionEventDto
            {
                Id = DetectionId,
                Detail = new DetectionDetailDto
                {
                    Thumbnail = thumbnail,
                    FrameWidth = 1920,
                    FrameHeight = 1080
                }
            }
        };

    private static MessageArgsModel SyncMessage(string action = "UPDATED", int resourceId = DetectionId, string cmd = "SYNC_DETECTION")
        => new(subject: "sensorway.unit001.all.sync.detection", subscriptionSubject: "sensorway.unit001.all.>", data: $$"""
        {
          "id": "sync-uuid",
          "cmd": "{{cmd}}",
          "from": "DBApi",
          "body": { "action": "{{action}}", "resource_id": {{resourceId}} }
        }
        """);

    [Fact]
    public async Task should_fetch_and_publish_when_updated_and_entry_active()
    {
        // Arrange
        var service = CreateService(out _, out var mockApi, out var mockEa, entryActive: true);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());
        await service.LastProcessingTask!;   // 분리된 GET+발행 완료 대기(동일 어셈블리 internal 시드)

        // Assert — GET 1회 + 회전 후 썸네일/프레임을 실은 메시지 발행 1회
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(DetectionId, It.IsAny<CancellationToken>()), Times.Once);
        // 식 트리 제약: 'is 패턴 변수' 불가 → 타입 테스트 + 캐스트로 검증(본 테스트는 이 메시지만 발행).
        mockEa.Verify(e => e.PublishAsync(
            It.Is<object>(o => o is DetectionThumbnailSyncedMessage
                            && ((DetectionThumbnailSyncedMessage)o).EventId == DetectionId
                            && ((DetectionThumbnailSyncedMessage)o).Thumbnail == "/api/thumbnails/rotated_42.jpg"
                            && ((DetectionThumbnailSyncedMessage)o).FrameWidth == 1920
                            && ((DetectionThumbnailSyncedMessage)o).FrameHeight == 1080),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task should_skip_get_when_event_not_in_queue()
    {
        // Arrange — EQM 미등록(비활성) → REST 호출 없이 no-op
        var service = CreateService(out _, out var mockApi, out _, entryActive: false);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());

        // Assert
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_ignore_when_action_is_deleted()
    {
        // Arrange — DELETED는 로그만(범위 결정) → GET 없음
        var service = CreateService(out _, out var mockApi, out _, entryActive: true);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage(action: "DELETED"));

        // Assert
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_ignore_when_cmd_is_not_sync_detection()
    {
        // Arrange — 타 cmd(DETECT)는 무시
        var service = CreateService(out _, out var mockApi, out _, entryActive: true);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage(cmd: "DETECT"));

        // Assert
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_drop_when_not_authenticated()
    {
        // Arrange — 로그아웃 상태 수신 드롭(로그인 게이팅)
        var token = new Mock<ITokenStorageService>();
        token.SetupGet(t => t.IsAuthenticated).Returns(false);
        var service = CreateService(out _, out var mockApi, out _, entryActive: true, tokenStorage: token.Object);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());

        // Assert — 유효 UPDATED라도 미인증이면 GET 없음
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_not_publish_when_get_returns_404()
    {
        // Arrange — 404(삭제 확정과 경합) → 예외 없이 발행 스킵
        var notFound = new ApiResponse<DetectionEventDto> { Success = false, StatusCode = 404 };
        var service = CreateService(out _, out var mockApi, out var mockEa, entryActive: true, getResponse: notFound);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());
        await service.LastProcessingTask!;

        // Assert — GET은 시도, 발행은 없음(예외 없음)
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(DetectionId, It.IsAny<CancellationToken>()), Times.Once);
        mockEa.Verify(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task should_apply_only_newest_when_two_updates_race_out_of_order()
    {
        // Arrange — 같은 id에 UPDATED 2건. GET 완료를 역순(신규 먼저, 오래된 것 나중)으로 제어해
        //           순서 역전 방지(seq 가드)가 오래된 응답을 폐기하는지 검증.
        var mockNats = new Mock<INatsService>();
        var mockQueue = new Mock<IEventQueueManager>();
        var mockApi = new Mock<IEventApiService>();
        var mockEa = new Mock<IEventAggregator>();

        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);
        mockQueue.Setup(q => q.FindEntryByEventId(DetectionId, EnumEventType.Intrusion))
                 .Returns(new EventEntry { EventId = DetectionId, EventType = EnumEventType.Intrusion });
        mockEa.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var tcs1 = new TaskCompletionSource<ApiResponse<DetectionEventDto>>();
        var tcs2 = new TaskCompletionSource<ApiResponse<DetectionEventDto>>();
        var call = 0;
        mockApi.Setup(a => a.GetDetectionEventByIdAsync(DetectionId, It.IsAny<CancellationToken>()))
               .Returns(() => call++ == 0 ? tcs1.Task : tcs2.Task);

        var service = new DetectionSyncNatsService(
            null, mockNats.Object, mockQueue.Object, mockApi.Object, mockEa.Object);
        await service.StartService();

        // Act — 1차(오래된 것, seq=1) 시작 → 2차(신규, seq=2) 시작
        await _capturedHandler!(SyncMessage());
        var task1 = service.LastProcessingTask!;
        await _capturedHandler!(SyncMessage());
        var task2 = service.LastProcessingTask!;

        // 완료 역순: 신규(seq=2) 먼저, 오래된 것(seq=1) 나중
        tcs2.SetResult(BuildSuccessResponse("/api/thumbnails/newer.jpg"));
        await task2;
        tcs1.SetResult(BuildSuccessResponse("/api/thumbnails/older.jpg"));
        await task1;

        // Assert — 신규만 발행, 오래된 것(seq 역전)은 폐기
        mockEa.Verify(e => e.PublishAsync(
            It.Is<object>(o => o is DetectionThumbnailSyncedMessage
                            && ((DetectionThumbnailSyncedMessage)o).Thumbnail == "/api/thumbnails/newer.jpg"),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockEa.Verify(e => e.PublishAsync(
            It.Is<object>(o => o is DetectionThumbnailSyncedMessage
                            && ((DetectionThumbnailSyncedMessage)o).Thumbnail == "/api/thumbnails/older.jpg"),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── B2: 큐에 든 탐지는 침입만이 아니다 — 접점 센서 ContactOn/Off · 사전 경보 Alert 도 같은 탐지 번호 체계(실 EventQueueManager) ──

    /// <summary>실 EQM 을 쓰는 서비스 — 게이트가 실제로 어떤 종류를 찾는지 목 설정에 기대지 않고 본다.</summary>
    private DetectionSyncNatsService CreateServiceWithRealQueue(
        EventQueueManager eqm, out Mock<IEventApiService> mockApi, out Mock<IEventAggregator> mockEa)
    {
        var mockNats = new Mock<INatsService>();
        mockApi = new Mock<IEventApiService>();
        mockEa = new Mock<IEventAggregator>();
        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);
        mockApi.Setup(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildSuccessResponse());
        mockEa.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        return new DetectionSyncNatsService(null, mockNats.Object, eqm, mockApi.Object, mockEa.Object);
    }

    [Theory]
    [InlineData(EnumEventType.ContactOn, EnumDeviceType.Contact)]
    [InlineData(EnumEventType.ContactOff, EnumDeviceType.Contact)]
    [InlineData(EnumEventType.Alert, EnumDeviceType.Fence)]
    public async Task should_fetch_and_publish_when_queued_detection_is_not_intrusion(EnumEventType eventType, EnumDeviceType deviceType)
    {
        // Arrange — DetectionNatsSyncService 는 type_event 그대로 큐에 넣는다(접점 센서의 ContactOn · Alert 등)
        var eqm = new EventQueueManager();
        eqm.Enqueue(new EventEntry { DeviceId = 3, DeviceType = deviceType, EventType = eventType, EventId = DetectionId });
        var service = CreateServiceWithRealQueue(eqm, out var mockApi, out var mockEa);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());
        if (service.LastProcessingTask is { } pending) await pending;

        // Assert — 침입이 아니어도 큐에 든 탐지면 재조회 · 카드 썸네일 갱신
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(DetectionId, It.IsAny<CancellationToken>()), Times.Once);
        mockEa.Verify(e => e.PublishAsync(
            It.Is<object>(o => o is DetectionThumbnailSyncedMessage && ((DetectionThumbnailSyncedMessage)o).EventId == DetectionId),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task should_skip_get_when_only_fault_with_same_id_is_queued()
    {
        // Arrange — 장애는 서버가 따로 세는 번호다 — 같은 숫자 42 의 장애만 있으면 탐지 재조회를 하면 안 된다
        var eqm = new EventQueueManager();
        eqm.Enqueue(new EventEntry { DeviceId = 1, DeviceType = EnumDeviceType.IpCamera, EventType = EnumEventType.Fault, EventId = DetectionId });
        var service = CreateServiceWithRealQueue(eqm, out var mockApi, out _);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());

        // Assert
        mockApi.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── B13: 순서 역전 방지 표(_latestRequest)는 처리가 끝나면 비어야 한다 — 탐지 번호마다 한 줄씩 영원히 쌓이지 않게 ──

    private static int LatestRequestCount(DetectionSyncNatsService service)
    {
        var field = typeof(DetectionSyncNatsService).GetField("_latestRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return ((System.Collections.ICollection)field.GetValue(service)!).Count;
    }

    [Fact]
    public async Task should_forget_request_generation_when_update_finished()
    {
        // Arrange
        var service = CreateService(out _, out _, out _, entryActive: true);
        await service.StartService();

        // Act — 같은 탐지의 UPDATED 3건을 하나씩 끝까지 처리
        for (var i = 0; i < 3; i++)
        {
            await _capturedHandler!(SyncMessage());
            await service.LastProcessingTask!;
        }

        // Assert
        Assert.Equal(0, LatestRequestCount(service));
    }

    [Fact]
    public async Task should_forget_request_generation_when_get_fails()
    {
        // Arrange — 404(삭제 경합)로 끝나도 표에 남지 않는다
        var notFound = new ApiResponse<DetectionEventDto> { Success = false, StatusCode = 404 };
        var service = CreateService(out _, out _, out _, entryActive: true, getResponse: notFound);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());
        await service.LastProcessingTask!;

        // Assert
        Assert.Equal(0, LatestRequestCount(service));
    }

    [Fact]
    public async Task should_drop_stale_response_when_newer_request_finished_and_another_started()
    {
        // Arrange — 표를 비운 뒤에도 순서 역전 방지가 유지되는지: A(오래된 것) 진행 중 B(신규)가 끝나 표를 비우고,
        //           곧이어 C 가 새로 시작한 다음 A 가 끝나면 A 는 버려져야 한다(세대 번호를 재사용하면 A 가 통과한다).
        var mockNats = new Mock<INatsService>();
        var mockQueue = new Mock<IEventQueueManager>();
        var mockApi = new Mock<IEventApiService>();
        var mockEa = new Mock<IEventAggregator>();
        mockNats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _capturedHandler = h);
        mockQueue.Setup(q => q.FindEntryByEventId(DetectionId, EnumEventType.Intrusion))
                 .Returns(new EventEntry { EventId = DetectionId, EventType = EnumEventType.Intrusion });
        mockEa.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        var tcsA = new TaskCompletionSource<ApiResponse<DetectionEventDto>>();
        var tcsB = new TaskCompletionSource<ApiResponse<DetectionEventDto>>();
        var tcsC = new TaskCompletionSource<ApiResponse<DetectionEventDto>>();
        var call = 0;
        mockApi.Setup(a => a.GetDetectionEventByIdAsync(DetectionId, It.IsAny<CancellationToken>()))
               .Returns(() => (call++) switch { 0 => tcsA.Task, 1 => tcsB.Task, _ => tcsC.Task });
        var service = new DetectionSyncNatsService(null, mockNats.Object, mockQueue.Object, mockApi.Object, mockEa.Object);
        await service.StartService();

        // Act
        await _capturedHandler!(SyncMessage());
        var taskA = service.LastProcessingTask!;
        await _capturedHandler!(SyncMessage());
        var taskB = service.LastProcessingTask!;
        tcsB.SetResult(BuildSuccessResponse("/api/thumbnails/b.jpg"));
        await taskB;
        await _capturedHandler!(SyncMessage());
        var taskC = service.LastProcessingTask!;
        tcsA.SetResult(BuildSuccessResponse("/api/thumbnails/a-stale.jpg"));
        await taskA;
        tcsC.SetResult(BuildSuccessResponse("/api/thumbnails/c.jpg"));
        await taskC;

        // Assert — B · C 만 발행, A(가장 오래된 것)는 버려짐
        mockEa.Verify(e => e.PublishAsync(
            It.Is<object>(o => o is DetectionThumbnailSyncedMessage
                            && ((DetectionThumbnailSyncedMessage)o).Thumbnail == "/api/thumbnails/a-stale.jpg"),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        mockEa.Verify(e => e.PublishAsync(
            It.Is<object>(o => o is DetectionThumbnailSyncedMessage
                            && ((DetectionThumbnailSyncedMessage)o).Thumbnail == "/api/thumbnails/c.jpg"),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(0, LatestRequestCount(service));
    }

    // ── EQM 활성 게이트 타입 판별(FindEntryByEventId) — 탐지/장애 독립 id 시퀀스 충돌 방어(실 EventQueueManager) ──

    [Fact]
    public void should_find_intrusion_entry_by_event_id_ignoring_same_id_fault()
    {
        // Arrange — 같은 EventId(7)의 Fault·Intrusion 공존(독립 시퀀스 충돌 재현)
        var eqm = new EventQueueManager();
        eqm.Enqueue(new EventEntry { DeviceId = 1, DeviceType = EnumDeviceType.IpCamera, EventType = EnumEventType.Fault, EventId = 7 });
        eqm.Enqueue(new EventEntry { DeviceId = 2, DeviceType = EnumDeviceType.Fence, EventType = EnumEventType.Intrusion, EventId = 7 });

        // Act
        var det = eqm.FindEntryByEventId(7, EnumEventType.Intrusion);
        var flt = eqm.FindEntryByEventId(7, EnumEventType.Fault);

        // Assert — 타입별로 정확히 분리(장애가 탐지 게이트를 오통과하지 않음)
        Assert.NotNull(det);
        Assert.Equal(EnumEventType.Intrusion, det!.EventType);
        Assert.NotNull(flt);
        Assert.Equal(EnumEventType.Fault, flt!.EventType);
    }

    [Fact]
    public void should_return_null_by_event_id_when_only_other_type_present()
    {
        // Arrange — 같은 id(7)의 Fault만 활성. 탐지 게이트는 미통과해야 함.
        var eqm = new EventQueueManager();
        eqm.Enqueue(new EventEntry { DeviceId = 1, DeviceType = EnumDeviceType.IpCamera, EventType = EnumEventType.Fault, EventId = 7 });

        // Act & Assert
        Assert.Null(eqm.FindEntryByEventId(7, EnumEventType.Intrusion));
    }
}

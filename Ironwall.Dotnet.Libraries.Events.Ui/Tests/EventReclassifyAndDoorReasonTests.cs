using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : G48 탐지 재분류(SYNC_DETECTION UPDATED 응답의 type_event ≠ 큐) · G19 함체 밖 장비의 문 사유 글자 · EnumGopCommand 새 cmd(G53).
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventReclassifyAndDoorReasonTests
{
    private Func<MessageArgsModel, Task>? _handler;
    private readonly List<object> _published = new();

    private async Task<DetectionSyncNatsService> StartAsync(Mock<IEventQueueManager> queue, Mock<IEventApiService> api)
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => _published.Add(m))
          .Returns(Task.CompletedTask);
        var service = new DetectionSyncNatsService(null, nats.Object, queue.Object, api.Object, ea.Object);
        await service.StartService();
        return service;
    }

    private static MessageArgsModel Updated(int id) => new("sensorway.unit001.all.sync.detection", "sensorway.unit001.all.>",
        $$"""{ "id": "e-{{id}}", "cmd": "SYNC_DETECTION", "body": { "action": "UPDATED", "resource_id": {{id}} } }""");

    [Theory]
    [InlineData("ContactOn", true)]
    [InlineData("Intrusion", false)]   // 같은 종류 — 재분류 아님
    [InlineData("Fault", false)]       // 장애로 바뀌는 것은 탐지 목록 일이 아니다
    public async Task 재조회_type_event_가_큐와_다르면_재분류_메시지를_REST_추가_호출_없이_낸다(string serverType, bool expected)
    {
        var queue = new Mock<IEventQueueManager>();
        queue.Setup(q => q.FindEntryByEventId(42, EnumEventType.Intrusion)).Returns(new EventEntry { EventId = 42, EventType = EnumEventType.Intrusion });
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.GetDetectionEventByIdAsync(42, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<DetectionEventDto> { Success = true, Data = new DetectionEventDto { Id = 42, TypeEvent = serverType } });
        var service = await StartAsync(queue, api);

        await _handler!(Updated(42));
        await service.LastProcessingTask!;

        Assert.Equal(expected, _published.Contains(new DetectionReclassifiedMessage(42, EnumEventType.Intrusion, EnumEventType.ContactOn)));
        api.Verify(a => a.GetDetectionEventByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── G19 ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ENCLOSURE_DOOR_OPEN", "camera", "카메라 문 열림")]
    [InlineData("ENCLOSURE_DOOR_CLOSED", "controller", "제어기 문 닫힘")]
    [InlineData("ENCLOSURE_DOOR_OPEN", "speaker", "스피커 문 열림")]
    [InlineData("ENCLOSURE_DOOR_OPEN", "enclosure", "함체 문 열림")]
    [InlineData("ENCLOSURE_DOOR_OPEN", null, "함체 문 열림")]         // 6.3 · 종류 모름 — 지금처럼
    [InlineData("ENCLOSURE_TEMP_HIGH", "camera", "함체 고온")]         // 문 사유가 아니면 그대로
    [InlineData("GATE_OPEN", "gate", "통문 열림")]
    public void 함체_밖_장비의_문_사유는_장비_종류로_적는다(string reason, string? category, string expected)
        => Assert.Equal(expected, OperationEventText.ReasonText(reason, category));

    // ── G53 열거값 ────────────────────────────────────────────────────

    [Theory]
    [InlineData("SYNC_RESYNC")]
    [InlineData("SYNC_MALFUNCTION")]
    [InlineData("SYNC_ACTION_REPORT")]
    [InlineData("SYNC_USER")]
    [InlineData("SYNC_USER_GROUP")]
    [InlineData("MIC_PAGING")]
    public void 브로커_새_cmd_는_이름으로_인식된다(string cmd)
    {
        Assert.True(Enum.TryParse<EnumGopCommand>(cmd, out var parsed));
        Assert.Equal(cmd, parsed.ToString());
    }

    [Fact]
    public void EnumGopCommand_정수는_모두_유일하다()
    {
        var values = Enum.GetValues<EnumGopCommand>().Select(v => (int)v).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }
}

[Collection("IoC-Dependent")]
public class OperationReasonCategoryWiringTests : IoCStubbedTestBase
{
    [Fact]
    public void 운영_카드와_레일_행은_서버_장비_카테고리를_넘긴다()
    {
        var model = new OperationEventDto { Id = 1, Reason = "ENCLOSURE_DOOR_OPEN", Device = new BaseDeviceDto { Id = 9, CategoryDevice = "camera" } }
            .ToOperationEventModel(null);

        Assert.Equal("camera", model.CategoryDevice);
        Assert.Equal("카메라 문 열림", new OperationEventCardViewModel(model).ReasonText);
        Assert.Equal("카메라 문 열림", new OperationEventViewModel(model).ReasonText);
    }
}

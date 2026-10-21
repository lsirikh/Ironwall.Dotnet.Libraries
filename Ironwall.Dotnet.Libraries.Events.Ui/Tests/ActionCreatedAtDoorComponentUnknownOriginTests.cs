using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : P3 정리 — G43 조치 created_at 판본 규칙 · user 1~100자 / G47 부품별 문 상태 합치기 / G49 모르는 원본(UnknownEventDto)은 종류+Id 종결을 건너뛴다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class ActionCreatedAtDoorComponentUnknownOriginTests
{
    // ── G43 ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EnumServerContract.V6_3, true)]    // 6.3.2 는 받은 값을 저장한다 — 그대로 싣는다
    [InlineData(EnumServerContract.V7_0, false)]   // 6.3.16+ · 7.0+ 는 무시하고 경고를 단다 — 비운다
    [InlineData(EnumServerContract.V8_0, false)]
    public void 조치_생성_created_at_은_7_0_미만에서만_실린다(EnumServerContract contract, bool sent)
    {
        var dto = ActionReportRules.ApplyCreatedAtPolicy(new ActionEventCreateDto { Content = "c", User = "u", FromEventId = 1 }, contract);
        var json = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal(sent, json.ContainsKey("created_at"));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("admin(1234)", true)]
    public void 보고자는_1자_이상(string user, bool ok) => Assert.Equal(ok, ActionReportRules.ValidateUser(user) is null);

    [Fact]
    public void 보고자는_100자까지()
    {
        Assert.Null(ActionReportRules.ValidateUser(new string('가', 100)));
        Assert.Contains("100자", ActionReportRules.ValidateUser(new string('가', 101)));
    }

    // ── G47 ──────────────────────────────────────────────────────────

    private readonly List<(int Id, EnumDoorState State)> _doors = new();
    private Func<MessageArgsModel, Task>? _handler;

    private async Task StartOperationAsync()
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        var symbols = new Mock<ISymbolEventManager>();
        symbols.Setup(s => s.SetDoorState(It.IsAny<int>(), It.IsAny<EnumDeviceType>(), It.IsAny<EnumDoorState>()))
               .Callback<int, EnumDeviceType, EnumDoorState>((id, _, st) => _doors.Add((id, st)));
        await new OperationEventNatsSyncService(null, nats.Object, symbols.Object).StartService();
    }

    private Task Door(string reason, string? component, int id = 31)
        => _handler!(new MessageArgsModel("sensorway.unit001.all.event.operation", "sensorway.unit001.all.>", $$"""
        { "cmd": "OPERATION_EVENT", "body": { "id": {{Guid.NewGuid().GetHashCode() & 0xffff}}, "reason": "{{reason}}",
          "device": { "id": {{id}}, "category_device": "enclosure" },
          "detail": { {{(component is null ? "" : $"\"component\": \"{component}\"")}} } } }
        """));

    [Fact]
    public async Task 부품_하나라도_열려_있으면_장비는_열림()
    {
        await StartOperationAsync();
        await Door("ENCLOSURE_DOOR_OPEN", "door_front");
        await Door("ENCLOSURE_DOOR_OPEN", "door_rear");
        await Door("ENCLOSURE_DOOR_CLOSED", "door_front");   // 뒷문이 아직 열림
        await Door("ENCLOSURE_DOOR_CLOSED", "door_rear");    // 둘 다 닫힘

        Assert.Equal(new[] { EnumDoorState.Open, EnumDoorState.Open, EnumDoorState.Open, EnumDoorState.Closed }, _doors.Select(d => d.State));
    }

    [Fact]
    public async Task 부품이_없으면_지금처럼_장비_단위()
    {
        await StartOperationAsync();
        await Door("ENCLOSURE_DOOR_OPEN", null);
        await Door("ENCLOSURE_DOOR_CLOSED", null);

        Assert.Equal(new[] { EnumDoorState.Open, EnumDoorState.Closed }, _doors.Select(d => d.State));
    }

    // ── G49 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task 모르는_원본의_조치는_종류_Id_종결을_하지_않는다()
    {
        var json = """{ "id": 77, "type_event": "Action", "from_event": { "id": 42, "type_event": "SomethingBrandNew" } }""";
        var action = JsonConvert.DeserializeObject<ActionEventDto>(json)!;
        Assert.IsType<UnknownEventDto>(action.FromEvent);
        Assert.Null(EventSyncNatsService.OriginKind(action.FromEvent));

        // SYNC_ACTION_REPORT 재조회 응답의 원본이 미상이면 원본 종류 · Id 없이 알린다(카드 목록이 아무것도 닫지 않는다).
        Func<MessageArgsModel, Task>? handler = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handler = h);
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.GetActionEventByIdAsync(77, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiResponse<ActionEventDto> { Success = true, Data = action });
        var published = new List<object>();
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => published.Add(m))
          .Returns(Task.CompletedTask);
        var service = new EventSyncNatsService(null, nats.Object, new Mock<IEventQueueManager>().Object, api.Object, ea.Object);
        await service.StartService();

        await handler!(new MessageArgsModel("s", "s", """{ "id": "u1", "cmd": "SYNC_ACTION_REPORT", "body": { "action": "UPDATED", "resource_id": 77 } }"""));
        await service.LastProcessingTask!;

        Assert.Contains(new ActionReportSyncedMessage(77, false, null, null, null), published);
        api.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

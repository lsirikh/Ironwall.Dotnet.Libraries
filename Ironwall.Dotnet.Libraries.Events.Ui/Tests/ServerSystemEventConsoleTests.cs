using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Systems;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using MaterialDesignThemes.Wpf;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : G52 서버 콘솔 시스템 이벤트 레일 · G54 서버 변경 알림 · SYSTEM_EVENT/SYNC_SYSTEM_EVENT 루프백 주입(헤드리스).
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]
public class ServerSystemEventConsoleTests
{
    private readonly Mock<IServerConsoleService> _service = new();
    private readonly Mock<ISystemEventSource> _source = new();
    private int _loads;

    public ServerSystemEventConsoleTests()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        _service.Setup(s => s.LoadAsync(It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => _loads++)
                .ReturnsAsync(new ServerLoadResult(new List<ServerAxisView>(), new List<ServerUnitOption>(), new List<ServerCategoryOption>(), false, null));
    }

    private ServerMonitorViewModel Build(bool withSource = true)
        => new(new EventAggregator(), new Mock<ILogService>().Object, _service.Object, new DeviceProvider(),
               new Mock<IClock>().Object, null, withSource ? _source.Object : null);

    private static SystemEventDto Event(int id, string severity = "WARNING", bool ack = false)
        => new() { Id = id, Severity = severity, Title = $"이벤트 {id}", IsAcknowledged = ack, CreatedAt = $"2026-10-21T10:00:{id:00}+09:00" };

    private void ListReturns(params SystemEventDto[] items)
        => _source.Setup(s => s.GetSystemEventsAsync(It.IsAny<SystemEventQuery>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ApiListResponse<SystemEventDto> { Success = true, StatusCode = 200, Data = items.ToList() });

    [Fact]
    public void 공급자가_있으면_레일에_시스템_이벤트_칸이_붙는다_없으면_예전처럼_없다()
    {
        Assert.Contains(Build().RailEntries, e => e.Key == ServerTypeCatalog.SystemEventsKey);
        Assert.DoesNotContain(Build(withSource: false).RailEntries, e => e.Key == ServerTypeCatalog.SystemEventsKey);
    }

    [Fact]
    public async Task 칸을_고르면_목록을_최신순으로_띄운다()
    {
        ListReturns(Event(1, "INFO"), Event(2, "CRITICAL"));
        var vm = Build();

        Assert.True(vm.SelectRail(ServerTypeCatalog.SystemEventsKey));
        await vm.LoadSystemEventsAsync(CancellationToken.None);

        Assert.True(vm.IsSystemEvents);
        Assert.Equal(new[] { 2, 1 }, vm.SystemEventRows.Select(r => r.Id));
        Assert.True(vm.HasSystemEventRows);
        Assert.Equal("시스템 이벤트 2건 · 미확인 2", vm.ListStatusText);
    }

    [Fact]
    public async Task 서버가_404_면_이_서버_판은_제공하지_않음()
    {
        _source.Setup(s => s.GetSystemEventsAsync(It.IsAny<SystemEventQuery>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ApiListResponse<SystemEventDto> { Success = false, StatusCode = 404 });
        var vm = Build();
        vm.SelectRail(ServerTypeCatalog.SystemEventsKey);

        await vm.LoadSystemEventsAsync(CancellationToken.None);

        Assert.Equal(ServerMonitorViewModel.SystemEventsUnsupportedText, vm.SystemEventsNote);
        Assert.False(vm.HasSystemEventRows);
    }

    [Theory]
    [InlineData(SystemEventAckOutcome.Acknowledged, "시스템 이벤트를 확인했습니다.")]
    [InlineData(SystemEventAckOutcome.AlreadyAcknowledged, "이미 확인된 시스템 이벤트입니다.")]
    public async Task 확인과_이미_확인됨은_둘_다_줄을_확인됨으로(SystemEventAckOutcome outcome, string status)
    {
        ListReturns(Event(5));
        _source.Setup(s => s.AcknowledgeSystemEventAsync(5, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        var vm = Build();
        vm.SelectRail(ServerTypeCatalog.SystemEventsKey);
        await vm.LoadSystemEventsAsync(CancellationToken.None);

        await vm.AcknowledgeSystemEventAsync(vm.SystemEventRows[0]);

        Assert.True(vm.SystemEventRows[0].IsAcknowledged);
        Assert.Equal(status, vm.StatusText);
    }

    [Fact]
    public async Task 열린_콘솔은_수신_메시지로_줄을_넣고_다시_읽고_뺀다()
    {
        ListReturns(Event(1));
        var vm = Build();
        await ((IActivate)vm).ActivateAsync();
        vm.SelectRail(ServerTypeCatalog.SystemEventsKey);
        await vm.LoadSystemEventsAsync(CancellationToken.None);

        await vm.HandleAsync(new ServerSystemEventMessage(2, Event(2, "ERROR"), false), CancellationToken.None);   // SYSTEM_EVENT 새 줄
        Assert.Equal(2, vm.SystemEventRows[0].Id);

        _source.Setup(s => s.GetSystemEventByIdAsync(1, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ApiResponse<SystemEventDto> { Success = true, Data = Event(1, ack: true) });
        await vm.HandleAsync(new ServerSystemEventMessage(1, null, false), CancellationToken.None);               // SYNC UPDATED → 다시 읽기
        Assert.True(vm.SystemEventRows.Single(r => r.Id == 1).IsAcknowledged);

        await vm.HandleAsync(new ServerSystemEventMessage(2, null, true), CancellationToken.None);                // SYNC DELETED
        Assert.DoesNotContain(vm.SystemEventRows, r => r.Id == 2);
    }

    [Fact]
    public async Task 서버_변경_알림은_열려_있을_때만_다시_읽는다()
    {
        var vm = Build();
        await vm.HandleAsync(new ServersChangedMessage(3), CancellationToken.None);
        Assert.Equal(0, _loads);   // 닫혀 있다

        await ((IActivate)vm).ActivateAsync();
        var before = _loads;
        await vm.HandleAsync(new ServersChangedMessage(3), CancellationToken.None);
        Assert.Equal(before + 1, _loads);
    }

    [Theory]
    [InlineData("INFO")]
    [InlineData("WARNING")]
    [InlineData("ERROR")]
    [InlineData("CRITICAL")]
    [InlineData("UNKNOWN")]
    public void 심각도_모양_이름은_실제_아이콘이다(string severity)
        => Assert.True(Enum.TryParse<PackIconKind>(ServerSystemEventRow.SeverityIconOf(severity), ignoreCase: false, out _));

    [Fact]
    public void 심각도마다_모양이_다르다()
        => Assert.Equal(4, new[] { "INFO", "WARNING", "ERROR", "CRITICAL" }.Select(ServerSystemEventRow.SeverityIconOf).Distinct().Count());
}

public class ServerSystemEventNatsServiceTests
{
    private Func<MessageArgsModel, Task>? _handler;
    private readonly List<object> _published = new();

    private async Task StartAsync()
    {
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => _handler = h);
        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => _published.Add(m))
          .Returns(Task.CompletedTask);
        await new ServerSystemEventNatsService(null, nats.Object, ea.Object).StartService();
    }

    private Task Inject(string json) => _handler!(new MessageArgsModel("sensorway.unit001.all.event.system", "sensorway.unit001.all.>", json));

    [Fact]
    public async Task SYSTEM_EVENT_는_본문_그대로_한_줄_메시지_7_0_과_6_3_모양()
    {
        await StartAsync();
        await Inject("""{ "id": "a", "cmd": "SYSTEM_EVENT", "body": { "id": 7, "server": { "id": 8 }, "severity": "ERROR", "is_acknowledged": false } }""");
        await Inject("""{ "id": "b", "cmd": "SYSTEM_EVENT", "body": { "id": 9, "server_id": 3, "severity": "INFO", "acknowledged": true } }""");

        var msgs = _published.OfType<ServerSystemEventMessage>().ToList();
        Assert.Equal(8, msgs[0].Event!.EffectiveServerId);
        Assert.False(msgs[0].Event!.Acknowledged);
        Assert.Equal(3, msgs[1].Event!.EffectiveServerId);
        Assert.True(msgs[1].Event!.Acknowledged);
    }

    [Fact]
    public async Task SYNC_SYSTEM_EVENT_는_다시_읽기_빼기_메시지_중복_봉투는_한_번()
    {
        await StartAsync();
        await Inject("""{ "id": "s1", "cmd": "SYNC_SYSTEM_EVENT", "body": { "action": "UPDATED", "resource_id": 7 } }""");
        await Inject("""{ "id": "s1", "cmd": "SYNC_SYSTEM_EVENT", "body": { "action": "UPDATED", "resource_id": 7 } }""");
        await Inject("""{ "id": "s2", "cmd": "SYNC_SYSTEM_EVENT", "body": { "action": "DELETED", "resource_id": 7 } }""");

        Assert.Equal(new[] { new ServerSystemEventMessage(7, null, false), new ServerSystemEventMessage(7, null, true) },
                     _published.OfType<ServerSystemEventMessage>().ToArray());
    }

    [Theory]
    [InlineData("""{ "id": "x", "cmd": "system_event", "body": { "id": 1 } }""")]
    [InlineData("""{ "id": "x", "cmd": "SYSTEM_EVENT", "m_type": "RSP", "body": { "id": 1 } }""")]
    [InlineData("""{ "id": "x", "cmd": "DETECT", "body": { "id": 1 } }""")]
    [InlineData("{ not json")]
    public async Task 남의_cmd_응답_깨진_본문은_조용히_넘긴다(string json)
    {
        await StartAsync();
        var ex = await Record.ExceptionAsync(() => Inject(json));
        Assert.Null(ex);
        Assert.Empty(_published);
    }
}

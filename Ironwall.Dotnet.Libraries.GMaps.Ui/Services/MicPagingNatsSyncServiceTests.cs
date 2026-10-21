using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Broadcast;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : G38 MIC_PAGING 루프백 봉투 주입 — 스피커 '방송 중' 켜고 끄기 · BROADCAST_STATUS 와 같은 표시 경로 · 배선.
                  (이 작업의 파일 범위가 Services/MicPaging*.cs 라 시험도 여기 둔다 — 헤드리스, 창 없음)
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MicPagingNatsSyncServiceTests
{
    private Func<MessageArgsModel, Task>? _micHandler;
    private Func<MessageArgsModel, Task>? _broadcastHandler;
    private readonly List<(int Speaker, bool On)> _shown = new();

    private async Task<MicPagingNatsSyncService> StartAsync()
    {
        var nats = new Mock<INatsService>();
        var calls = 0;
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => { if (calls++ == 0) _broadcastHandler = h; else _micHandler = h; });
        var service = new MicPagingNatsSyncService(null, nats.Object, new BroadcastStatusNatsSyncService(null, nats.Object));
        service.BroadcastStateChanged += (id, on) => _shown.Add((id, on));
        await service.StartService();
        return service;
    }

    private static MessageArgsModel Msg(string subject, string json) => new(subject, "sensorway.unit001.gis.>", json);

    [Fact]
    public async Task START_는_스피커들을_켜고_STOP_은_끈다()
    {
        await StartAsync();
        await _micHandler!(Msg("sensorway.unit001.gis.mic-paging",
            """{ "id": "m1", "cmd": "MIC_PAGING", "body": { "state": "START", "dial": "101", "speaker_ids": [3, 4], "mic_device_id": 9, "started_at": "2026-10-21T10:00:00+09:00" } }"""));
        await _micHandler!(Msg("sensorway.unit001.gis.mic-paging",
            """{ "id": "m2", "cmd": "MIC_PAGING", "body": { "state": "STOP", "speaker_ids": [3, 4], "mic_device_id": 9 } }"""));

        Assert.Equal(new[] { (3, true), (4, true), (3, false), (4, false) }, _shown);
    }

    [Fact]
    public async Task STOP_에_스피커가_없으면_같은_마이크로_켠_것을_끈다()
    {
        await StartAsync();
        await _micHandler!(Msg("s", """{ "cmd": "MIC_PAGING", "body": { "state": "START", "speaker_ids": [7], "mic_device_id": 2 } }"""));
        await _micHandler!(Msg("s", """{ "cmd": "MIC_PAGING", "body": { "state": "STOP", "mic_device_id": 2 } }"""));

        Assert.Equal(new[] { (7, true), (7, false) }, _shown);
    }

    [Fact]
    public async Task BROADCAST_STATUS_도_같은_사건으로_그대로_나온다()
    {
        await StartAsync();
        await _broadcastHandler!(Msg("sensorway.unit001.gis.broadcast-status",
            """{ "cmd": "BROADCAST_STATUS", "body": { "speaker_id": 5, "status": "ON" } }"""));

        Assert.Equal(new[] { (5, true) }, _shown);
    }

    [Theory]
    [InlineData("""{ "cmd": "mic_paging", "body": { "state": "START", "speaker_ids": [1] } }""")]
    [InlineData("""{ "cmd": "MIC_PAGING", "m_type": "RSP", "body": { "state": "START", "speaker_ids": [1] } }""")]
    [InlineData("""{ "cmd": "MIC_PAGING", "body": { "state": "MAYBE", "speaker_ids": [1] } }""")]
    [InlineData("{ broken")]
    public async Task 모르는_것은_조용히_넘긴다(string json)
    {
        await StartAsync();
        var ex = await Record.ExceptionAsync(() => _micHandler!(Msg("s", json)));
        Assert.Null(ex);
        Assert.Empty(_shown);
    }

    [Fact]
    public void 지도는_MIC_PAGING_서비스를_방송_상태_인터페이스로_받는다()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln"))) dir = dir.Parent;
        var module = File.ReadAllText(Path.Combine(dir!.FullName, "Ironwall.Dotnet.Libraries.GMaps.Ui", "Modules", "GMapUiModule.cs"));
        var at = module.IndexOf("new Services.MicPagingNatsSyncService(", StringComparison.Ordinal);
        Assert.True(at > 0);
        Assert.Contains(".As<Services.Broadcast.IBroadcastStatusNatsSyncService>()", module.Substring(at, 400));
    }
}

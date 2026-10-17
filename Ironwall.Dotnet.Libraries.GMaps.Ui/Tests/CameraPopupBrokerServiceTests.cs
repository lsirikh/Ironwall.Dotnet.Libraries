using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : 브로커 모드 CAMERA_POPUP_OPEN · POPUP_LAYOUT_GET — 헤드리스 (camera-popup-modes T-07 · FR-05~08 · FR-26~28)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 두 층으로 본다.
/// <list type="bullet">
/// <item><b>실행기 가짜</b>(<see cref="IBrokerRequestClient"/> 목) — 주제 · 낱말 · 시간 제한 · 토스트 · 합치기.</item>
/// <item><b>메모리 NVR Manager</b> — 진짜 <see cref="BrokerRequestClient"/> 가 만든 JSON 봉투를 명세 §11.5.9 대로 읽고
/// (다른 관제석이면 <b>조용히 무시</b>) RSP 봉투를 JSON 으로 돌려준다. 네트워크 없이 직렬화 → 해석 → 회신 → 해석 왕복.</item>
/// </list>
/// 운영 부대(unit001)를 쓰지 않는다 — 주제는 <c>sensorway.unit999</c>.
/// </summary>
public class CameraPopupBrokerServiceTests
{
    private const string Subject = "sensorway.unit999.nvr_manager.popup";
    private const string ClientId = "gis-7f3a9c2e41b6";

    private static CameraPopupOpenRequest Request(int cameraId = 101, int monitor = 2, int cell = 5,
                                                  CameraPopupOnOccupied onOccupied = CameraPopupOnOccupied.Replace,
                                                  string clientId = ClientId, int timeout = 3)
        => CameraPopupOpenRequest.From(new CameraPopupSettings
        {
            Mode = CameraPopupMode.Broker,
            BrokerMonitor = monitor,
            BrokerCell = cell,
            BrokerOnOccupied = onOccupied,
            BrokerResponseTimeoutSeconds = timeout,
        }, cameraId, "정문 PTZ", clientId, "operator1");

    private static (CameraPopupBrokerService Svc, Mock<IBrokerRequestClient> Client) WithClient(
        Func<BrokerRequestResult> answer, string? domain = "sensorway", string? group = "unit999")
    {
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => answer());
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PopupLayoutGetBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => answer());
        var nats = new StubPopupNatsSetupModel { DomainNats = domain, GroupNats = group };
        return (new CameraPopupBrokerService(client.Object, nats), client);
    }

    private static string OkReply(object body, string cmd = CameraPopupBrokerService.OpenCommand)
        => BrokerMessageHelper.CreateResponse(JObject.FromObject(body), "req-1", "NVRManager", cmd, "OK").ToJson();

    // ══════ 요청 모양(명세 §11.5.9 G-52) ══════

    [Fact]
    public void should_build_spec_body_with_snake_case_fields_when_cell_chosen()
    {
        // Arrange
        var body = Request(cameraId: 101, monitor: 2, cell: 5, onOccupied: CameraPopupOnOccupied.Reject).ToBody();

        // Act
        var json = JObject.Parse(body.ToBrokerRequest(CameraPopupBrokerService.OpenCommand, "GIS").ToJson());
        var b = (JObject)json["body"]!;

        // Assert — 봉투
        Assert.Equal("REQ", (string?)json["m_type"]);
        Assert.Equal("CAMERA_POPUP_OPEN", (string?)json["cmd"]);
        Assert.Equal("GIS", (string?)json["from"]);
        // body — 이름 · 형
        Assert.Equal(new[] { "target_client_id", "camera_id", "monitor", "cell", "on_occupied", "requested_by" },
                     b.Properties().Select(p => p.Name));
        Assert.Equal(JTokenType.String, b["target_client_id"]!.Type);
        Assert.Equal(JTokenType.Integer, b["camera_id"]!.Type);
        Assert.Equal(JTokenType.Integer, b["monitor"]!.Type);
        Assert.Equal(JTokenType.Integer, b["cell"]!.Type);
        Assert.Equal(ClientId, (string?)b["target_client_id"]);
        Assert.Equal(101, (int)b["camera_id"]!);
        Assert.Equal(2, (int)b["monitor"]!);
        Assert.Equal(5, (int)b["cell"]!);
        Assert.Equal("REJECT", (string?)b["on_occupied"]);
        Assert.Equal("operator1", (string?)b["requested_by"]);
    }

    [Fact]
    public void should_omit_cell_when_cell_is_auto()
    {
        var b = JObject.FromObject(Request(cell: 0).ToBody());

        Assert.Null(b.Property("cell"));
        Assert.Equal("REPLACE", (string?)b["on_occupied"]);
        Assert.Equal(2, (int)b["monitor"]!);
    }

    [Fact]
    public void should_omit_requested_by_when_operator_unknown()
    {
        var request = CameraPopupOpenRequest.From(new CameraPopupSettings(), 7, null, ClientId, "  ");

        var b = JObject.FromObject(request.ToBody());

        Assert.Null(b.Property("requested_by"));
        Assert.Equal("카메라 7", request.CameraName);
    }

    [Fact]
    public async Task should_send_to_popup_subject_with_settings_timeout_when_requested()
    {
        var (svc, client) = WithClient(() => BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p-1" })));

        await svc.RequestOpenAsync(Request(timeout: 3));

        Assert.Equal(Subject, svc.BuildSubject());
        client.Verify(c => c.RequestAsync(Subject, "CAMERA_POPUP_OPEN", It.IsAny<CameraPopupOpenBodyDto>(),
                                          TimeSpan.FromSeconds(3), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════ 토스트(FR-06) ══════

    [Fact]
    public async Task should_toast_requested_then_monitor_and_cell_when_popup_opened()
    {
        // Arrange
        var (svc, _) = WithClient(() => BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = 42 })));
        var notices = new List<CameraPopupBrokerNotice>();

        // Act
        var outcome = await svc.RequestOpenAsync(Request(monitor: 2, cell: 5), notices.Add);

        // Assert
        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, outcome.Kind);
        Assert.Equal("42", outcome.PopupId);                       // 숫자로 와도 글자로 읽는다
        Assert.Equal(new[]
        {
            new CameraPopupBrokerNotice("NVR 관제석에 정문 PTZ 팝업을 요청했습니다", true),
            new CameraPopupBrokerNotice("모니터 2 · 칸 5에 띄웠습니다", false),
        }, notices);
    }

    [Fact]
    public async Task should_toast_actual_slot_when_rsp_reports_it()
    {
        var (svc, _) = WithClient(() => BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p", monitor = 1, cell = 9 })));
        var notices = new List<CameraPopupBrokerNotice>();

        await svc.RequestOpenAsync(Request(monitor: 2, cell: 0), notices.Add);

        Assert.Equal("모니터 1 · 칸 9에 띄웠습니다", notices.Last().Text);
    }

    [Fact]
    public async Task should_toast_monitor_only_when_cell_auto_and_rsp_has_no_slot()
    {
        var (svc, _) = WithClient(() => BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p" })));
        var notices = new List<CameraPopupBrokerNotice>();

        await svc.RequestOpenAsync(Request(monitor: 3, cell: 0), notices.Add);

        Assert.Equal("모니터 3에 띄웠습니다", notices.Last().Text);
    }

    [Fact]
    public async Task should_toast_korean_reason_when_nvr_rejects()
    {
        var (svc, _) = WithClient(() => BrokerRequestResult.Fail(EnumBrokerFailure.Rejected, "칸 5가 이미 사용 중입니다",
                                                                  serverMessage: "칸 5가 이미 사용 중입니다"));
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(onOccupied: CameraPopupOnOccupied.Reject), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal(new CameraPopupBrokerNotice("NVR 팝업 거부 — 칸 5가 이미 사용 중입니다", false), notices.Last());
    }

    [Fact]
    public async Task should_toast_no_response_with_seconds_when_timeout()
    {
        var (svc, _) = WithClient(() => BrokerRequestResult.Fail(EnumBrokerFailure.NoResponse, "대상 서비스 응답 없음"));
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(timeout: 4), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.NoResponse, outcome.Kind);
        Assert.Equal("NVR Manager 응답 없음(4초) — 이 관제석에서 돌고 있는지 확인하세요", notices.Last().Text);
        Assert.False(notices.Last().IsPending);
    }

    [Fact]
    public async Task should_cut_off_and_toast_no_response_when_transport_hangs_past_timeout()
    {
        // Arrange — 전송 계층이 제 시간 제한을 지키지 않고 붙잡힌다(연결 재시도 등). 우리가 설정 초 + 여유에서 끊어야 한다(FR-26).
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .Returns(new TaskCompletionSource<BrokerRequestResult>().Task);   // 영영 안 끝난다
        var svc = new CameraPopupBrokerService(client.Object, new StubPopupNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit999" });
        var notices = new List<CameraPopupBrokerNotice>();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var outcome = await svc.RequestOpenAsync(Request(timeout: 1), notices.Add);

        // Assert
        watch.Stop();
        Assert.Equal(CameraPopupBrokerOutcomeKind.NoResponse, outcome.Kind);
        Assert.Equal("NVR Manager 응답 없음(1초) — 이 관제석에서 돌고 있는지 확인하세요", notices.Last().Text);
        Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 10);             // 1초 + 여유 1초 무렵에 끊는다
        Assert.False(svc.IsPending(101));
    }

    [Fact]
    public async Task should_not_toast_when_caller_cancels()
    {
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(BrokerRequestResult.Fail(EnumBrokerFailure.Cancelled, "요청이 취소되었습니다."));
        var svc = new CameraPopupBrokerService(client.Object, new StubPopupNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit999" });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(), notices.Add, cts.Token);

        Assert.Equal(CameraPopupBrokerOutcomeKind.Cancelled, outcome.Kind);
        Assert.Single(notices);                                         // 요청 토스트 하나뿐 — 취소는 알리지 않는다
    }

    // ══════ 보내기 전 검사 ══════

    [Fact]
    public async Task should_not_send_and_toast_when_client_id_empty()
    {
        var (svc, client) = WithClient(() => throw new InvalidOperationException("보내면 안 된다"));
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(clientId: ""), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.Invalid, outcome.Kind);
        Assert.Equal(CameraPopupBrokerToasts.NoClientId, notices.Single().Text);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task should_not_send_and_toast_when_unit_subject_missing()
    {
        var (svc, client) = WithClient(() => throw new InvalidOperationException("보내면 안 된다"), group: null);
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.Invalid, outcome.Kind);
        Assert.Equal(CameraPopupBrokerToasts.NoSubject, notices.Single().Text);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task should_not_send_when_camera_id_invalid()
    {
        var (svc, client) = WithClient(() => throw new InvalidOperationException("보내면 안 된다"));

        var outcome = await svc.RequestOpenAsync(Request(cameraId: 0));

        Assert.Equal(CameraPopupBrokerOutcomeKind.Invalid, outcome.Kind);
        client.VerifyNoOtherCalls();
    }

    // ══════ 연타 합치기(FR-06) ══════

    [Fact]
    public async Task should_coalesce_same_camera_while_request_pending()
    {
        // Arrange — 첫 요청을 붙잡아 둔다
        var gate = new TaskCompletionSource<BrokerRequestResult>();
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .Returns(gate.Task);
        var svc = new CameraPopupBrokerService(client.Object, new StubPopupNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit999" });
        var notices = new ConcurrentQueue<CameraPopupBrokerNotice>();

        // Act
        var first = svc.RequestOpenAsync(Request(cameraId: 101, timeout: 30), notices.Enqueue);
        var second = await svc.RequestOpenAsync(Request(cameraId: 101, timeout: 30), notices.Enqueue);
        var third = await svc.RequestOpenAsync(Request(cameraId: 101, timeout: 30), notices.Enqueue);
        Assert.True(svc.IsPending(101));
        gate.SetResult(BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p-9" })));
        var firstOutcome = await first;

        // Assert
        Assert.Equal(CameraPopupBrokerOutcomeKind.Coalesced, second.Kind);
        Assert.Equal(CameraPopupBrokerOutcomeKind.Coalesced, third.Kind);
        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, firstOutcome.Kind);
        client.Verify(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                          It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, notices.Count);                                 // 요청 1 + 결과 1 — 합친 연타는 알리지 않는다
        Assert.False(svc.IsPending(101));
    }

    [Fact]
    public async Task should_send_again_when_previous_request_finished()
    {
        var (svc, client) = WithClient(() => BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p" })));

        await svc.RequestOpenAsync(Request(cameraId: 5));
        await svc.RequestOpenAsync(Request(cameraId: 5));

        client.Verify(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                          It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task should_not_coalesce_different_cameras()
    {
        var gate = new TaskCompletionSource<BrokerRequestResult>();
        var client = new Mock<IBrokerRequestClient>();
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CameraPopupOpenBodyDto>(),
                                         It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
              .Returns(gate.Task);
        var svc = new CameraPopupBrokerService(client.Object, new StubPopupNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit999" });

        var a = svc.RequestOpenAsync(Request(cameraId: 1, timeout: 30));
        var b = svc.RequestOpenAsync(Request(cameraId: 2, timeout: 30));
        gate.SetResult(BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p" })));

        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, (await a).Kind);
        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, (await b).Kind);
    }

    // ══════ NATS 끊김 · 예외 경계(FR-27/28) ══════

    [Fact]
    public async Task should_toast_no_response_without_exception_when_nats_down()
    {
        // Arrange — 진짜 실행기 + 끊긴 NATS(요청이 예외를 던진다)
        var nats = new Mock<INatsService>();
        nats.Setup(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new InvalidOperationException("NATS 연결 끊김"));
        var svc = new CameraPopupBrokerService(new BrokerRequestClient(nats.Object),
                                               new StubPopupNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit999" });
        var notices = new List<CameraPopupBrokerNotice>();

        // Act
        var ex = await Record.ExceptionAsync(() => svc.RequestOpenAsync(Request(timeout: 2), notices.Add));

        // Assert
        Assert.Null(ex);
        Assert.Equal("NVR Manager 응답 없음(2초) — 이 관제석에서 돌고 있는지 확인하세요", notices.Last().Text);
        Assert.False(svc.IsPending(101));
    }

    [Fact]
    public async Task should_toast_failure_without_exception_when_client_throws()
    {
        var (svc, _) = WithClient(() => throw new InvalidOperationException("boom"));
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(CameraPopupBrokerToasts.Failed, notices.Last().Text);
        Assert.False(svc.IsPending(101));
    }

    [Fact]
    public async Task should_survive_when_toast_callback_throws()
    {
        var (svc, _) = WithClient(() => BrokerRequestResult.Ok("OK", "r", OkReply(new { popup_id = "p" })));

        var outcome = await svc.RequestOpenAsync(Request(), _ => throw new InvalidOperationException("UI gone"));

        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, outcome.Kind);
    }

    // ══════ 메모리 NVR Manager 왕복(진짜 BrokerRequestClient · JSON) ══════

    [Fact]
    public async Task should_round_trip_open_through_real_envelope_when_nvr_manager_answers()
    {
        // Arrange
        var nvr = new InMemoryNvrManager(ClientId);
        var svc = nvr.CreateService();
        var notices = new List<CameraPopupBrokerNotice>();

        // Act
        var outcome = await svc.RequestOpenAsync(Request(cameraId: 101, monitor: 2, cell: 5), notices.Add);

        // Assert — NVR 이 받은 것
        var received = nvr.Received.Single();
        Assert.Equal(Subject, received.Subject);
        Assert.Equal("REQ", (string?)received.Json["m_type"]);
        Assert.Equal("CAMERA_POPUP_OPEN", (string?)received.Json["cmd"]);
        Assert.Equal(101, (int)received.Json["body"]!["camera_id"]!);
        // GIS 가 받은 것
        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, outcome.Kind);
        Assert.Equal("popup-1", outcome.PopupId);
        Assert.Equal("모니터 2 · 칸 5에 띄웠습니다", notices.Last().Text);
    }

    [Fact]
    public async Task should_round_trip_korean_reject_reason_when_cell_occupied()
    {
        var nvr = new InMemoryNvrManager(ClientId) { OccupiedCells = { (2, 5) } };
        var svc = nvr.CreateService();
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(monitor: 2, cell: 5, onOccupied: CameraPopupOnOccupied.Reject), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal("NVR 팝업 거부 — 모니터 2 칸 5 에 이미 영상이 있습니다", notices.Last().Text);
    }

    [Fact]
    public async Task should_time_out_when_another_station_is_targeted()
    {
        // NVR Manager 는 target_client_id 가 자기 값과 다르면 조용히 무시한다(§11.5.9) → 요청자는 무응답.
        var nvr = new InMemoryNvrManager("gis-other-station");
        var svc = nvr.CreateService();
        var notices = new List<CameraPopupBrokerNotice>();

        var outcome = await svc.RequestOpenAsync(Request(timeout: 1), notices.Add);

        Assert.Equal(CameraPopupBrokerOutcomeKind.NoResponse, outcome.Kind);
        Assert.Single(nvr.Received);
        Assert.Contains("응답 없음(1초)", notices.Last().Text);
    }

    [Fact]
    public async Task should_round_trip_layout_monitors_when_layout_requested()
    {
        var nvr = new InMemoryNvrManager(ClientId);
        var svc = nvr.CreateService();

        var layout = await svc.GetLayoutAsync(ClientId, 3);

        Assert.True(layout.Success);
        Assert.Equal(new[]
        {
            new NvrPopupMonitor(1, true, 1920, 1080),
            new NvrPopupMonitor(2, false, 2560, 1440),
        }, layout.Monitors);
        Assert.Equal(1, layout.DefaultMonitor);
        Assert.Equal(5, layout.DefaultCell);
        var sent = nvr.Received.Single().Json;
        Assert.Equal("POPUP_LAYOUT_GET", (string?)sent["cmd"]);
        Assert.Equal(new[] { "target_client_id" }, ((JObject)sent["body"]!).Properties().Select(p => p.Name));
    }

    [Fact]
    public async Task should_fail_layout_with_reason_when_no_response()
    {
        var nvr = new InMemoryNvrManager("gis-other-station");
        var svc = nvr.CreateService();

        var layout = await svc.GetLayoutAsync(ClientId, 1);

        Assert.False(layout.Success);
        Assert.Equal("NVR Manager 응답 없음(1초) — 이 관제석에서 돌고 있는지 확인하세요", layout.Message);
    }

    [Fact]
    public async Task should_fail_layout_without_sending_when_client_id_empty()
    {
        var nvr = new InMemoryNvrManager(ClientId);

        var layout = await nvr.CreateService().GetLayoutAsync("", 1);

        Assert.False(layout.Success);
        Assert.Empty(nvr.Received);
    }

    /// <summary>
    /// 명세 §11.5.9 대로 도는 메모리 NVR Manager. 받은 JSON 을 <see cref="JObject"/> 로 읽고(우리 DTO 를 쓰지 않는다),
    /// 대상이 아니면 무응답(null — 실행기는 타임아웃과 같게 본다), 대상이면 RSP 봉투 JSON 을 돌려준다.
    /// </summary>
    private sealed class InMemoryNvrManager
    {
        private readonly string _clientId;
        private int _popupSeq;

        public InMemoryNvrManager(string clientId) => _clientId = clientId;

        public List<(string Subject, JObject Json)> Received { get; } = new();
        public HashSet<(int Monitor, int Cell)> OccupiedCells { get; } = new();

        public CameraPopupBrokerService CreateService()
        {
            var nats = new Mock<INatsService>();
            nats.Setup(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns((string subject, string json, TimeSpan? _) => Task.FromResult(Respond(subject, json)));
            return new CameraPopupBrokerService(new BrokerRequestClient(nats.Object),
                                                new StubPopupNatsSetupModel { DomainNats = "sensorway", GroupNats = "unit999" });
        }

        private string? Respond(string subject, string json)
        {
            var req = JObject.Parse(json);
            lock (Received) Received.Add((subject, req));
            if (subject != Subject || (string?)req["m_type"] != "REQ") return null;
            var body = req["body"] as JObject;
            if ((string?)body?["target_client_id"] != _clientId) return null;   // 다른 관제석 — 조용히 무시

            return (string?)req["cmd"] switch
            {
                "CAMERA_POPUP_OPEN" => Open(req, body!),
                "POPUP_LAYOUT_GET" => Rsp(req, true, "OK", new JObject
                {
                    ["monitors"] = new JArray
                    {
                        new JObject { ["index"] = 2, ["is_primary"] = false, ["width"] = 2560, ["height"] = 1440 },
                        new JObject { ["index"] = 1, ["is_primary"] = true, ["width"] = 1920, ["height"] = 1080 },
                    },
                    ["default_slot"] = new JObject { ["monitor"] = 1, ["cell"] = 5 },
                }),
                _ => Rsp(req, false, $"Unknown command: {req["cmd"]}", null),
            };
        }

        private string Open(JObject req, JObject body)
        {
            var monitor = (int?)body["monitor"] ?? 1;
            var cell = (int?)body["cell"] ?? 1;
            var reject = (string?)body["on_occupied"] == "REJECT";
            if (reject && OccupiedCells.Contains((monitor, cell)))
                return Rsp(req, false, $"모니터 {monitor} 칸 {cell} 에 이미 영상이 있습니다", null);
            OccupiedCells.Add((monitor, cell));
            return Rsp(req, true, "OK", new JObject { ["popup_id"] = $"popup-{++_popupSeq}" });
        }

        private static string Rsp(JObject req, bool success, string message, JObject? body) => new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["m_type"] = "RSP",
            ["cmd"] = req["cmd"],
            ["from"] = "NVRManager",
            ["body"] = (JToken?)body ?? JValue.CreateNull(),
            ["req_id"] = req["id"],
            ["success"] = success,
            ["message"] = message,
            ["created"] = "2026-09-30T10:00:00.000000+09:00",
        }.ToString(Newtonsoft.Json.Formatting.None);
    }
}

/// <summary>테스트용 INatsSetupModel 스텁(부대 unit999 — 운영 부대 금지).</summary>
file class StubPopupNatsSetupModel : INatsSetupModel
{
    public string IpAddressNats { get; set; } = "127.0.0.1";
    public int PortNats { get; set; } = 4222;
    public string? DefaultSubjectNats { get; set; }
    public string? DomainNats { get; set; }
    public string? GroupNats { get; set; }
    public string? SubsystemNats { get; set; }
    public string? UsernameNats { get; set; }
    public string? PasswordNats { get; set; }
    public int ConnectionTimeoutNats { get; set; } = 5000;
    public string EffectiveSubject => $"{DomainNats}.{GroupNats}.{SubsystemNats}.>";
}

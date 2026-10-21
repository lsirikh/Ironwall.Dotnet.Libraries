using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Systems;
using Newtonsoft.Json;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;

/****************************************************************************
   Purpose      : G52 — 시스템 이벤트 REST(가짜 응답). 7.0 · 6.3 모양 읽기 · 새 경로만 쓰기 · 확인 충돌 409/400 = 이미 확인됨 · 404 = 이 판은 제공 안 함.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class SystemEventApiServiceTests
{
    private const string BaseUrl = "https://localhost:8000/api";
    private readonly EventCapturingApiService _http = new();

    private SystemEventApiService Service() => new(new LogService(), _http, new ApiSetupModel { Url = BaseUrl });

    [Fact]
    public async Task 목록은_새_경로_api_system_events_를_쓴다()
    {
        _http.ResponseJson = """
        { "success": true, "data": [
            { "id": 10, "server": { "id": 8, "category_server": "PROXY" }, "server_description": "PROXY-1 (192.168.9.201)",
              "type_event": "RESOURCE_THRESHOLD", "severity": "CRITICAL", "title": "CPU 임계치 초과", "is_acknowledged": false,
              "acknowledged_by": null, "created_at": "2026-09-12T12:15:49.133817+09:00" } ],
          "pagination": { "page": 1, "limit": 20, "total": 1, "total_pages": 1 } }
        """;

        var res = await Service().GetSystemEventsAsync(new SystemEventQuery(ServerId: 8));

        Assert.Equal("GET", _http.Method);
        Assert.Equal($"{BaseUrl}/system-events", _http.Endpoint);   // 옛 /servers/{id}/system-events 아님
        var item = Assert.Single(res.Data!);
        Assert.Equal(8, item.EffectiveServerId);
        Assert.Equal("PROXY", item.Server!.CategoryServer);
        Assert.False(item.Acknowledged);
    }

    [Fact]
    public void 옛_6_3_모양도_읽는다()
    {
        var dto = JsonConvert.DeserializeObject<SystemEventDto>(
            """{ "id": 3, "server_id": 5, "type_event": "SERVER_ERROR", "severity": "ERROR", "title": "t", "acknowledged": true }""")!;

        Assert.Equal(5, dto.EffectiveServerId);
        Assert.True(dto.Acknowledged);
        Assert.Null(dto.Server);
    }

    [Fact]
    public void 전역_이벤트는_서버가_null_이다()
    {
        var dto = JsonConvert.DeserializeObject<SystemEventDto>(
            """{ "id": 4, "server": null, "server_description": null, "type_event": "SECURITY_ALERT", "severity": "WARNING", "is_acknowledged": true }""")!;
        Assert.Null(dto.EffectiveServerId);
        Assert.True(dto.Acknowledged);
    }

    [Fact]
    public async Task 목록_404_는_상태코드_그대로_돌려준다()
    {
        _http.ResponseStatus = HttpStatusCode.NotFound;
        _http.ResponseJson = """{ "success": false, "error": { "code": "NOT_FOUND", "message": "Not Found" } }""";

        var res = await Service().GetSystemEventsAsync(new SystemEventQuery());

        Assert.False(res.Success);
        Assert.Equal(404, res.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{ "success": true, "data": { "id": 11, "is_acknowledged": true } }""", SystemEventAckOutcome.Acknowledged)]
    [InlineData(HttpStatusCode.Conflict, """{ "success": false, "error": { "code": "CONFLICT", "message": "System event 11 is already acknowledged" } }""", SystemEventAckOutcome.AlreadyAcknowledged)]
    [InlineData(HttpStatusCode.BadRequest, """{ "success": false, "error": { "code": "BAD_REQUEST", "message": "already acknowledged" } }""", SystemEventAckOutcome.AlreadyAcknowledged)]
    [InlineData(HttpStatusCode.NotFound, """{ "success": false, "error": { "code": "NOT_FOUND", "message": "Not Found" } }""", SystemEventAckOutcome.NotSupported)]
    [InlineData(HttpStatusCode.Forbidden, """{ "success": false, "error": { "code": "FORBIDDEN", "message": "no" } }""", SystemEventAckOutcome.Failed)]
    public async Task 확인은_409_와_400_을_둘_다_이미_확인됨으로_본다(HttpStatusCode status, string body, SystemEventAckOutcome expected)
    {
        _http.ResponseStatus = status;
        _http.ResponseJson = body;

        var outcome = await Service().AcknowledgeSystemEventAsync(11, "admin");

        Assert.Equal(expected, outcome);
        Assert.Equal("POST", _http.Method);
        Assert.Equal($"{BaseUrl}/system-events/11/acknowledge", _http.Endpoint);
        Assert.Equal("admin", _http.Body!.Value<string>("acknowledged_by"));
    }

    [Fact]
    public async Task 요약은_summary_경로를_읽는다()
    {
        _http.ResponseJson = """{ "success": true, "data": { "total_count": 8, "unacknowledged_count": 7, "by_severity": { "WARNING": 5 }, "by_type": {}, "recent_critical": [] } }""";

        var res = await Service().GetSystemEventSummaryAsync();

        Assert.Equal($"{BaseUrl}/system-events/summary", _http.Endpoint);
        Assert.Equal(7, res.Data!.UnacknowledgedCount);
        Assert.Equal(5, res.Data.BySeverity!["WARNING"]);
    }

    [Fact]
    public void 목록_쿼리는_값이_있는_칸만_싣는다()
    {
        var p = SystemEventApiService.BuildQuery(new SystemEventQuery(Page: 2, Limit: 500, ServerId: 8, Severity: "CRITICAL", IsAcknowledged: false,
                                                                      StartDate: "2026-10-01T00:00:00+09:00"));
        Assert.Equal("2", p["page"]);
        Assert.Equal("100", p["limit"]);   // 서버 상한
        Assert.Equal("8", p["server_id"]);
        Assert.Equal("CRITICAL", p["severity"]);
        Assert.Equal("false", p["is_acknowledged"]);
        Assert.Equal("2026-10-01T00:00:00+09:00", p["start_date"]);
        Assert.False(p.ContainsKey("type_event"));
        Assert.False(p.ContainsKey("end_date"));
    }
}

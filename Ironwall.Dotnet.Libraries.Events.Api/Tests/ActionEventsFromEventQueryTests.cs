using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;
/****************************************************************************
   Purpose      : 조치 목록의 원본 · 작성자 필터(G42) — from_event_id · user 쿼리 계약
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.

   Description  : 연결 이벤트는 원본별 조치 하위 경로가 없어 GET /api/events/actions?from_event_id= 가 유일한 입구다(명세 §6.4.2).
                  6.3.2 서버도 두 쿼리를 받으므로(v6.3.2 app/routers/actions.py) 판본과 관계없이 보낸다.
****************************************************************************/
[Trait("Category", "Contract")]
public class ActionEventsFromEventQueryTests
{
    private static (EventApiService Service, EventCapturingApiService Http) Create(EnumServerContract contract)
    {
        var http = new EventCapturingApiService { ResponseJson = "{\"success\":true,\"data\":[],\"pagination\":{\"total\":0}}" };
        return (new EventApiService(new LogService(), http, new ApiSetupModel { Url = "https://unit.test/api" },
            new FixedEventContractProbe(contract)), http);
    }

    [Trait("Scenario", "GOP815-G42")]
    [Theory]
    [InlineData(EnumServerContract.V6_3)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_send_from_event_id_and_user_when_given(EnumServerContract contract)
    {
        var (svc, http) = Create(contract);

        await svc.GetActionEventsAsync(null, null, 1, 100, 352, "operator1");

        Assert.Equal("https://unit.test/api/events/actions", http.Endpoint);
        Assert.Equal("352", http.Query!["from_event_id"]);
        Assert.Equal("operator1", http.Query["user"]);
        Assert.Equal("100", http.Query["limit"]);
    }

    [Trait("Scenario", "GOP815-G42")]
    [Fact]
    public async Task should_not_send_the_filters_from_the_old_overload()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetActionEventsAsync("2026-10-01T00:00:00+09:00", null, 2, 20);

        Assert.False(http.Query!.ContainsKey("from_event_id"));
        Assert.False(http.Query.ContainsKey("user"));
        Assert.Equal("2", http.Query["page"]);
        Assert.Equal("2026-10-01T00:00:00+09:00", http.Query["start_date"]);
    }
}

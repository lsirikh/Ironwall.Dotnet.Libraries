using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : 서버 축 통로 — 동시 쓰기 경합 409(G01) · 목록 표현 프로필(G28) 계약 시험, 네트워크 없음
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.

   Description  : G01 — 8.0.15 는 DB 교착 · 직렬화 실패를 409 CONFLICT + error.details.retryable=true 로 낸다(명세 §12.2).
                  "같은 이름" 고정 문구로 말하면 운영자가 이름을 바꾸러 간다 — 재시도 문구가 먼저다.
                  G28 — 서버 목록은 view=basic(자격증명 제외) + include=server_config(modes · thresholds)로 받는다(명세 §서버 목록).
****************************************************************************/
public class ServerAxisRetryAndListViewTests
{
    private const string BaseUrl = "https://gop.test/api";

    private const string RetryableConflict =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"동시에 처리 중인 다른 요청과 충돌해 이 요청을 처리하지 못했습니다\",\"details\":{\"retryable\":true}}}";

    private const string NameConflict =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"Server with name 'A' already exists\",\"details\":null}}";

    private static (ServerAxisApiService Service, CapturingApiService Http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        return (new ServerAxisApiService(http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract)), http);
    }

    private static ServerWriteIntent Intent() => new() { TypeServer = "PROXY", CategoryId = 1, Name = "A", IpAddress = "10.0.0.1", Port = 8100 };

    #region - G01 409 재시도 -
    [Trait("Scenario", "GOP815-G01")]
    [Theory]
    [InlineData(EnumServerContract.V8_0)]
    [InlineData(EnumServerContract.V6_3)]
    public async Task should_say_retry_when_create_hits_a_retryable_conflict(EnumServerContract contract)
    {
        var (svc, http) = Create(contract);
        http.ResponseStatus = HttpStatusCode.Conflict;
        http.ResponseJson = RetryableConflict;

        var result = await svc.CreateServerAsync(Intent());

        Assert.False(result.IsSuccess);
        Assert.Contains(ApiStatusHelper.RetryableConflictMessage, result.Message);
        Assert.DoesNotContain("같은 이름", result.Message);
    }

    [Trait("Scenario", "GOP815-G01")]
    [Fact]
    public async Task should_say_retry_when_patch_hits_a_retryable_conflict()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);
        http.ResponseStatus = HttpStatusCode.Conflict;
        http.ResponseJson = RetryableConflict;

        var result = await svc.PatchServerAsync(3, new ServerWriteIntent { Port = 8200 });

        Assert.False(result.IsSuccess);
        Assert.Contains(ApiStatusHelper.RetryableConflictMessage, result.Message);
        Assert.DoesNotContain("같은 이름", result.Message);
    }

    [Trait("Scenario", "GOP815-G01")]
    [Fact]
    public async Task should_keep_the_duplicate_name_text_when_the_conflict_is_not_retryable()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);
        http.ResponseStatus = HttpStatusCode.Conflict;
        http.ResponseJson = NameConflict;

        var created = await svc.CreateServerAsync(Intent());
        var patched = await svc.PatchServerAsync(3, new ServerWriteIntent { Name = "A" });

        Assert.Contains("같은 이름의 서버가 이미 있습니다", created.Message);
        Assert.Contains("같은 이름의 서버가 이미 있습니다", patched.Message);
        Assert.DoesNotContain(ApiStatusHelper.RetryableConflictMessage, created.Message);
        Assert.DoesNotContain(ApiStatusHelper.RetryableConflictMessage, patched.Message);
    }
    #endregion

    #region - G28 목록 표현 프로필 -
    [Trait("Scenario", "GOP815-G28")]
    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_list_servers_with_basic_view_and_server_config_on_the_axis_contract(EnumServerContract contract)
    {
        var (svc, http) = Create(contract);
        http.ResponseJson = "{\"success\":true,\"data\":[],\"pagination\":{\"total\":0}}";

        await svc.GetServersAsync(unitId: null, includeDescendants: false);

        Assert.Equal($"{BaseUrl}/servers", http.Endpoint);
        Assert.Equal("basic", http.Query!["view"]);
        Assert.Equal("server_config", http.Query["include"]);
    }

    [Trait("Scenario", "GOP815-G28")]
    [Fact]
    public async Task should_not_send_view_or_include_to_a_6_3_server()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);
        http.ResponseJson = "{\"success\":true,\"data\":[],\"pagination\":{\"total\":0}}";

        await svc.GetServersAsync(unitId: null, includeDescendants: false);

        Assert.Equal($"{BaseUrl}/servers", http.Endpoint);
        Assert.False(http.Query!.ContainsKey("view"));
        Assert.False(http.Query.ContainsKey("include"));
    }
    #endregion
}

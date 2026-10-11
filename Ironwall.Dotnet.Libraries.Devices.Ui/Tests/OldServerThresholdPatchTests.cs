using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 원격 6.3.x 대비 — 6.3 서버의 PATCH /api/servers/{id} 는 threshold_config 를 <b>통째로 바꾼다</b>
                  (6.3.0 payload app/routers/servers.py:437-440 — model_dump(exclude_unset) 를 setattr, 병합 없음).
                  손댄 임계만 보내면 나머지 임계가 200 과 함께 조용히 지워진다 → 저장 직전에 다시 받은 임계 위에 고친 값만 덮어 보낸다.
                  진짜 ApiService · ServerAxisApiService · ServerConsoleService 를 가짜 HTTP 처리기 위에 올린다 — 창 없음 · 원격 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OldServerThresholdPatchTests
{
    private const string Full = """{"cpu":{"warning":70,"critical":90},"ram":{"warning":80,"critical":95},"disk":{"warning":80,"critical":95},"network":{"warning_mbps":800,"critical_mbps":950}}""";

    /// <summary>6.3.0 <c>GET /api/servers/3</c> — 평면 모양(<c>_server_to_response</c>).</summary>
    private static string Server630(string thresholds) =>
        "{\"success\":true,\"message\":\"ok\",\"data\":{\"id\":3,\"category_id\":2,\"name\":\"PIDS-PROXY-01\",\"status\":\"NORMAL\",\"ip_address\":\"10.0.0.5\"," +
        "\"port\":8080,\"hostname\":null,\"user_name\":null,\"threshold_config\":" + thresholds + "," +
        "\"created_at\":\"2026-10-01T09:00:00+09:00\",\"updated_at\":\"2026-10-01T09:00:00+09:00\"}}";

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract == EnumServerContract.V6_3 ? "6.3.0" : Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class ServerHandler : DelegatingHandler
    {
        private readonly string _get;
        public ConcurrentQueue<string> PatchBodies { get; } = new();
        public ServerHandler(string get) => _get = get;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string reply = _get;
            if (request.Method == HttpMethod.Patch)
                PatchBodies.Enqueue(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }

    private static (ServerConsoleService Service, ServerHandler Http) Rig(string thresholds)
    {
        var http = new ServerHandler(Server630(thresholds));
        var setup = new ApiSetupModel { Url = "http://127.0.0.1:9/api" };
        var api = new ApiService(null, setup, http);
        api.Initialize();
        var probe = new FixedProbe(EnumServerContract.V6_3);
        var service = new ServerConsoleService(
            new ServerAxisApiService(api, setup, probe),
            new ServerApiService(null, api, setup, probe),
            new DeviceQueryPolicy(probe));
        return (service, http);
    }

    [Fact]
    [Trait("Scenario", "DEP-309")]
    public async Task should_send_every_threshold_when_one_value_is_edited_on_a_6_3_server()
    {
        var (service, http) = Rig(Full);

        var result = await service.SaveAsync(3, new ServerWriteIntent { CpuWarning = 61 });

        Assert.True(result.IsSuccess, result.Message);
        var body = JObject.Parse(Assert.Single(http.PatchBodies));
        var expected = JObject.Parse(Full);
        expected["cpu"]!["warning"] = 61.0;
        Assert.True(JToken.DeepEquals(expected, body["threshold_config"]), body.ToString());
    }

    [Fact]
    [Trait("Scenario", "DEP-309")]
    public async Task should_send_only_the_edited_values_when_the_6_3_server_had_no_thresholds()
    {
        var (service, http) = Rig("null");

        await service.SaveAsync(3, new ServerWriteIntent { RamCritical = 97 });

        var body = JObject.Parse(Assert.Single(http.PatchBodies));
        Assert.True(JToken.DeepEquals(JObject.Parse("""{"ram":{"critical":97.0}}"""), body["threshold_config"]), body.ToString());
    }

    [Fact]
    [Trait("Scenario", "DEP-309")]
    public void should_keep_the_axis_patch_to_edited_values_only()
    {
        // 무회귀 — 7.0+ 는 RFC 7396 병합이라 손댄 값만 보낸다(기준선이 있어도 싣지 않는다).
        var intent = new ServerWriteIntent { CpuWarning = 61, BaselineThresholds = JObject.Parse(Full) };

        var body = ServerAxisWriter.BuildPatch(intent, EnumServerContract.V8_0);

        Assert.True(JToken.DeepEquals(JObject.Parse("""{"cpu":{"warning":61.0}}"""), body["server_config"]!["thresholds"]), body.ToString());
    }

    [Fact]
    [Trait("Scenario", "DEP-309")]
    public void should_not_put_thresholds_on_the_wire_when_nothing_in_them_was_edited()
    {
        var body = ServerAxisWriter.BuildPatch(new ServerWriteIntent { Port = 8081, BaselineThresholds = JObject.Parse(Full) }, EnumServerContract.V6_3);

        Assert.Null(body["threshold_config"]);
        Assert.Equal(new[] { "port" }, body.Properties().Select(p => p.Name).ToArray());
    }
}

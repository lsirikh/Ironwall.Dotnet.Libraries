using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 함체째 부대 옮기기(GOP API 8.0.15 대조 G32) — 진짜 <see cref="DeviceApiService"/> 를 가짜 HTTP 위에 올려 4단계 순서와
/// 실제 본문(<c>device_ids</c> · <c>unit_id</c>)을 본다. 중간 실패면 옮긴 장비를 되돌리고 <c>device_ids</c> 를 다시 넣는다.
/// </summary>
public class EnclosureUnitMoverTests
{
    private const int EnclosureId = 50;
    private const int OldUnit = 3;
    private const int NewUnit = 7;

    private sealed class Probe : IServerContractProbe
    {
        public Probe(string raw) => RawVersion = raw;
        public EnumServerContract Contract => EnumServerContract.V8_0;
        public string? RawVersion { get; }
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    /// <summary>함체 50(부대 3) 안에 경광등 11 · 12. 쓰기는 적고, 정해 준 차례의 쓰기만 거절한다.</summary>
    private sealed class Server : DelegatingHandler
    {
        public ConcurrentQueue<(string Method, string Path, JObject? Body)> Writes { get; } = new();
        public List<string> Reads { get; } = new();
        public int FailEnclosurePatchNo { get; set; }      // 1부터 — 함체 PATCH 몇 번째를 거절할지
        public int FailLampId { get; set; }
        public bool EmptyEnclosure { get; set; }
        private int _enclosurePatches;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get)
            {
                lock (Reads) Reads.Add(path);
                if (path.Contains("/devices/enclosures?") && path.Contains("device_id=11"))
                    return Ok("{\"success\":true,\"message\":\"ok\",\"data\":[" + Enclosure(false) + "]}");
                if (path.StartsWith("/api/devices/enclosures/50"))
                    return Ok("{\"success\":true,\"message\":\"ok\",\"data\":" + Enclosure(true) + "}");
                if (path.StartsWith("/api/devices/lamps/"))
                {
                    var id = int.Parse(path.Split('/', '?')[4]);
                    return Ok("{\"success\":true,\"message\":\"ok\",\"data\":{\"id\":" + id + ",\"number_device\":" + id + ",\"name_device\":\"경광등 " + id
                              + "\",\"category_device\":\"lamp\",\"status\":\"ACTIVATED\",\"unit_id\":" + OldUnit + "}}");
                }
                return Fail(404);
            }

            var body = request.Content == null ? null : JObject.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            Writes.Enqueue((request.Method.Method, path, body));
            if (path.StartsWith("/api/devices/enclosures/50"))
            {
                var no = Interlocked.Increment(ref _enclosurePatches);
                return no == FailEnclosurePatchNo ? Fail(422) : Ok("{\"success\":true,\"message\":\"ok\",\"data\":" + Enclosure(false) + "}");
            }
            if (FailLampId > 0 && path.StartsWith($"/api/devices/lamps/{FailLampId}") && (int?)body?["unit_id"] == NewUnit) return Fail(422);
            return Ok("{\"success\":true,\"message\":\"ok\",\"data\":{\"id\":1}}");
        }

        private string Enclosure(bool contents) =>
            "{\"id\":50,\"number_device\":50,\"name_device\":\"함체-50\",\"category_device\":\"enclosure\",\"status\":\"ACTIVATED\",\"unit_id\":" + OldUnit
            + (contents ? ",\"contents\":" + (EmptyEnclosure ? "[]" : "[{\"id\":11,\"category_device\":\"lamp\",\"name_device\":\"경광등 11\"},{\"id\":12,\"category_device\":\"lamp\",\"name_device\":\"경광등 12\"}]") : "")
            + "}";

        private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        private static HttpResponseMessage Fail(int status) => new((HttpStatusCode)status)
        {
            Content = new StringContent("{\"success\":false,\"message\":\"\",\"error\":{\"code\":\"VALIDATION_ERROR\",\"message\":\"refused\"}}", Encoding.UTF8, "application/json"),
        };
    }

    private static (EnclosureUnitMover Mover, Server Http) Rig(string raw = "8.0.11")
    {
        var http = new Server();
        var setup = new ApiSetupModel { Url = "http://127.0.0.1:9/api" };
        var api = new ApiService(null, setup, http);
        api.Initialize();
        var probe = new Probe(raw);
        return (new EnclosureUnitMover(new DeviceApiService(null, api, setup, probe), new DeviceQueryPolicy(probe)), http);
    }

    private static List<(string Method, string Path, JObject? Body)> W(Server http) => http.Writes.ToList();

    [Fact]
    public async Task should_plan_enclosure_move_when_device_is_inside_an_enclosure()
    {
        var (mover, _) = Rig();

        var plan = await mover.PlanAsync(11, EnumDeviceCategory.Lamp);

        Assert.NotNull(plan);
        Assert.Equal(EnclosureId, plan!.EnclosureId);
        Assert.Equal(OldUnit, plan.OldUnitId);
        Assert.Equal(new[] { 11, 12 }, plan.ContentIds);
        Assert.True(plan.TriggeredByContent);
        Assert.Contains("함체 '함체-50' 안에", plan.ConfirmText("고른 부대"));
    }

    [Fact]
    public async Task should_not_plan_when_server_is_older_than_8_0_6()
    {
        var (mover, http) = Rig("8.0.5");

        Assert.False(mover.IsAvailable);
        Assert.Null(await mover.PlanAsync(EnclosureId, EnumDeviceCategory.Enclosure));
        Assert.Empty(http.Reads);                                                  // 묻지도 않는다
    }

    [Fact]
    public async Task should_not_plan_when_enclosure_is_empty()
    {
        var (mover, http) = Rig();
        http.EmptyEnclosure = true;

        Assert.Null(await mover.PlanAsync(EnclosureId, EnumDeviceCategory.Enclosure));
    }

    [Fact]
    public async Task should_move_in_four_steps_with_real_bodies_when_every_step_succeeds()
    {
        var (mover, http) = Rig();
        var plan = (await mover.PlanAsync(EnclosureId, EnumDeviceCategory.Enclosure))!;

        var result = await mover.MoveAsync(plan, NewUnit);

        Assert.True(result.IsSuccess, result.Message);
        var w = W(http);
        Assert.Equal(new[] { "/api/devices/enclosures/50", "/api/devices/lamps/11", "/api/devices/lamps/12", "/api/devices/enclosures/50", "/api/devices/enclosures/50" },
                     w.Select(x => x.Path).ToArray());
        Assert.All(w, x => Assert.Equal("PATCH", x.Method));
        // ① 비우기 — 부대는 그대로
        Assert.Equal(new JArray(), w[0].Body!["device_ids"]);
        Assert.Equal(OldUnit, (int)w[0].Body!["unit_id"]!);
        // ② 장비마다 새 부대
        Assert.Equal(NewUnit, (int)w[1].Body!["unit_id"]!);
        Assert.Equal(NewUnit, (int)w[2].Body!["unit_id"]!);
        // ③ 함체 새 부대 — 목록 키는 싣지 않는다
        Assert.Equal(NewUnit, (int)w[3].Body!["unit_id"]!);
        Assert.Null(w[3].Body!["device_ids"]);
        // ④ 다시 넣기
        Assert.Equal(new[] { 11, 12 }, w[4].Body!["device_ids"]!.Values<int>().ToArray());
        Assert.Equal(NewUnit, (int)w[4].Body!["unit_id"]!);
    }

    [Fact]
    public async Task should_restore_devices_and_device_ids_when_step_three_fails()
    {
        var (mover, http) = Rig();
        http.FailEnclosurePatchNo = 2;                                             // ③ 함체 부대 옮기기 거절
        var plan = (await mover.PlanAsync(EnclosureId, EnumDeviceCategory.Enclosure))!;

        var result = await mover.MoveAsync(plan, NewUnit);

        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.FailedStep);
        Assert.True(result.Restored);
        var w = W(http);
        // ① ② ② ③(거절) → 되돌리기: 장비 12 · 11 원래 부대로 → 함체 device_ids 복원
        Assert.Equal(7, w.Count);
        Assert.Equal(("/api/devices/lamps/12", OldUnit), (w[4].Path, (int)w[4].Body!["unit_id"]!));
        Assert.Equal(("/api/devices/lamps/11", OldUnit), (w[5].Path, (int)w[5].Body!["unit_id"]!));
        var restore = w[^1];
        Assert.Equal("/api/devices/enclosures/50", restore.Path);
        Assert.Equal(new[] { 11, 12 }, restore.Body!["device_ids"]!.Values<int>().ToArray());
        Assert.Equal(OldUnit, (int)restore.Body!["unit_id"]!);
        Assert.Contains("원래대로 되돌렸습니다", result.Message);
    }

    [Fact]
    public async Task should_restore_only_moved_devices_when_step_two_fails_midway()
    {
        var (mover, http) = Rig();
        http.FailLampId = 12;                                                       // ② 두 번째 장비 거절
        var plan = (await mover.PlanAsync(EnclosureId, EnumDeviceCategory.Enclosure))!;

        var result = await mover.MoveAsync(plan, NewUnit);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.FailedStep);
        var w = W(http);
        Assert.Equal(new[] { "/api/devices/enclosures/50", "/api/devices/lamps/11", "/api/devices/lamps/12", "/api/devices/lamps/11", "/api/devices/enclosures/50" },
                     w.Select(x => x.Path).ToArray());
        Assert.Equal(OldUnit, (int)w[3].Body!["unit_id"]!);                       // 옮겼던 11 만 되돌린다
        Assert.Equal(new[] { 11, 12 }, w[4].Body!["device_ids"]!.Values<int>().ToArray());
    }

    [Fact]
    public async Task should_change_nothing_when_emptying_the_enclosure_fails()
    {
        var (mover, http) = Rig();
        http.FailEnclosurePatchNo = 1;
        var plan = (await mover.PlanAsync(EnclosureId, EnumDeviceCategory.Enclosure))!;

        var result = await mover.MoveAsync(plan, NewUnit);

        Assert.Equal(1, result.FailedStep);
        Assert.Single(W(http));
        Assert.Contains("바뀐 것은 없습니다", result.Message);
    }
}

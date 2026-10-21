using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json.Linq;
using System.Net;
using Xunit;
using static Ironwall.Dotnet.Libraries.Devices.Api.Tests.Spec8015Kit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// GOP API 8.0.15 명세 대조(L2) — 장비 API 계층 계약 시험. 서버에 붙지 않는다(가짜 HTTP).
/// </summary>
[Trait("Scenario", "Spec8015")]
public class Spec8015DeviceApiTests
{
    #region - G21 프리셋 목록 끝까지 -
    /// <summary>서버 기본 limit(10)처럼 요청 limit 을 무시하고 10건씩 주는 가짜 — 25건 · 3쪽.</summary>
    private static (HttpStatusCode, string) PresetPages(ScriptedCall call, bool withPagination)
    {
        const int total = 25, size = 10;
        var page = int.Parse(call.Query!["page"]);
        var items = new JArray(Enumerable.Range((page - 1) * size + 1, Math.Max(0, Math.Min(size, total - (page - 1) * size)))
            .Select(i => new JObject { ["id"] = i, ["camera_id"] = 7, ["preset_index"] = i, ["preset_name"] = $"p{i}" }));
        var root = new JObject
        {
            ["success"] = true,
            ["data"] = new JObject { ["items"] = items, ["total"] = total },
            ["meta"] = new JObject(),
        };
        if (withPagination)
            root["pagination"] = new JObject { ["page"] = page, ["limit"] = size, ["total"] = total, ["total_pages"] = 3 };
        return (HttpStatusCode.OK, root.ToString());
    }

    [Fact]
    public async Task G21_프리셋_25건_3쪽을_limit100으로_끝까지_모은다()
    {
        var http = new ScriptedApiService(c => PresetPages(c, withPagination: true));
        var svc = Service(http, EnumServerContract.V8_0, "8.0.15");

        var r = await svc.GetPresetsAsync(7);

        Assert.True(r.Success);
        Assert.Equal(25, r.Data!.Items.Count);
        Assert.Equal(Enumerable.Range(1, 25), r.Data.Items.Select(p => p.Id));
        Assert.Equal(3, http.Calls.Count);
        Assert.All(http.Calls, c => Assert.Equal("100", c.Query!["limit"]));
        Assert.Equal(new[] { "1", "2", "3" }, http.Calls.Select(c => c.Query!["page"]));
    }

    [Fact]
    public async Task G21_pagination이_없으면_data_total을_보고_끝까지_모은다()
    {
        var http = new ScriptedApiService(c => PresetPages(c, withPagination: false));
        var svc = Service(http, EnumServerContract.V6_3, "6.3.2");

        var r = await svc.GetPresetsAsync(7, includeRois: true);

        Assert.Equal(25, r.Data!.Items.Count);
        Assert.Equal(3, http.Calls.Count);
        Assert.All(http.Calls, c => Assert.Equal("true", c.Query!["include_rois"]));
    }

    [Fact]
    public async Task G21_중간_쪽이_실패하면_실패를_그대로_돌려준다()
    {
        var http = new ScriptedApiService(c => c.Query!["page"] == "2"
            ? (HttpStatusCode.InternalServerError, "{\"success\":false,\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"boom\"}}")
            : PresetPages(c, withPagination: true));
        var svc = Service(http, EnumServerContract.V8_0, "8.0.15");

        var r = await svc.GetPresetsAsync(7);

        Assert.False(r.Success);
        Assert.Equal(500, r.StatusCode);
    }
    #endregion

    #region - G25 음원 그룹 목록 -
    private const string FileGroupsJson = """
    {"success":true,"message":"FileGroup list retrieved","data":[
      {"id":1,"server_id":3,"group_id":2,"group_name":"화재경보","files":["music01.mp3","music02.mp3"],"created_at":"2026-01-07T10:00:00+09:00","updated_at":"2026-01-07T10:00:00+09:00"}],
     "pagination":{"page":1,"limit":100,"total":1,"total_pages":1}}
    """;

    [Fact]
    public async Task G25_음원그룹_목록은_경로_server_id_limit상한을_싣는다()
    {
        var (svc, http) = Capturing(EnumServerContract.V8_0, "8.0.15", FileGroupsJson);

        var r = await svc.GetFileGroupsAsync(serverId: 3, page: 2, limit: 500);

        Assert.Equal($"{BaseUrl}/file-groups", http.Endpoint);
        Assert.Equal("3", http.Query!["server_id"]);
        Assert.Equal("2", http.Query["page"]);
        Assert.Equal("100", http.Query["limit"]);
        Assert.True(r.Success);
        var g = Assert.Single(r.Data!);
        Assert.Equal("화재경보", g.Name);
        Assert.Equal(3, g.ServerId);
        Assert.Equal(2, g.GroupId);
        Assert.Equal(new[] { "music01.mp3", "music02.mp3" }, g.Files);
        Assert.Equal(1, r.Pagination!.TotalPages);
    }

    [Fact]
    public async Task G25_server_id를_안주면_싣지_않는다()
    {
        var (svc, http) = Capturing(EnumServerContract.V6_3, "6.3.2", FileGroupsJson);

        await svc.GetFileGroupsAsync();

        Assert.False(http.Query!.ContainsKey("server_id"));
        Assert.Equal("1", http.Query["page"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    public async Task G25_권한없음과_없음을_상태코드로_가른다(HttpStatusCode status, int expected)
    {
        var (svc, _) = Capturing(EnumServerContract.V8_0, "8.0.15",
            "{\"success\":false,\"error\":{\"code\":\"X\",\"message\":\"m\"}}", status);

        var r = await svc.GetFileGroupsAsync();

        Assert.False(r.Success);
        Assert.Equal(expected, r.StatusCode);
    }
    #endregion

    #region - G22 함체 안 장비 -
    private static EnclosureDeviceDto Enclosure(List<int>? ids) => new() { Id = 9, NameDevice = "ENC", NumberDevice = 9, DeviceIds = ids };

    [Theory]
    [InlineData(EnumServerContract.V8_0, "8.0.6", true)]
    [InlineData(EnumServerContract.V8_0, "8.0.15", true)]
    [InlineData(EnumServerContract.V8_0, "8.0.5", false)]
    [InlineData(EnumServerContract.V8_0, null, false)]
    [InlineData(EnumServerContract.V7_0, "7.0.1", false)]
    [InlineData(EnumServerContract.V6_3, "6.3.2", false)]
    public async Task G22_device_ids는_806이상에서만_싣는다(EnumServerContract contract, string? raw, bool expected)
    {
        var http = new CapturingApiService();
        var svc = new DeviceApiService(null, http, new Ironwall.Dotnet.Libraries.Api.Models.ApiSetupModel { Url = BaseUrl },
            new VersionedContractProbe(contract, raw));

        await svc.PatchEnclosureAsync(9, Enclosure(new List<int> { 101, 102 }));

        Assert.Equal(expected, http.Body!.ContainsKey("device_ids"));
        if (expected) Assert.Equal(new[] { 101, 102 }, http.Body["device_ids"]!.Values<int>());
    }

    [Fact]
    public async Task G22_device_ids_null이면_생략_빈목록이면_모두빼기()
    {
        var (svc, http) = Capturing(EnumServerContract.V8_0, "8.0.15");

        await svc.PatchEnclosureAsync(9, Enclosure(null));
        Assert.False(http.Body!.ContainsKey("device_ids"));

        await svc.UpdateEnclosureAsync(9, Enclosure(new List<int>()));
        Assert.Equal(JTokenType.Array, http.Body!["device_ids"]!.Type);
        Assert.Empty((JArray)http.Body["device_ids"]!);
    }

    [Fact]
    public async Task G22_contents는_읽고_쓰기본문에는_싣지_않는다()
    {
        const string json = """
        {"success":true,"data":{"id":9,"name_device":"ENC","category_device":"enclosure",
          "contents":[{"id":101,"category_device":"controller","name_device":"제어기1"},{"id":102,"category_device":"camera","name_device":"카메라"}]}}
        """;
        var (svc, http) = Capturing(EnumServerContract.V8_0, "8.0.15", json);

        var r = await svc.GetEnclosureByIdAsync(9, include: "contents");

        Assert.Equal("contents", http.Query!["include"]);
        Assert.Equal(2, r.Data!.Contents!.Count);
        Assert.Equal(101, r.Data.Contents[0].Id);
        Assert.Equal("controller", r.Data.Contents[0].CategoryDevice);
        Assert.Equal("제어기1", r.Data.Contents[0].NameDevice);

        await svc.PatchEnclosureAsync(9, r.Data);
        Assert.False(http.Body!.ContainsKey("contents"));
    }

    [Fact]
    public async Task G22_기본응답에는_contents가_없어_null이다()
    {
        var (svc, _) = Capturing(EnumServerContract.V8_0, "8.0.15", "{\"success\":true,\"data\":{\"id\":9}}");
        var r = await svc.GetEnclosureByIdAsync(9);
        Assert.Null(r.Data!.Contents);
    }

    [Fact]
    public async Task G22_device_ids_422_사유를_오류메시지로_그대로_전달한다()
    {
        const string reason = "장비 [55] 는 이미 다른 함체(함체 [12])에 들어 있습니다";
        var body = new JObject
        {
            ["success"] = false,
            ["error"] = new JObject
            {
                ["code"] = "VALIDATION_ERROR",
                ["message"] = reason,
                ["details"] = new JArray(new JObject { ["field"] = "device_ids", ["message"] = reason }),
            },
        }.ToString();
        var (svc, _) = Capturing(EnumServerContract.V8_0, "8.0.15", body, HttpStatusCode.UnprocessableEntity);

        var r = await svc.PatchEnclosureAsync(9, Enclosure(new List<int> { 55 }));

        Assert.False(r.Success);
        Assert.Equal(422, r.StatusCode);
        Assert.Equal(reason, r.Error!.Message);
    }

    [Theory]
    [InlineData(EnumServerContract.V8_0, "8.0.5")]
    [InlineData(EnumServerContract.V6_3, "6.3.2")]
    public async Task G22_역조회는_806미만이면_서버에_묻지_않는다(EnumServerContract contract, string raw)
    {
        var (svc, http) = Capturing(contract, raw);

        var r = await svc.GetEnclosuresAsync(101);

        Assert.False(r.Success);
        Assert.Equal("ENDPOINT_UNAVAILABLE", r.Error!.Code);
        Assert.Equal(0, http.CallCount);
    }

    [Fact]
    public async Task G22_역조회는_device_id_질의로_묻는다()
    {
        var (svc, http) = Capturing(EnumServerContract.V8_0, "8.0.11",
            "{\"success\":true,\"data\":[{\"id\":12,\"name_device\":\"ENC\"}],\"pagination\":{\"page\":1,\"limit\":20,\"total\":1,\"total_pages\":1}}");

        var r = await svc.GetEnclosuresAsync(101);

        Assert.Equal($"{BaseUrl}/devices/enclosures", http.Endpoint);
        Assert.Equal("101", http.Query!["device_id"]);
        Assert.Equal(12, Assert.Single(r.Data!).Id);
    }
    #endregion

    #region - G17 저장 경고 -
    [Fact]
    public async Task G17_config_저장_결과에_경고가_실려_호출부가_알릴_수_있다()
    {
        const string json = """
        {"success":true,"data":{"device_config":{}},
         "warnings":[
           {"field":"device_config.thresholds.temperature","code":"UNMATCHED_THRESHOLD","message":"'temperature' 는 이 장비의 부품이 만드는 메트릭이 아닙니다"},
           {"field":"observed_at","code":"OBSERVED_AT_DEFAULTED","message":"x"}]}
        """;
        var (svc, _) = Capturing(EnumServerContract.V8_0, "8.0.15", json);

        var r = await svc.PatchDeviceConfigAsync("enclosures", 9, new DeviceConfigAxisDto { Thresholds = JObject.Parse("{\"temperature\":{\"high\":40}}") });

        Assert.Equal(2, r.Warnings!.Count);
        Assert.Equal("저장했지만 이 장비에 없는 측정 항목이라 경보가 동작하지 않습니다(온도)", SaveWarnings.ToText(SaveWarnings.Collect(r)));
    }

    [Fact]
    public async Task G17_장비_본체_쓰기_결과에도_경고가_실린다()
    {
        const string json = """
        {"success":true,"data":{"id":9},"warnings":[{"field":"thresholds.humidity","code":"UNMATCHED_THRESHOLD","message":"m"}]}
        """;
        var (svc, _) = Capturing(EnumServerContract.V8_0, "8.0.15", json);

        var r = await svc.PatchEnclosureAsync(9, Enclosure(null));

        var notice = Assert.Single(SaveWarnings.Collect(r));
        Assert.Equal("저장했지만 이 장비에 없는 측정 항목이라 경보가 동작하지 않습니다(습도)", notice.Text);
    }
    #endregion

    #region - G24 전 함체 최신 계측 -
    [Fact]
    public async Task G24_전함체_최신계측을_읽고_계측없음은_null이다()
    {
        const string json = """
        {"success":true,"message":"All enclosure metrics retrieved successfully","data":[
          {"enclosure_id":394,"enclosure_name":"GOP-ENC-01","latest_metrics":null},
          {"enclosure_id":395,"enclosure_name":"GOP-ENC-02","latest_metrics":{"id":92,"enclosure_id":395,"temperature":25.5,"humidity":62.0,
            "vibration":3,"ups_battery_level":95,"ups_charging":true,"observed_at":"2026-09-12T12:42:11+09:00","created_at":"2026-09-12T12:42:12+09:00"}}],
         "pagination":null,"meta":{}}
        """;
        var (svc, http) = Capturing(EnumServerContract.V8_0, "8.0.15", json);

        var r = await svc.GetAllEnclosureLatestMetricsAsync();

        Assert.Equal($"{BaseUrl}/enclosure-metrics", http.Endpoint);
        Assert.True(r.Success);
        Assert.Null(r.Pagination);
        Assert.Equal(2, r.Data!.Count);
        Assert.Null(r.Data[0].LatestMetrics);
        Assert.Equal("GOP-ENC-01", r.Data[0].EnclosureName);
        var m = r.Data[1].LatestMetrics!;
        Assert.Equal(92, m.Id);
        Assert.Equal(25.5, m.TemperatureValue);
        Assert.Equal("2026-09-12T12:42:11+09:00", m.ObservedAt);
        Assert.Equal("2026-09-12T12:42:12+09:00", m.CreatedAt);
        Assert.Equal(95, m.UpsBatteryLevel);
    }

    [Fact]
    public async Task G24_경로가_없는_서버는_404로_가른다()
    {
        var (svc, _) = Capturing(EnumServerContract.V6_3, "6.3.2",
            "{\"success\":false,\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"Not Found\"}}", HttpStatusCode.NotFound);

        var r = await svc.GetAllEnclosureLatestMetricsAsync();

        Assert.False(r.Success);
        Assert.Equal(404, r.StatusCode);
    }
    #endregion

    #region - G23 함체 계측 저장 응답의 임계 초과 -
    [Fact]
    public async Task G23_data안의_threshold_exceeded_direction_severity를_먼저_읽는다()
    {
        const string json = """
        {"success":true,"data":{"id":5,"enclosure_id":9,"temperature":48.0,
          "threshold_exceeded":[{"field":"temperature","value":48.0,"threshold":45.0,"direction":"HIGH","severity":"WARNING"}]},
         "warnings":[]}
        """;
        var (svc, _) = Capturing(EnumServerContract.V8_0, "8.0.15", json);

        var r = await svc.CreateEnclosureMetricAsync(9, new EnclosureMetricDto { TemperatureValue = 48 });

        var item = Assert.Single(r.EffectiveThresholdExceeded);
        Assert.Equal("temperature", item.Field);
        Assert.Equal("HIGH", item.EffectiveDirection);
        Assert.Equal("WARNING", item.Severity);
    }

    [Fact]
    public async Task G23_63_최상위_type은_폴백으로_읽는다()
    {
        const string json = """
        {"success":true,"data":{"id":5,"enclosure_id":9,"temperature":"48.0"},
         "threshold_exceeded":[{"field":"temperature","value":48.0,"threshold":45.0,"type":"HIGH"}]}
        """;
        var (svc, _) = Capturing(EnumServerContract.V6_3, "6.3.2", json);

        var r = await svc.CreateEnclosureMetricAsync(9, new EnclosureMetricDto { TemperatureValue = 48 });

        var item = Assert.Single(r.EffectiveThresholdExceeded);
        Assert.Equal("HIGH", item.EffectiveDirection);
        Assert.Null(item.Severity);
    }
    #endregion

    #region - G26 점 전체 교체 응답 -
    [Fact]
    public async Task G26_점_교체_응답의_roi_id_points_total을_읽는다()
    {
        const string json = """
        {"success":true,"message":"XyPoints replaced successfully","data":{"roi_id":4,"points":[
          {"id":13,"roi_id":4,"x":0.1,"y":0.1,"order":0},{"id":14,"roi_id":4,"x":0.9,"y":0.1,"order":1},{"id":15,"roi_id":4,"x":0.9,"y":0.9,"order":2}],"total":3}}
        """;
        var (svc, http) = Capturing(EnumServerContract.V8_0, "8.0.15", json);

        var r = await svc.ReplacePointsAsync(4, new XyPointBulkDto());

        Assert.Equal($"{BaseUrl}/rois/4/points", http.Endpoint);
        Assert.Equal(3, r.Data!.Items.Count);
        Assert.Equal(3, r.Data.Total);
        Assert.Equal(new[] { 13, 14, 15 }, r.Data.Items.Select(p => p.Id));
        Assert.Equal(4, Assert.IsType<PointReplaceResultDto>(r.Data).RoiId);
    }
    #endregion

    #region - G27 통문 gate_status -
    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task G27_축계약에서는_gate_status를_싣지_않는다(EnumServerContract contract)
    {
        var (svc, http) = Capturing(contract, null, "{\"success\":true,\"data\":[]}");

        await svc.GetGatesAsync(gateStatus: "OPEN");

        Assert.False(http.Query!.ContainsKey("gate_status"));
        Assert.Equal(1, http.CallCount);
    }

    [Fact]
    public async Task G27_63에서는_gate_status를_그대로_싣는다()
    {
        var (svc, http) = Capturing(EnumServerContract.V6_3, "6.3.2", "{\"success\":true,\"data\":[]}");

        await svc.GetGatesAsync(gateStatus: "OPEN");

        Assert.Equal("OPEN", http.Query!["gate_status"]);
    }
    #endregion
}

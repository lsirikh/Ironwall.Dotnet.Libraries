using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : ServerApiService 쓰기 본문 — 카테고리 판별자(category_server) · 서버 계측 보고 (헤드리스)
   Created By   : Claude
   Created On   : 10/12/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : 라이브 시험(DEP-202 · DEP-203) 실패의 제품 결함 2건을 서버 없이 고정한다.
                  ① 카테고리 등록 · 수정 · 교체 — 7.0+ 는 type_server 를 422 REMOVED_FIELD 로 거부한다
                     (서버 app/schemas/server.py:69 · 518-570). 축 계약에서는 category_server 로 보내고,
                     응답의 category_server 는 TypeServer 로 읽힌다. 6.3 본문은 바이트 그대로.
                  ② 서버 계측 보고 — 7.0+ ServerMetricsCreate 는 extra="forbid"(server.py:585-617).
                     평면 ServerMetricDto 그대로면 server_id · created_at · threshold_exceeded 가 실려 422 UNKNOWN_FIELD 였다.
****************************************************************************/
public class ServerApiServiceWriteBodyTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static ServerApiService Create(EnumServerContract contract, out CapturingApiService http)
    {
        http = new CapturingApiService();
        return new ServerApiService(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract));
    }

    private static CategoryDto FullCategory() => new()
    {
        TypeServer = "ETC",
        Name = "기타 서버",
        Description = "설명",
        SortOrder = 99
    };

    #region - 카테고리: 축 계약은 category_server 로 보낸다 -
    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_send_category_server_instead_of_type_server_on_create_when_axis_era(EnumServerContract contract)
    {
        var service = Create(contract, out var http);

        await service.CreateCategoryAsync(FullCategory());

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/servers/categories", http.Endpoint);
        Assert.Equal("ETC", (string?)http.Body!["category_server"]);
        Assert.False(http.Body.ContainsKey("type_server"));
        Assert.Equal("기타 서버", (string?)http.Body["name"]);
        Assert.Equal("설명", (string?)http.Body["description"]);
        Assert.Equal(99, (int?)http.Body["sort_order"]);

        // 서버가 받는 4키 밖은 하나도 싣지 않는다(id · created_at 은 응답 전용).
        Assert.Empty(http.Body.Properties().Select(p => p.Name)
            .Except(new[] { "name", "category_server", "description", "sort_order" }));
    }

    [Fact]
    public async Task should_send_category_server_on_put_when_axis_era()
    {
        var service = Create(EnumServerContract.V8_0, out var http);

        await service.UpdateCategoryAsync(5, FullCategory());

        Assert.Equal("PUT", http.Method);
        Assert.Equal($"{BaseUrl}/servers/categories/5", http.Endpoint);
        Assert.Equal("ETC", (string?)http.Body!["category_server"]);
        Assert.False(http.Body.ContainsKey("type_server"));
        Assert.Equal(99, (int?)http.Body["sort_order"]);
    }

    [Fact]
    public async Task should_send_only_the_touched_key_on_patch_when_axis_era()
    {
        var service = Create(EnumServerContract.V8_0, out var http);

        await service.PatchCategoryAsync(5, new CategoryDto { Description = "수정된 설명" });

        Assert.Equal("PATCH", http.Method);
        Assert.Equal($"{BaseUrl}/servers/categories/5", http.Endpoint);
        var only = Assert.Single(http.Body!.Properties());
        Assert.Equal("description", only.Name);
        Assert.Equal("수정된 설명", (string?)only.Value);
    }

    [Fact]
    public async Task should_carry_category_server_on_patch_when_the_caller_sent_the_type()
    {
        var service = Create(EnumServerContract.V8_0, out var http);

        await service.PatchCategoryAsync(5, FullCategory());

        Assert.Equal("ETC", (string?)http.Body!["category_server"]);
        Assert.False(http.Body.ContainsKey("type_server"));
    }

    [Fact]
    public async Task should_read_category_server_from_the_response_into_type_server()
    {
        var service = Create(EnumServerContract.V8_0, out var http);
        http.ResponseStatus = System.Net.HttpStatusCode.Created;
        http.ResponseJson = "{\"success\":true,\"message\":\"Server category created successfully\",\"data\":" +
                            "{\"id\":42,\"name\":\"기타 서버\",\"category_server\":\"ETC\",\"description\":\"설명\",\"sort_order\":99," +
                            "\"created_at\":\"2026-10-12T09:00:00.000000+09:00\",\"updated_at\":\"2026-10-12T09:00:00.000000+09:00\"}," +
                            "\"warnings\":[]}";

        var response = await service.CreateCategoryAsync(FullCategory());

        Assert.True(response.Success);
        Assert.Equal(42, response.Data!.Id);
        Assert.Equal("ETC", response.Data.TypeServer);
    }
    #endregion

    #region - 카테고리: 6.3(운영) 본문은 그대로 — 회귀 없음 -
    [Fact]
    public async Task should_keep_the_flat_type_server_body_on_create_when_legacy()
    {
        var service = Create(EnumServerContract.V6_3, out var http);
        var dto = FullCategory();

        await service.CreateCategoryAsync(dto);

        Assert.Equal("POST", http.Method);
        Assert.Equal("ETC", (string?)http.Body!["type_server"]);
        Assert.False(http.Body.ContainsKey("category_server"));
        // 바이트 그대로 — DTO 를 직접 직렬화한 것과 같다.
        Assert.True(JToken.DeepEquals(JObject.Parse(JsonConvert.SerializeObject(dto)), http.Body));
    }

    [Fact]
    public async Task should_keep_the_flat_type_server_body_on_put_when_legacy()
    {
        var service = Create(EnumServerContract.V6_3, out var http);
        var dto = FullCategory();

        await service.UpdateCategoryAsync(5, dto);

        Assert.Equal("PUT", http.Method);
        Assert.True(JToken.DeepEquals(JObject.Parse(JsonConvert.SerializeObject(dto)), http.Body));
        Assert.False(http.Body!.ContainsKey("category_server"));
    }

    [Fact]
    public async Task should_not_add_category_server_on_patch_when_legacy()
    {
        var service = Create(EnumServerContract.V6_3, out var http);

        await service.PatchCategoryAsync(5, new CategoryDto { Description = "수정된 설명" });

        Assert.Equal("수정된 설명", (string?)http.Body!["description"]);
        Assert.False(http.Body.ContainsKey("category_server"));
        Assert.False(http.Body.ContainsKey("type_server"));   // 빈 문자열은 종전처럼 지운다
    }
    #endregion

    #region - 서버 계측 보고: 축 계약은 서버가 받는 키만 -
    private static readonly string[] MetricCreateKeys =
    {
        "cpu_usage", "ram_usage", "ram_total_gb", "ram_used_gb", "disk_usage", "disk_total_gb", "disk_used_gb",
        "network_in_mbps", "network_out_mbps", "process_count", "detail", "observed_at",
    };

    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_send_only_server_accepted_metric_keys_when_axis_era(EnumServerContract contract)
    {
        var service = Create(contract, out var http);

        await service.CreateServerMetricAsync(7, new ServerMetricDto
        {
            ServerId = 0,
            CpuUsage = 45.5,
            RamUsage = 60.2,
            DiskUsage = 30.1,
            NetworkInMbps = 10.5,
            NetworkOutMbps = 20.3,
            ProcessCount = 12
        });

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/servers/7/metrics", http.Endpoint);

        var keys = http.Body!.Properties().Select(p => p.Name).ToList();
        Assert.Empty(keys.Except(MetricCreateKeys));
        foreach (var forbidden in new[] { "server_id", "created_at", "updated_at", "id", "threshold_exceeded", "collected_at" })
            Assert.False(http.Body.ContainsKey(forbidden), $"'{forbidden}' 는 7.0+ 계측 보고 본문에 실리면 422 다");

        Assert.Equal(45.5, (double?)http.Body["cpu_usage"]);
        Assert.Equal(60.2, (double?)http.Body["ram_usage"]);
        Assert.Equal(30.1, (double?)http.Body["disk_usage"]);
        Assert.Equal(10.5, (double?)http.Body["network_in_mbps"]);
        Assert.Equal(20.3, (double?)http.Body["network_out_mbps"]);
        Assert.Equal(12, (int?)http.Body["process_count"]);

        // 값이 없는 계측은 null 로 싣지 않는다.
        Assert.False(http.Body.ContainsKey("ram_total_gb"));
        Assert.False(http.Body.ContainsKey("detail"));
        Assert.False(http.Body.ContainsKey("observed_at"));
    }

    [Fact]
    public async Task should_move_the_legacy_collected_at_into_observed_at_when_axis_era()
    {
        var service = Create(EnumServerContract.V8_0, out var http);

        await service.CreateServerMetricAsync(7, new ServerMetricDto
        {
            CpuUsage = 1.0,
            CollectedAt = "2026-10-12T10:00:05.120000+09:00",
            Detail = new JObject { ["source"] = "probe" }
        });

        // 대역은 본문을 JObject.Parse 로 읽어 ISO 문자열이 Date 토큰이 된다 — 같은 순간인지로 비교한다.
        Assert.Equal(System.DateTimeOffset.Parse("2026-10-12T10:00:05.120000+09:00"), InstantOf(http.Body!["observed_at"]));
        Assert.False(http.Body.ContainsKey("collected_at"));
        Assert.Equal("probe", (string?)http.Body["detail"]!["source"]);
    }

    [Fact]
    public async Task should_prefer_observed_at_over_collected_at_when_both_are_set()
    {
        var service = Create(EnumServerContract.V8_0, out var http);

        await service.CreateServerMetricAsync(7, new ServerMetricDto
        {
            CpuUsage = 1.0,
            ObservedAt = "2026-10-12T10:00:06.000000+09:00",
            CollectedAt = "2026-10-12T10:00:05.000000+09:00"
        });

        Assert.Equal(System.DateTimeOffset.Parse("2026-10-12T10:00:06.000000+09:00"), InstantOf(http.Body!["observed_at"]));
    }

    /// <summary>Date 토큰이든 문자열이든 같은 순간으로 읽는다.</summary>
    private static System.DateTimeOffset InstantOf(JToken? token)
    {
        Assert.NotNull(token);
        return token!.Type == JTokenType.Date
            ? new System.DateTimeOffset(token.Value<System.DateTime>())
            : System.DateTimeOffset.Parse(token.Value<string>()!);
    }

    [Fact]
    public async Task should_keep_the_flat_metric_body_when_legacy()
    {
        var service = Create(EnumServerContract.V6_3, out var http);
        var dto = new ServerMetricDto { CpuUsage = 45.5, RamUsage = 60.2 };

        await service.CreateServerMetricAsync(7, dto);

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/servers/7/metrics", http.Endpoint);
        // 6.3 본문은 바이트 그대로(server_id · created_at · threshold_exceeded 포함) — 운영 6.3.2 무회귀.
        Assert.True(JToken.DeepEquals(JObject.Parse(JsonConvert.SerializeObject(dto)), http.Body));
        Assert.True(http.Body!.ContainsKey("server_id"));
    }

    [Fact]
    public async Task should_read_the_axis_metric_response()
    {
        var service = Create(EnumServerContract.V8_0, out var http);
        http.ResponseStatus = System.Net.HttpStatusCode.Created;
        http.ResponseJson = "{\"success\":true,\"message\":\"Server metrics created successfully\",\"data\":" +
                            "{\"id\":11,\"server_id\":7,\"cpu_usage\":45.5,\"ram_usage\":null,\"detail\":null," +
                            "\"observed_at\":\"2026-10-12T10:00:05.120000+09:00\",\"created_at\":\"2026-10-12T10:00:05.200000+09:00\"," +
                            "\"threshold_exceeded\":[]},\"warnings\":[{\"field\":\"observed_at\",\"code\":\"OBSERVED_AT_DEFAULTED\",\"message\":\"-\"}]}";

        var response = await service.CreateServerMetricAsync(7, new ServerMetricDto { CpuUsage = 45.5 });

        Assert.True(response.Success);
        Assert.Equal(11, response.Data!.Id);
        Assert.Equal(7, response.Data.ServerId);
        Assert.Equal(45.5, response.Data.CpuUsage);
        Assert.Empty(response.Data.ThresholdExceededItems);
    }
    #endregion

    #region - 계측 보존 삭제: 건수는 message 에서 합성 -
    [Fact]
    public async Task should_synthesize_the_deleted_count_from_the_message_when_data_is_null()
    {
        var service = Create(EnumServerContract.V8_0, out var http);
        http.ResponseJson = "{\"success\":true,\"message\":\"Deleted 0 old metrics for server 42\",\"data\":null,\"warnings\":[]}";

        var response = await service.DeleteServerMetricsAsync(42, olderThanDays: 1);

        Assert.Equal("DELETE", http.Method);
        Assert.Equal($"{BaseUrl}/servers/42/metrics?older_than_days=1", http.Endpoint);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(42, response.Data!.ServerId);
        Assert.Equal(0, response.Data.DeletedCount);
    }

    [Fact]
    public async Task should_refuse_the_before_date_overload_without_calling_the_server()
    {
        var service = Create(EnumServerContract.V8_0, out var http);

#pragma warning disable CS0618
        var response = await service.DeleteServerMetricsAsync(42, beforeDate: "2020-01-01");
#pragma warning restore CS0618

        Assert.False(response.Success);
        Assert.Equal("INVALID_ARGUMENT", response.Error?.Code);
        Assert.Equal(0, http.CallCount);
    }
    #endregion
}

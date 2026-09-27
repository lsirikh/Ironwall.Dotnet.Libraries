using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : 부대 관계도 공유 배치 API(TEST-12) — 헤더 · 상태 해석 · 본문
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 서버에 붙지 않는다 — 전송 대역(ScriptedLayoutHttp)이 요청을 기록하고 정해 둔 응답을 돌려준다.
                  422 + path.unit_id 본문은 2026-09-28 시험 서버 8.0.3 실측 그대로다(probe log V-06).
                  시나리오: SIM-P001 · P009~P035 · F056 · F059 · F062 · Q001~Q041(412 해석 부분).
****************************************************************************/
public class UnitLayoutApiServiceTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static UnitLayoutApiService Create(ScriptedLayoutHttp http, EnumServerContract contract = EnumServerContract.V8_0)
        => new(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract));

    private static string Envelope(object data) => JsonConvert.SerializeObject(new { success = true, message = "ok", data, meta = new { } });

    private static string Error(string code, string message, object? details = null)
        => JsonConvert.SerializeObject(new { success = false, message, error = new { code, message, details }, meta = new { } });

    private static object Doc(long version, int layoutVersion = 1, params (int Id, double Dx, double Dy)[] items) => new
    {
        version,
        layout_version = layoutVersion,
        updated_at = "2026-09-27T14:02:11.120000+09:00",
        updated_by = new { id = 7, name = "김○○" },
        items = items.Select(i => new { unit_id = i.Id, dx = i.Dx, dy = i.Dy }).ToArray(),
    };

    /// <summary>시험 서버 8.0.3 이 <c>GET /api/units/layout</c> 에 실제로 돌려준 본문(V-06).</summary>
    private const string RouteShadow422 =
        "{\"success\":false,\"message\":\"요청 검증에 실패했습니다\",\"error\":{\"code\":\"VALIDATION_ERROR\",\"message\":\"요청 검증에 실패했습니다\","
        + "\"details\":[{\"field\":\"path.unit_id\",\"code\":\"CONSTRAINT\",\"message\":\"Input should be a valid integer, unable to parse string as an integer\"}]},"
        + "\"meta\":{\"timestamp\":\"2026-09-28T07:23:10.801553+09:00\",\"request_id\":\"b9ebb1c2\"}}";

    private static UnitLayoutPatchDto MovePatch(int unitId = 27, double dx = -51.1, double dy = 64.4) => new()
    {
        LayoutVersion = 1,
        Set = { new UnitLayoutItemDto { UnitId = unitId, Dx = dx, Dy = dy } },
    };

    #region - 읽기 · 지원 판정 (FR-50) -
    [Fact]
    public async Task should_return_supported_with_document_and_etag_when_get_answers_200()
    {
        // Arrange
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(13, 1, (27, -51.1, 64.4))), etag: "\"13\"");
        var service = Create(http);

        // Act
        var result = await service.GetAsync();

        // Assert
        var supported = Assert.IsType<UnitLayoutReadResult.Supported>(result);
        Assert.Equal(13, supported.Document.Version);
        Assert.Equal(1, supported.Document.LayoutVersion);
        Assert.Equal(13, supported.ETagVersion);
        Assert.Equal("김○○", supported.Document.UpdatedBy?.Name);
        var item = Assert.Single(supported.Document.Items);
        Assert.Equal((27, -51.1, 64.4), (item.UnitId, item.Dx, item.Dy));
        Assert.Equal("GET", http.Requests.Single().Method);
        Assert.Equal($"{BaseUrl}/units/layout", http.Requests.Single().Endpoint);
    }

    [Fact]
    public async Task should_return_supported_with_empty_items_when_nobody_moved_yet()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK,
            Envelope(new { version = 0, layout_version = 1, updated_at = (string?)null, updated_by = (object?)null, items = new object[0] }));

        var result = await Create(http).GetAsync();

        var supported = Assert.IsType<UnitLayoutReadResult.Supported>(result);
        Assert.Equal(0, supported.Document.Version);
        Assert.Null(supported.Document.UpdatedBy);
        Assert.Empty(supported.Document.Items);
        Assert.Null(supported.ETagVersion);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData(HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED")]
    [InlineData(HttpStatusCode.Gone, "ENDPOINT_REMOVED")]
    public async Task should_return_unsupported_when_route_is_absent(HttpStatusCode status, string code)
    {
        var http = new ScriptedLayoutHttp().Reply(status, Error(code, "없음"));

        var result = await Create(http).GetAsync();

        var unsupported = Assert.IsType<UnitLayoutReadResult.Unsupported>(result);
        Assert.Equal((int)status, unsupported.StatusCode);
    }

    [Fact]
    public async Task should_return_unsupported_when_error_code_is_endpoint_removed_whatever_the_status()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.BadRequest, Error("ENDPOINT_REMOVED", "제거됨"));

        var result = await Create(http).GetAsync();

        Assert.IsType<UnitLayoutReadResult.Unsupported>(result);
    }

    [Fact]
    public async Task should_return_unsupported_when_422_names_path_unit_id_because_route_is_shadowed()
    {
        // 8.0.1~8.0.3: /units/layout 이 /{unit_id} 에 가려 422 가 된다(V-06 실측 본문).
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity, RouteShadow422);

        var result = await Create(http).GetAsync();

        var unsupported = Assert.IsType<UnitLayoutReadResult.Unsupported>(result);
        Assert.Equal(422, unsupported.StatusCode);
    }

    [Fact]
    public async Task should_return_read_failed_when_422_does_not_name_path_unit_id()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity,
            Error("VALIDATION_ERROR", "검증 실패", new[] { new { field = "query.depth", code = "CONSTRAINT", message = "x" } }));

        var result = await Create(http).GetAsync();

        var failed = Assert.IsType<UnitLayoutReadResult.ReadFailed>(result);
        Assert.Equal(UnitLayoutFailureKind.Other, failed.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, UnitLayoutFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, UnitLayoutFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, UnitLayoutFailureKind.Server)]
    [InlineData(HttpStatusCode.BadGateway, UnitLayoutFailureKind.Server)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UnitLayoutFailureKind.Unreachable)]
    [InlineData(HttpStatusCode.GatewayTimeout, UnitLayoutFailureKind.Timeout)]
    public async Task should_return_read_failed_with_kind_when_get_fails(HttpStatusCode status, UnitLayoutFailureKind kind)
    {
        var http = new ScriptedLayoutHttp().Reply(status, Error("X", "실패"));

        var result = await Create(http).GetAsync();

        var failed = Assert.IsType<UnitLayoutReadResult.ReadFailed>(result);
        Assert.Equal(kind, failed.Kind);
        Assert.Equal((int)status, failed.StatusCode);
    }

    [Fact]
    public async Task should_return_read_failed_when_timeout_response_has_no_body()
    {
        // ApiService 는 시간 초과를 본문 없는 504 로 합성한다(ApiService.BuildExceptionResponse).
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.GatewayTimeout, body: null);

        var result = await Create(http).GetAsync();

        Assert.Equal(UnitLayoutFailureKind.Timeout, Assert.IsType<UnitLayoutReadResult.ReadFailed>(result).Kind);
    }

    [Fact]
    public async Task should_return_read_failed_parse_when_200_body_is_not_a_document()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, "<html>proxy</html>");

        var result = await Create(http).GetAsync();

        Assert.Equal(UnitLayoutFailureKind.Parse, Assert.IsType<UnitLayoutReadResult.ReadFailed>(result).Kind);
    }

    [Fact]
    public async Task should_return_read_failed_parse_when_200_envelope_has_no_data()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, "{\"success\":true,\"data\":null}");

        var result = await Create(http).GetAsync();

        Assert.Equal(UnitLayoutFailureKind.Parse, Assert.IsType<UnitLayoutReadResult.ReadFailed>(result).Kind);
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3)]
    [InlineData(EnumServerContract.V7_0)]
    public async Task should_return_unsupported_without_network_when_contract_is_below_8_0(EnumServerContract contract)
    {
        var http = new ScriptedLayoutHttp();

        var read = await Create(http, contract).GetAsync();
        var write = await Create(http, contract).PatchAsync(13, MovePatch());

        Assert.IsType<UnitLayoutReadResult.Unsupported>(read);
        Assert.IsType<UnitLayoutWriteResult.Unsupported>(write);
        Assert.Empty(http.Requests);
    }

    [Theory]
    [InlineData("\"21\"", 21L)]
    [InlineData("W/\"21\"", 21L)]
    [InlineData("\"abc\"", null)]
    public async Task should_read_etag_version_when_header_is_quoted_or_weak(string etag, long? expected)
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(21)), etag: etag);

        var result = await Create(http).GetAsync();

        Assert.Equal(expected, Assert.IsType<UnitLayoutReadResult.Supported>(result).ETagVersion);
    }
    #endregion

    #region - 쓰기 · 헤더 · 본문 (FR-10 · FR-52 · NFR-15) -
    [Fact]
    public async Task should_send_if_match_with_quoted_version_when_patch()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(14, 1, (27, -51.1, 64.4))), etag: "\"14\"");

        await Create(http).PatchAsync(13, MovePatch());

        var request = http.Requests.Single();
        Assert.Equal("PATCH", request.Method);
        Assert.Equal($"{BaseUrl}/units/layout", request.Endpoint);
        Assert.Equal("\"13\"", request.Headers["If-Match"]);
    }

    [Fact]
    public async Task should_send_exactly_the_four_contract_keys_with_empty_arrays_when_patch_has_only_set()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(14)));

        await Create(http).PatchAsync(13, MovePatch());

        var body = http.Requests.Single().Body!;
        Assert.Equal(new[] { "clear", "clear_all", "layout_version", "set" }, body.Properties().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal(1, body["layout_version"]!.Value<int>());
        Assert.Equal(JTokenType.Array, body["clear"]!.Type);
        Assert.Empty((JArray)body["clear"]!);
        Assert.False(body["clear_all"]!.Value<bool>());
    }

    [Fact]
    public async Task should_serialize_set_item_as_unit_id_dx_dy_only_when_patch()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(14)));

        await Create(http).PatchAsync(13, MovePatch(27, -51.1, 64.4));

        var item = (JObject)Assert.Single((JArray)http.Requests.Single().Body!["set"]!);
        Assert.Equal(new[] { "dx", "dy", "unit_id" }, item.Properties().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal(27, item["unit_id"]!.Value<int>());
        Assert.Equal(-51.1, item["dx"]!.Value<double>(), 6);
        Assert.Equal(64.4, item["dy"]!.Value<double>(), 6);
    }

    [Fact]
    public async Task should_serialize_clear_all_and_clear_list_when_reset_requested()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(14)));
        var patch = new UnitLayoutPatchDto { LayoutVersion = 1, Clear = { 31, 32 }, ClearAll = true };

        await Create(http).PatchAsync(13, patch);

        var body = http.Requests.Single().Body!;
        Assert.True(body["clear_all"]!.Value<bool>());
        Assert.Equal(new[] { 31, 32 }, ((JArray)body["clear"]!).Select(t => t.Value<int>()));
        Assert.Empty((JArray)body["set"]!);
    }

    [Fact]
    public async Task should_return_ok_with_new_document_when_patch_answers_200()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(14, 1, (27, -51.1, 64.4))), etag: "\"14\"");

        var result = await Create(http).PatchAsync(13, MovePatch());

        var ok = Assert.IsType<UnitLayoutWriteResult.Ok>(result);
        Assert.Equal(14, ok.Document.Version);
        Assert.Equal(14, ok.ETagVersion);
    }

    [Fact]
    public async Task should_return_conflict_with_current_version_when_patch_answers_412()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.PreconditionFailed,
            Error("VERSION_CONFLICT", "버전이 다릅니다", new { current_version = 15 }));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal(15, Assert.IsType<UnitLayoutWriteResult.Conflict>(result).CurrentVersion);
    }

    [Fact]
    public async Task should_return_conflict_without_version_when_412_details_are_missing()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.PreconditionFailed, Error("VERSION_CONFLICT", "버전이 다릅니다"));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Null(Assert.IsType<UnitLayoutWriteResult.Conflict>(result).CurrentVersion);
    }

    [Fact]
    public async Task should_return_failed_precondition_required_when_patch_answers_428()
    {
        var http = new ScriptedLayoutHttp().Reply((HttpStatusCode)428, Error("PRECONDITION_REQUIRED", "If-Match 필요"));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal(UnitLayoutFailureKind.PreconditionRequired, Assert.IsType<UnitLayoutWriteResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task should_return_rejected_when_patch_answers_422()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity,
            Error("VALIDATION_ERROR", "검증 실패", new[] { new { field = "body.set.0.unit_id", code = "CONSTRAINT", message = "없는 부대" } }));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.IsType<UnitLayoutWriteResult.Rejected>(result);
    }

    [Fact]
    public async Task should_return_unsupported_when_patch_422_names_path_unit_id()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity, RouteShadow422);

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.IsType<UnitLayoutWriteResult.Unsupported>(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task should_return_unsupported_when_patch_route_is_gone(HttpStatusCode status)
    {
        var http = new ScriptedLayoutHttp().Reply(status, Error("NOT_FOUND", "없음"));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.IsType<UnitLayoutWriteResult.Unsupported>(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, UnitLayoutFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, UnitLayoutFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, UnitLayoutFailureKind.Server)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UnitLayoutFailureKind.Unreachable)]
    [InlineData(HttpStatusCode.GatewayTimeout, UnitLayoutFailureKind.Timeout)]
    public async Task should_return_failed_with_kind_when_patch_fails(HttpStatusCode status, UnitLayoutFailureKind kind)
    {
        var http = new ScriptedLayoutHttp().Reply(status, body: null);

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal(kind, Assert.IsType<UnitLayoutWriteResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task should_not_send_unconditional_write_when_transport_cannot_carry_headers()
    {
        // 헤더를 못 싣는 전송이면 PATCH 를 보내지 않는다 — 조건 없는 쓰기는 말없는 덮어쓰기다(NFR-15).
        var http = new CapturingApiService();
        var service = new UnitLayoutApiService(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(EnumServerContract.V8_0));

        var result = await service.PatchAsync(13, MovePatch());

        Assert.Equal(UnitLayoutFailureKind.NoConditionalTransport, Assert.IsType<UnitLayoutWriteResult.Failed>(result).Kind);
        Assert.Null(http.Method);
    }

    [Fact]
    public async Task should_reject_locally_when_patch_is_null()
    {
        var http = new ScriptedLayoutHttp();

        var result = await Create(http).PatchAsync(13, null!);

        Assert.IsType<UnitLayoutWriteResult.Rejected>(result);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void should_resolve_layout_service_over_a_header_capable_transport_when_device_api_module_is_loaded()
    {
        // 분기 배선 확인(메모리 branch_wiring_must_be_verified) — 등록돼 있고, 실제 전송(ApiService)이 If-Match 를 실을 수 있어야
        // NoConditionalTransport 갈래가 운영에서 죽은 코드가 된다. 네트워크에는 나가지 않는다(해석만).
        var builder = new ContainerBuilder();
        builder.RegisterModule(new Ironwall.Dotnet.Libraries.Devices.Api.Modules.DeviceApiModule(null, new ApiSetupModel { Url = BaseUrl }, "DeviceApi"));
        builder.RegisterInstance(new FixedContractProbe(EnumServerContract.V8_0)).As<IServerContractProbe>();
        using var container = builder.Build();

        var named = container.ResolveNamed<IUnitLayoutApiService>("DeviceApi");
        var unnamed = container.Resolve<IUnitLayoutApiService>();
        var transport = container.ResolveNamed<IApiService>("DeviceApi");

        Assert.IsType<UnitLayoutApiService>(named);
        Assert.Same(named, unnamed);
        Assert.IsAssignableFrom<IApiHeaderRequestService>(transport);
    }

    [Fact]
    public async Task should_return_failed_when_transport_throws()
    {
        var http = new ScriptedLayoutHttp { Throw = true };

        var read = await Create(http).GetAsync();
        var write = await Create(http).PatchAsync(13, MovePatch());

        Assert.IsType<UnitLayoutReadResult.ReadFailed>(read);
        Assert.IsType<UnitLayoutWriteResult.Failed>(write);
    }
    #endregion
}

/// <summary>
/// 공유 배치 시험용 전송 대역 — <see cref="IApiService"/> + <see cref="IApiHeaderRequestService"/>.
/// 요청(메서드 · 경로 · 헤더 · 본문)을 기록하고, 정해 둔 응답을 차례로 돌려준다(마지막 응답은 되풀이).
/// 본문은 실제 <c>ApiService</c> 와 같은 직렬화기(Newtonsoft 기본)를 거친다.
/// </summary>
internal sealed class ScriptedLayoutHttp : IApiService, IApiHeaderRequestService
{
    internal sealed record Captured(string Method, string Endpoint, IReadOnlyDictionary<string, string> Headers, JObject? Body);

    private readonly Queue<(HttpStatusCode Status, string? Body, string? ETag)> _replies = new();
    private (HttpStatusCode Status, string? Body, string? ETag) _last = (HttpStatusCode.OK, "{\"success\":true,\"data\":null}", null);

    public List<Captured> Requests { get; } = new();
    public bool Throw { get; set; }

    public ScriptedLayoutHttp Reply(HttpStatusCode status, string? body, string? etag = null)
    {
        _replies.Enqueue((status, body, etag));
        return this;
    }

    private HttpResponseMessage Next()
    {
        if (_replies.Count > 0) _last = _replies.Dequeue();
        var response = new HttpResponseMessage(_last.Status);
        if (_last.Body != null) response.Content = new StringContent(_last.Body, Encoding.UTF8, "application/json");
        if (_last.ETag != null) response.Headers.TryAddWithoutValidation("ETag", _last.ETag);
        return response;
    }

    private Task<HttpResponseMessage> Capture(string method, string endpoint, object? body, IReadOnlyDictionary<string, string>? headers)
    {
        if (Throw) throw new HttpRequestException("simulated transport failure");
        Requests.Add(new Captured(method, endpoint,
            headers ?? new Dictionary<string, string>(),
            body == null ? null : JObject.Parse(JsonConvert.SerializeObject(body))));
        return Task.FromResult(Next());
    }

    public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string endpoint, object? body, IReadOnlyDictionary<string, string>? headers, CancellationToken token = default)
        => Capture(method.Method, endpoint, body, headers);

    public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => Capture("DELETE", endpoint, null, null);
    public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Capture("DELETE", endpoint, body, null);
    public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Capture("GET", endpoint, null, null);
    public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Capture("PATCH", endpoint, body, null);
    public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Capture("POST", endpoint, null, null);
    public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Capture("POST", endpoint, body, null);
    public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Capture("PUT", endpoint, body, null);

    public void Initialize() { }
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public string Url => string.Empty;
    public string ApiKey => string.Empty;
    public string UserId => string.Empty;
    public string Phone => string.Empty;
}

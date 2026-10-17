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

        // TEST-65 — 428 은 전용 갈래(Failed 의 하위 — 옛 소비자도 Failed 로 받는다). VM 은 배치를 다시 읽는다.
        var precondition = Assert.IsType<UnitLayoutWriteResult.PreconditionRequired>(result);
        Assert.Equal(UnitLayoutFailureKind.PreconditionRequired, precondition.Kind);
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
    public async Task should_treat_every_write_422_as_rejected_even_when_it_names_path_unit_id()
    {
        // FR-50(PRD v1.2 · 분석 ISSUE-1): 422 → 미지원 해석은 **프로브 GET 한정**. 쓰기의 422 는 검증 실패다 —
        // 미지원으로 읽으면 세션 전용으로 조용히 넘어가 서버 거절이 숨는다. 시나리오 SIM-F062 · F073 · F084 · F095.
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity, RouteShadow422);

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.IsType<UnitLayoutWriteResult.Rejected>(result);
    }
    #endregion

    #region - 서버 v8.0.4 계약 맞춤(회신 2026-09-29 · REST §11-A.6 실행 예시 원문) -
    /// <summary>§11-A.6 「낡은 If-Match 412」 원문 — 응답 헤더 <c>ETag: "9"</c> 와 함께 온다.</summary>
    private const string Conflict412V804 =
        "{\"success\":false,\"error\":{\"code\":\"VERSION_CONFLICT\",\"message\":\"배치가 그사이 바뀌었습니다(요청 판 \\\"8\\\", 현재 판 9) — 다시 읽고 재시도하십시오\","
        + "\"details\":{\"current_version\":9}},\"meta\":{\"timestamp\":\"2026-09-29T10:24:57.651507+09:00\",\"request_id\":\"f3fcf6a4-6967-4890-a1c0-b985df8ed953\"}}";

    /// <summary>§11-A.6 「내리기 422」 원문 — 요청 판이 서버 판보다 낮다.</summary>
    private const string Downgrade422V804 =
        "{\"success\":false,\"error\":{\"code\":\"VALIDATION_ERROR\",\"message\":\"배치 판 1 은 서버 판 2 보다 낮습니다 — 옛 자동 배치 규칙으로 계산한 위치라 받을 수 없습니다. 클라이언트를 갱신하십시오\","
        + "\"details\":[{\"field\":\"layout_version\",\"code\":\"VALUE_NOT_ALLOWED\",\"message\":\"배치 판 1 은 서버 판 2 보다 낮습니다\"}]},"
        + "\"meta\":{\"timestamp\":\"2026-09-29T10:24:57.683840+09:00\",\"request_id\":\"080ff76d-feb8-452a-8fd2-ba3730943693\"}}";

    /// <summary>§11-A.6 「판 올림인데 clear_all 없음 422」 원문.</summary>
    private const string BumpWithoutClearAll422V804 =
        "{\"success\":false,\"error\":{\"code\":\"VALIDATION_ERROR\",\"message\":\"배치 판을 1 → 2 로 올리려면 clear_all: true 를 함께 보내야 합니다\","
        + "\"details\":[{\"field\":\"clear_all\",\"code\":\"CONSTRAINT\",\"message\":\"배치 판을 1 → 2 로 올리려면 clear_all: true 를 함께 보내야 합니다\"}]},"
        + "\"meta\":{\"timestamp\":\"2026-09-29T10:24:57.662169+09:00\",\"request_id\":\"b18602ae-df17-4182-a55e-5074075beebe\"}}";

    [Fact]
    public async Task should_return_conflict_with_current_version_when_412_is_the_v8_0_4_body()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.PreconditionFailed, Conflict412V804, etag: "\"9\"");

        var result = await Create(http).PatchAsync(8, MovePatch());

        Assert.Equal(9, Assert.IsType<UnitLayoutWriteResult.Conflict>(result).CurrentVersion);
    }

    [Fact]
    public async Task should_read_current_version_from_etag_when_412_details_are_missing()
    {
        // 회신 §1 ④ — 412 응답 헤더 ETag 에도 현재 판을 싣는다(다시 읽기 전에 판을 알 수 있게). details 가 없어도 판을 잃지 않는다.
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.PreconditionFailed, Error("VERSION_CONFLICT", "판이 바뀌었습니다"), etag: "\"9\"");

        var result = await Create(http).PatchAsync(8, MovePatch());

        Assert.Equal(9, Assert.IsType<UnitLayoutWriteResult.Conflict>(result).CurrentVersion);
    }

    [Fact]
    public async Task should_return_conflict_when_412_code_is_precondition_failed()
    {
        // §12.2 — 412 에는 VERSION_CONFLICT(지금) · PRECONDITION_FAILED(매핑 폴백) 둘이 올 수 있다. 상태로 가른다.
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.PreconditionFailed, Error("PRECONDITION_FAILED", "전제 조건 불일치"), etag: "\"12\"");

        var result = await Create(http).PatchAsync(8, MovePatch());

        Assert.Equal(12, Assert.IsType<UnitLayoutWriteResult.Conflict>(result).CurrentVersion);
    }

    [Fact]
    public async Task should_mark_client_outdated_when_422_names_layout_version()
    {
        // 회신 §2 — details[0].field == layout_version → "클라이언트 갱신 필요"(서버 판이 더 높다).
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity, Downgrade422V804);

        var result = await Create(http).PatchAsync(10, MovePatch());

        Assert.Equal(UnitLayoutRejectKind.ClientOutdated, Assert.IsType<UnitLayoutWriteResult.Rejected>(result).Kind);
    }

    [Fact]
    public async Task should_mark_bump_needs_clear_all_when_422_names_clear_all()
    {
        // 회신 §2 — details[0].field == clear_all → 판 올림 요청 형식 오류.
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity, BumpWithoutClearAll422V804);

        var result = await Create(http).PatchAsync(9, new UnitLayoutPatchDto { LayoutVersion = 2 });

        Assert.Equal(UnitLayoutRejectKind.BumpNeedsClearAll, Assert.IsType<UnitLayoutWriteResult.Rejected>(result).Kind);
    }

    [Fact]
    public async Task should_mark_plain_validation_when_422_names_another_field()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.UnprocessableEntity,
            Error("VALIDATION_ERROR", "검증 실패", new[] { new { field = "unit_id", code = "CONSTRAINT", message = "없는 부대" } }));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal(UnitLayoutRejectKind.Validation, Assert.IsType<UnitLayoutWriteResult.Rejected>(result).Kind);
    }

    [Fact]
    public async Task should_reject_locally_without_network_when_set_and_clear_exceed_1000_items()
    {
        // §11-A.6 — set · clear 합산 최대 1,000 항목. 넘으면 서버가 422 로 전체를 거절하므로 보내지 않는다.
        var http = new ScriptedLayoutHttp();
        var patch = new UnitLayoutPatchDto { LayoutVersion = 1 };
        for (var id = 1; id <= 600; id++) patch.Set.Add(new UnitLayoutItemDto { UnitId = id, Dx = 1, Dy = 1 });
        for (var id = 601; id <= 1001; id++) patch.Clear.Add(id);

        var result = await Create(http).PatchAsync(13, patch);

        Assert.Equal(UnitLayoutRejectKind.Validation, Assert.IsType<UnitLayoutWriteResult.Rejected>(result).Kind);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task should_send_when_set_and_clear_are_exactly_1000_items()
    {
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(14, 1)), etag: "\"14\"");
        var patch = new UnitLayoutPatchDto { LayoutVersion = 1 };
        for (var id = 1; id <= 500; id++) patch.Set.Add(new UnitLayoutItemDto { UnitId = id, Dx = 1, Dy = 1 });
        for (var id = 501; id <= 1000; id++) patch.Clear.Add(id);

        var result = await Create(http).PatchAsync(13, patch);

        Assert.IsType<UnitLayoutWriteResult.Ok>(result);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task should_send_bump_body_with_clear_all_and_higher_layout_version_when_bumping()
    {
        // 회신 §2 1번 안 — 판 올림 = clear_all:true + 더 큰 layout_version + If-Match(한쪽만 성공 · 다른 쪽 412).
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.OK, Envelope(Doc(10, 2)), etag: "\"10\"");

        var result = await Create(http).PatchAsync(9, new UnitLayoutPatchDto { LayoutVersion = 2, ClearAll = true });

        var ok = Assert.IsType<UnitLayoutWriteResult.Ok>(result);
        Assert.Equal(2, ok.Document.LayoutVersion);
        Assert.Equal(10, ok.ETagVersion);
        var sent = Assert.Single(http.Requests);
        Assert.Equal("\"9\"", sent.Headers["If-Match"]);
        Assert.Equal(2, (int)sent.Body!["layout_version"]!);
        Assert.True((bool)sent.Body!["clear_all"]!);
        Assert.Empty((JArray)sent.Body!["set"]!);
        Assert.Empty((JArray)sent.Body!["clear"]!);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task should_return_endpoint_gone_when_patch_route_is_gone(HttpStatusCode status)
    {
        // TEST-65 — 지원 중이던 경로가 사라졌다 → EndpointGone(Unsupported 의 하위 — VM 은 세션 전용으로 전환, SIM-F059).
        var http = new ScriptedLayoutHttp().Reply(status, Error("NOT_FOUND", "없음"));

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal((int)status, Assert.IsType<UnitLayoutWriteResult.EndpointGone>(result).StatusCode);
        Assert.IsAssignableFrom<UnitLayoutWriteResult.Unsupported>(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, UnitLayoutFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, UnitLayoutFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, UnitLayoutFailureKind.Server)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UnitLayoutFailureKind.Unreachable)]
    [InlineData(HttpStatusCode.Conflict, UnitLayoutFailureKind.Other)]
    public async Task should_return_failed_with_kind_when_patch_fails(HttpStatusCode status, UnitLayoutFailureKind kind)
    {
        var http = new ScriptedLayoutHttp().Reply(status, body: null);

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal(kind, Assert.IsType<UnitLayoutWriteResult.Failed>(result).Kind);
    }

    [Fact]
    public async Task should_return_unknown_when_patch_times_out_because_the_write_may_have_landed()
    {
        // TEST-65 — 504(ApiService 합성 · 본문 없음)는 "반영됐을 수 있음" → Unknown(Failed 의 하위, Kind=Timeout). VM 은 다시 읽어 확정한다(SIM-F065).
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.GatewayTimeout, body: null);

        var result = await Create(http).PatchAsync(13, MovePatch());

        Assert.Equal(UnitLayoutFailureKind.Timeout, Assert.IsType<UnitLayoutWriteResult.Unknown>(result).Kind);
    }

    [Fact]
    public async Task should_read_fail_not_unsupported_when_probe_answers_400()
    {
        // 결정 D-2026-09-27-638a63 — 400 은 경로 부재의 증거가 아니다(SIM-P017~018 카탈로그 기대 '미지원' 대체).
        var http = new ScriptedLayoutHttp().Reply(HttpStatusCode.BadRequest, Error("BAD_REQUEST", "잘못된 요청"));

        var result = await Create(http).GetAsync();

        Assert.Equal(UnitLayoutFailureKind.Other, Assert.IsType<UnitLayoutReadResult.ReadFailed>(result).Kind);
    }

    [Fact]
    public void should_keep_the_iapiservice_member_list_unchanged_when_headers_travel_through_the_new_interface()
    {
        // NFR-10 · ISSUE-5 — If-Match 는 IApiHeaderRequestService 로만 나간다. IApiService 에 멤버를 더하면 여러 시험 프로젝트의 가짜가 깨진다.
        var members = typeof(IApiService).GetMembers()
            .Select(m => m is System.Reflection.MethodInfo mi ? $"{mi.Name}({mi.GetParameters().Length})" : m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[]
        {
            "ApiKey", "DeleteRequestAsync(1)", "DeleteRequestAsync(2)", "get_ApiKey(0)", "get_Phone(0)", "get_Url(0)", "get_UserId(0)",
            "GetRequestAsync(2)", "Initialize(0)", "PatchRequestAsync(2)", "Phone", "PostFormDataRequestAsync(2)", "PostRequestAsync(2)",
            "PutRequestAsync(2)", "Url", "UserId",
        }.OrderBy(n => n, StringComparer.Ordinal), members);
        Assert.False(typeof(IApiHeaderRequestService).IsAssignableFrom(typeof(IApiService)));
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

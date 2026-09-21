using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Servers;
/****************************************************************************
   Purpose      : 서버 축 API 통로 — 판본별 본문·응답을 한 곳에서 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>한 번의 호출 결과 — 실패는 <b>문장</b>으로 돌아온다(예외로 새지 않는다).</summary>
public sealed record ServerAxisResult(bool IsSuccess, string Message, int StatusCode = 0, int Id = 0);

/// <summary>목록 한 번의 결과.</summary>
public sealed record ServerAxisListResult(
    bool IsSuccess,
    IReadOnlyList<ServerAxisView> Servers,
    bool IsTruncated,
    string? Message);

/// <summary>서버 분류 한 줄 — 6.3 의 <c>category_id</c> ↔ 7.0+ 의 <c>category_server</c> 를 잇는다.</summary>
public sealed record ServerCategoryView(int Id, string Name, string TypeServer);

/// <summary>
/// 서버 콘솔이 쓰는 <b>판본 인식</b> 통로. 기존 <see cref="Services.IServerApiService"/> 를 넓히지 않는
/// <b>새 인터페이스</b>다 — 그 인터페이스의 페이크가 레포 곳곳에 있다.
/// </summary>
public interface IServerAxisApiService
{
    EnumServerContract Contract { get; }

    Task<IReadOnlyList<ServerCategoryView>> GetCategoriesAsync(CancellationToken token = default);

    Task<ServerAxisListResult> GetServersAsync(int? unitId, bool includeDescendants, CancellationToken token = default);

    Task<ServerAxisView?> GetServerAsync(int id, CancellationToken token = default);

    Task<ServerAxisResult> CreateServerAsync(ServerWriteIntent intent, CancellationToken token = default);

    Task<ServerAxisResult> PatchServerAsync(int id, ServerWriteIntent intent, CancellationToken token = default);

    /// <summary>
    /// 장비의 관리 서버를 바꾼다 — 병합 패치 본문 <c>{ "server_id": id|null }</c> 하나.
    /// </summary>
    /// <param name="serverId"><c>null</c> = 해제. 6.3 에서는 해제를 보낼 수 없어 거절한다.</param>
    Task<ServerAxisResult> AssignDeviceServerAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, int? unitId, CancellationToken token = default);
}

/// <summary>
/// <see cref="IApiService"/> 위에 판본 분기를 씌운 통로. 응답은 원문 JSON 으로 읽어
/// <b>키의 부재</b>까지 보존한다(<see cref="ServerAxisReader"/>).
/// </summary>
public sealed class ServerAxisApiService : IServerAxisApiService
{
    private const int PageLimit = 100;
    private const int MaxPages = 20;

    private readonly IApiService _api;
    private readonly ApiSetupModel _setup;
    private readonly IServerContractProbe? _probe;
    private readonly ILogService? _log;

    public ServerAxisApiService(IApiService api, ApiSetupModel setup, IServerContractProbe? probe = null, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _probe = probe;
        _log = log;
    }

    public EnumServerContract Contract => _probe?.Contract ?? EnumServerContract.V6_3;

    private bool IsAxisEra => Contract >= EnumServerContract.V7_0;

    /// <summary>카테고리별 장비 경로 — 센서는 관리 서버가 없어 자리가 없다(<c>device.py:645-647</c>).</summary>
    public static string? DevicePathOf(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "controllers",
        EnumDeviceCategory.Camera => "cameras",
        EnumDeviceCategory.Speaker => "speakers",
        EnumDeviceCategory.Enclosure => "enclosures",
        EnumDeviceCategory.Lamp => "lamps",
        EnumDeviceCategory.Gate => "gates",
        _ => null,
    };

    #region - Read -
    public async Task<IReadOnlyList<ServerCategoryView>> GetCategoriesAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _api.GetRequestAsync($"{_setup.Url}/servers/categories",
                new Dictionary<string, string> { ["page"] = "1", ["limit"] = PageLimit.ToString() }).ConfigureAwait(false);

            var envelope = await ReadAsync(response).ConfigureAwait(false);
            if (envelope?["data"] is not JArray rows) return Array.Empty<ServerCategoryView>();

            return rows.OfType<JObject>()
                .Select(row => new ServerCategoryView(
                    row.Value<int?>("id") ?? 0,
                    row.Value<string>("name") ?? string.Empty,
                    // 7.0 은 판별자를 category_server 로 개명했다(server.py:71 SERVER_CATEGORY_REMOVED_FIELDS).
                    row.Value<string>("category_server") ?? row.Value<string>("type_server") ?? string.Empty))
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(ServerAxisApiService)}] 서버 분류를 받지 못했습니다: {ex.Message}");
            return Array.Empty<ServerCategoryView>();
        }
    }

    public async Task<ServerAxisListResult> GetServersAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
    {
        var categories = await GetCategoriesAsync(token).ConfigureAwait(false);
        var typeOf = categories.ToDictionary(c => c.Id, c => c.TypeServer);

        var collected = new List<ServerAxisView>();
        var truncated = false;

        try
        {
            for (var page = 1; page <= MaxPages; page++)
            {
                token.ThrowIfCancellationRequested();

                var parameters = new Dictionary<string, string>
                {
                    ["page"] = page.ToString(),
                    ["limit"] = PageLimit.ToString(),
                };
                // view=full 은 7.0+ 파라미터다. 6.3 은 모르는 쿼리를 조용히 버린다.
                if (IsAxisEra) parameters["view"] = "full";
                if (Contract >= EnumServerContract.V8_0 && unitId is > 0)
                {
                    parameters["unit_id"] = unitId.Value.ToString();
                    parameters["include_descendants"] = includeDescendants ? "true" : "false";
                }

                var response = await _api.GetRequestAsync($"{_setup.Url}/servers", parameters).ConfigureAwait(false);
                var envelope = await ReadAsync(response).ConfigureAwait(false);

                if (envelope is null || envelope.Value<bool?>("success") != true)
                    return new ServerAxisListResult(false, Array.Empty<ServerAxisView>(), false,
                        $"서버 목록을 받지 못했습니다 — {Message(envelope) ?? $"HTTP {(int)response.StatusCode}"}");

                if (envelope["data"] is JArray rows)
                    collected.AddRange(rows.OfType<JObject>()
                        .Select(row => ServerAxisReader.Parse(row, Contract, id => typeOf.TryGetValue(id, out var type) ? type : null)));

                var pagination = envelope["pagination"] as JObject;
                var total = pagination?.Value<int?>("total") ?? envelope.Value<int?>("total");
                if (total is null) { truncated = collected.Count > 0; break; }
                if (page * PageLimit >= total.Value) break;
            }

            return new ServerAxisListResult(true, collected, truncated, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ServerAxisApiService)}] 목록 적재 실패 — {ex.Message}");
            return new ServerAxisListResult(false, Array.Empty<ServerAxisView>(), false, "서버 목록을 받지 못했습니다 — 서버에 닿지 못했습니다");
        }
    }

    public async Task<ServerAxisView?> GetServerAsync(int id, CancellationToken token = default)
    {
        if (id <= 0) return null;
        try
        {
            var parameters = new Dictionary<string, string>();
            if (IsAxisEra)
            {
                parameters["view"] = "full";
                parameters["include"] = "connection,server_config";
            }

            var response = await _api.GetRequestAsync($"{_setup.Url}/servers/{id}", parameters).ConfigureAwait(false);
            var envelope = await ReadAsync(response).ConfigureAwait(false);
            if (envelope is null || envelope.Value<bool?>("success") != true) return null;

            // 6.3 은 판별자를 분류에서 얻는다 — 단건은 분류를 다시 부르지 않고 목록이 채운 값을 화면이 유지한다.
            return envelope["data"] is JObject row ? ServerAxisReader.Parse(row, Contract) : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ServerAxisApiService)}] 서버 {id} 단건 조회 실패 — {ex.Message}");
            return null;
        }
    }
    #endregion

    #region - Write -
    public async Task<ServerAxisResult> CreateServerAsync(ServerWriteIntent intent, CancellationToken token = default)
    {
        if (intent is null) return new ServerAxisResult(false, "보낼 것이 없습니다");
        try
        {
            var body = ServerAxisWriter.BuildCreate(intent, Contract);
            var response = await _api.PostRequestAsync($"{_setup.Url}/servers", body).ConfigureAwait(false);
            var envelope = await ReadAsync(response).ConfigureAwait(false);

            if (envelope?.Value<bool?>("success") == true)
                return new ServerAxisResult(true, string.Empty, (int)response.StatusCode,
                    (envelope["data"] as JObject)?.Value<int?>("id") ?? 0);

            var reason = (int)response.StatusCode == 409
                ? $"같은 이름의 서버가 이미 있습니다 — {Message(envelope)}"
                : Message(envelope) ?? $"HTTP {(int)response.StatusCode}";
            return new ServerAxisResult(false, $"등록하지 못했습니다 — {reason}", (int)response.StatusCode);
        }
        catch (OperationCanceledException) { return new ServerAxisResult(false, "등록을 취소했습니다"); }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ServerAxisApiService)}] 서버 등록 실패 — {ex.Message}");
            return new ServerAxisResult(false, "등록하지 못했습니다 — 서버에 닿지 못했습니다");
        }
    }

    public async Task<ServerAxisResult> PatchServerAsync(int id, ServerWriteIntent intent, CancellationToken token = default)
    {
        if (id <= 0) return new ServerAxisResult(false, "아직 등록되지 않은 서버입니다");
        if (intent is null) return new ServerAxisResult(false, "바뀐 것이 없습니다");

        try
        {
            var body = ServerAxisWriter.BuildPatch(intent, Contract);
            if (!body.Properties().Any()) return new ServerAxisResult(true, "바뀐 것이 없습니다", 200, id);

            var response = await _api.PatchRequestAsync($"{_setup.Url}/servers/{id}", body).ConfigureAwait(false);
            var envelope = await ReadAsync(response).ConfigureAwait(false);

            if (envelope?.Value<bool?>("success") == true)
                return new ServerAxisResult(true, string.Empty, (int)response.StatusCode, id);

            var reason = (int)response.StatusCode == 409
                ? $"같은 이름의 서버가 이미 있습니다 — {Message(envelope)}"
                : Message(envelope) ?? $"HTTP {(int)response.StatusCode}";
            return new ServerAxisResult(false, $"저장하지 못했습니다 — {reason}", (int)response.StatusCode);
        }
        catch (OperationCanceledException) { return new ServerAxisResult(false, "저장을 취소했습니다"); }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ServerAxisApiService)}] 서버 {id} 저장 실패 — {ex.Message}");
            return new ServerAxisResult(false, "저장하지 못했습니다 — 서버에 닿지 못했습니다");
        }
    }

    public async Task<ServerAxisResult> AssignDeviceServerAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, int? unitId, CancellationToken token = default)
    {
        var path = DevicePathOf(category);
        if (path is null)
            return new ServerAxisResult(false, "이 카테고리에는 관리 서버가 없습니다 — 소속 제어기를 따릅니다");
        if (deviceId <= 0) return new ServerAxisResult(false, "아직 등록되지 않은 장비입니다");

        // 해제는 축 계약의 병합 패치(키 null = 삭제)로만 보낼 수 있다.
        if (serverId is null && !ServerAxisWriter.SupportsClearing(Contract))
            return new ServerAxisResult(false, "이 서버 판본(6.3)에는 배정 해제 입구가 없습니다");

        try
        {
            var body = ServerAxisWriter.BuildDeviceAssign(serverId, unitId, Contract);
            var response = await _api.PatchRequestAsync($"{_setup.Url}/devices/{path}/{deviceId}", body).ConfigureAwait(false);
            var envelope = await ReadAsync(response).ConfigureAwait(false);

            return envelope?.Value<bool?>("success") == true
                ? new ServerAxisResult(true, string.Empty, (int)response.StatusCode, deviceId)
                : new ServerAxisResult(false, Message(envelope) ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
        }
        catch (OperationCanceledException) { return new ServerAxisResult(false, "배정을 취소했습니다"); }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ServerAxisApiService)}] 장비 {deviceId} → 서버 {serverId} 실패 — {ex.Message}");
            return new ServerAxisResult(false, "서버에 닿지 못했습니다");
        }
    }
    #endregion

    #region - Envelope -
    /// <summary>응답 본문을 <b>원문 그대로</b> 읽는다 — 키의 부재를 보존해야 "보고 없음" 을 알 수 있다.</summary>
    private static async Task<JObject?> ReadAsync(HttpResponseMessage response)
    {
        if (response?.Content is null) return null;
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            // 날짜를 DateTime 으로 바꾸지 않는다 — 바꾸면 오프셋이 날아가고 시각이 현지 표기가 된다.
            using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(text))
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
            };
            return JObject.Load(reader);
        }
        catch (Newtonsoft.Json.JsonException) { return null; }
    }

    private static string? Message(JObject? envelope)
    {
        var detail = (envelope?["error"] as JObject)?.Value<string>("message");
        if (!string.IsNullOrWhiteSpace(detail)) return detail;
        var message = envelope?.Value<string>("message");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
    #endregion
}

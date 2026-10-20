using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : map-3d 서버(기본 http://127.0.0.1:8765) HTTP 창구 — 굽기 시작 · 진행 조회 · 지점을 덮는 장면 찾기.
                  GIS 는 서버를 띄우지 않는다(POC). 닿지 않으면 Map3DServerUnavailableException(호출자가 "꺼져 있습니다" 배너).
   Note         : WPF 무의존 — HttpMessageHandler 를 받아 가짜 응답으로 시험한다(tests/GMaps.Ui.Tests/Map3DClientTests).
   Created On   : 2026-10-16 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>굽기 작업 상태. Status = "queued" | "running" | "done" | "error" | "cancelled", Progress 0~1.</summary>
public sealed record Map3DJob(string Id, string Status, double Progress, string? Message)
{
    public bool IsDone => Status == "done";
    public bool IsError => Status == "error";
    /// <summary>취소로 끝남(map-3d 「만들기 취소」, 2026-10-19).</summary>
    public bool IsCancelled => Status == "cancelled";
}

/// <summary>
/// 굽기 취소 요청 결과(POST /api/jobs/{id}/cancel). Result = "cancelled" | "requested"(곧 멈춤) | "too_late"(GLB 쓰는 중 — 곧 다 만든다)
/// | "already" | "missing", 서버가 취소를 모르면(옛 판, 404 · 405) "unsupported".
/// </summary>
public sealed record Map3DCancelResult(string Result)
{
    public const string Unsupported = "unsupported", TooLate = "too_late";
    /// <summary>옛 서버 — 취소 API 가 없다.</summary>
    public bool IsUnsupported => Result == Unsupported;
    /// <summary>거의 다 만들어(GLB 쓰는 중) 취소하지 않았다 — 곧 끝난다.</summary>
    public bool IsTooLate => Result == TooLate;
}

/// <summary>구운 장면. Id 는 3D 화면 주소의 job 값으로 쓴다. Bbox = [minLon, minLat, maxLon, maxLat].</summary>
public sealed record Map3DScene(string Id, double[] Bbox)
{
    /// <summary>그 영역의 3D 로 바뀌는 줌(scene.json enter_zoom) — 없으면 null(세팅값을 쓴다).</summary>
    public double? EnterZoom { get; init; }

    /// <summary>회전 영역(scene.json rect, 2026-10-19) — 없으면 null(북쪽 정렬 bbox 영역 · 옛 장면). 안/밖은 <see cref="Map3DMath.SceneContains"/>.</summary>
    public Map3DRotRect? Rect { get; init; }
}

/// <summary>구운 장면 목록 항목(GET /api/jobs 의 scenes) — 2D 지도 3D 영역 표시용. 모르는 값은 null.</summary>
public sealed record Map3DSceneInfo(string Id, string? Name, double[] Bbox, DateTime? Created, int? Buildings, string? Kind)
{
    /// <summary>그 영역의 3D 로 바뀌는 줌(scene.json enter_zoom) — 없으면 null(세팅값을 쓴다).</summary>
    public double? EnterZoom { get; init; }

    /// <summary>직접 입력한 건물 높이 수(manual_heights) — 옛 서버면 0.</summary>
    public int ManualHeights { get; init; }

    /// <summary>다시 굽기에서 다시 붙이지 못한 직접 입력 수(manual_unmatched) — 옛 서버면 0.</summary>
    public int ManualUnmatched { get; init; }

    /// <summary>회전 영역(rect) — 없으면 null(북쪽 정렬 bbox 영역).</summary>
    public Map3DRotRect? Rect { get; init; }

    /// <summary>직접 그린 건물 수(user_buildings — 아직 다시 만들지 않은 것 포함) — 옛 서버면 0.</summary>
    public int UserBuildings { get; init; }
}

/// <summary>건물 높이 저장 · 되돌리기 응답 — Building = 서버가 고친 건물 줄(3D 에 그대로 보낸다), Pick = 그 해석.</summary>
public sealed record Map3DBuildingSaved(JObject Building, Map3DBuildingPick Pick);

/// <summary>서버에 닿지 않음(연결 거부 · 시간 초과).</summary>
public sealed class Map3DServerUnavailableException : Exception
{
    public Map3DServerUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    public Map3DServerUnavailableException(string message, Exception? inner, bool isTimeout) : base(message, inner) { IsTimeout = isTimeout; }

    /// <summary>응답 시간 초과 — 요청은 서버에 닿았을 수 있다(굽기 시작이면 서버가 이미 만들고 있을 수 있다). 연결 거부면 false.</summary>
    public bool IsTimeout { get; }
}

/// <summary>서버가 요청을 거절함(예: 한 변 2000 m 초과 → 400). Message = 서버 detail.</summary>
public sealed class Map3DRequestException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public Map3DRequestException(HttpStatusCode statusCode, string detail) : base(detail) { StatusCode = statusCode; }
}

public sealed class Map3DClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _server;

    /// <param name="serverUrl">서버 주소(끝 '/' 없음 — Map3DModel.Normalize 결과).</param>
    /// <param name="handler">시험용 가짜 처리기(없으면 기본).</param>
    public Map3DClient(string serverUrl, HttpMessageHandler? handler = null)
    {
        _server = serverUrl.TrimEnd('/');
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;   // 시간 제한은 요청마다(SendAsync) — 굽기 시작은 큰 이미지 본문을 올린다
    }

    /// <summary>보통 요청(조회 · 저장 · 지우기)의 응답 시간 제한.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 굽기 시작(POST /api/jobs)의 시간 제한 — 본문에 오버레이 이미지(수십 MB)가 실려 올리기만 10 초를 넘을 수 있다.
    /// 10 초로 끊으면 서버는 이미 굽기를 시작했는데 GIS 는 "닿지 않음"으로 알려, 다시 누르면 같은 영역을 두 번 구웠다.
    /// </summary>
    public TimeSpan BakeStartTimeout { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>엔진 살아 있는지 묻기(<see cref="IsHealthyAsync"/>)의 응답 시간 제한 — 짧게(다시 연결 감시가 5 · 10 초마다 묻는다).</summary>
    public TimeSpan HealthTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 엔진이 살아 있는가 — GET /api/health 200 이면 true. 그 주소가 없는 옛 map-3d(404)면 GET /api/config 200 으로 본다.
    /// 닿지 않음 · 시간 초과 · 그 밖의 응답은 false. 예외를 던지지 않는다(감시 타이머에서 부른다).
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var status = await ProbeAsync("/api/health", ct).ConfigureAwait(false);
            if (status == HttpStatusCode.NotFound) status = await ProbeAsync("/api/config", ct).ConfigureAwait(false);   // 옛 map-3d
            return status == HttpStatusCode.OK;
        }
        catch (Exception) { return false; }   // 닿지 않음 · 시간 초과 · 취소 · 닫힌 창구 — 모두 "살아 있지 않음"
    }

    private async Task<HttpStatusCode> ProbeAsync(string path, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(HealthTimeout);
        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, _server + path),
                                                   HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
        return response.StatusCode;
    }

    /// <summary>굽기 시작(POST /api/jobs) → 202 작업.</summary>
    public Task<Map3DJob> StartBakeAsync(double[] bbox, CancellationToken ct = default)
        => StartBakeAsync(bbox, Map3DBake.ModeTiles, null, ct);

    /// <summary>굽기 시작 — 오버레이 이미지 포함 방식(overlay_mode) · 영역과 겹치는 이미지(overlays) 함께. 본문 = Map3DBake.BuildBody.</summary>
    public Task<Map3DJob> StartBakeAsync(double[] bbox, string overlayMode, IReadOnlyList<Map3DBakeOverlay>? overlays, CancellationToken ct = default)
        => StartBakeAsync(bbox, overlayMode, overlays, null, ct);

    /// <summary>굽기 시작 — 그 영역의 3D 로 바뀌는 줌(enter_zoom)도 함께 보낸다(map-3d 가 scene.json 에 기록).</summary>
    public Task<Map3DJob> StartBakeAsync(double[] bbox, string overlayMode, IReadOnlyList<Map3DBakeOverlay>? overlays, double? enterZoom,
                                         CancellationToken ct = default)
        => StartBakeAsync(bbox, overlayMode, overlays, enterZoom, null, ct);

    /// <summary>굽기 시작 — 회전 영역(rect)이면 그것도 보낸다(bbox 는 감싸는 상자, 옛 서버는 rect 를 모르고 bbox 로 굽는다).</summary>
    public async Task<Map3DJob> StartBakeAsync(double[] bbox, string overlayMode, IReadOnlyList<Map3DBakeOverlay>? overlays, double? enterZoom,
                                               Map3DRotRect? rect, CancellationToken ct = default)
    {
        var body = Map3DBake.BuildBody(bbox, overlayMode, overlays, enterZoom, rect).ToString(Formatting.None);
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Post, Map3DMath.JobsUrl(_server))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }, allowNotFound: false, ct, BakeStartTimeout).ConfigureAwait(false);
        return ParseJob(json!);
    }

    /// <summary>작업 진행 조회(GET /api/jobs/{id}).</summary>
    public async Task<Map3DJob> GetJobAsync(string id, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Get, Map3DMath.JobUrl(_server, id)), allowNotFound: false, ct).ConfigureAwait(false);
        return ParseJob(json!);
    }

    /// <summary>
    /// 굽기 취소(POST /api/jobs/{id}/cancel) — 서버는 늘 200(없는 작업 · 이미 끝난 작업도 무해한 응답)이라
    /// 404 · 405 는 그 기능이 없는 옛 서버로 본다(<see cref="Map3DCancelResult.Unsupported"/>). 그 밖의 거절은 Map3DRequestException.
    /// </summary>
    public async Task<Map3DCancelResult> CancelJobAsync(string id, CancellationToken ct = default)
    {
        string? json;
        try
        {
            json = await SendAsync(new HttpRequestMessage(HttpMethod.Post, $"{Map3DMath.JobUrl(_server, id)}/cancel"), allowNotFound: true, ct).ConfigureAwait(false);
        }
        catch (Map3DRequestException ex) when (ex.StatusCode == HttpStatusCode.MethodNotAllowed) { json = null; }
        if (json == null) return new Map3DCancelResult(Map3DCancelResult.Unsupported);
        string? result = null;
        try { if (JObject.Parse(json)["result"] is JValue { Type: JTokenType.String } v) result = v.ToString(); }
        catch (JsonException) { }
        return new Map3DCancelResult(string.IsNullOrEmpty(result) ? "requested" : result);
    }

    /// <summary>지점을 덮는 장면(GET /api/scenes/at) — 없으면(404) null.</summary>
    public async Task<Map3DScene?> FindSceneAtAsync(double lon, double lat, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Get, Map3DMath.SceneAtUrl(_server, lon, lat)), allowNotFound: true, ct).ConfigureAwait(false);
        if (json == null) return null;
        var o = JObject.Parse(json);
        var bbox = o["bbox"]?.ToObject<double[]>();
        string? id = o["id"]?.ToString();
        if (string.IsNullOrEmpty(id) || bbox == null || bbox.Length != 4)
            throw new Map3DRequestException(HttpStatusCode.OK, "장면 응답 형식이 올바르지 않습니다");
        return new Map3DScene(id, bbox) { EnterZoom = ReadZoom(o["enter_zoom"]), Rect = Map3DRotRect.Parse(o["rect"]) };
    }

    /// <summary>구운 장면 목록(GET /api/jobs 의 scenes).</summary>
    public async Task<IReadOnlyList<Map3DSceneInfo>> ListScenesAsync(CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Get, Map3DMath.JobsUrl(_server)), allowNotFound: false, ct).ConfigureAwait(false);
        return ParseScenes(json!);
    }

    /// <summary>작업 목록(GET /api/jobs) — 구운 장면과 지금 만드는 중(queued · running)인 작업 수(옛 서버라 running 이 없으면 0).</summary>
    public async Task<(IReadOnlyList<Map3DSceneInfo> Scenes, int Running)> ListJobsAsync(CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Get, Map3DMath.JobsUrl(_server)), allowNotFound: false, ct).ConfigureAwait(false);
        return (ParseScenes(json!), ParseRunningCount(json!));
    }

    internal static int ParseRunningCount(string json)
    {
        try { return JToken.Parse(json) is JObject o && o["running"] is JArray running ? running.Count : 0; }
        catch (JsonException) { return 0; }
    }

    /// <summary>굽은 장면 지우기(DELETE /api/jobs/{id}) — 지웠으면 true, 이미 없으면(404) false. 지울 수 없는 장면(예: 견본 405)은 Map3DRequestException.</summary>
    public async Task<bool> DeleteSceneAsync(string id, CancellationToken ct = default)
        => await SendAsync(new HttpRequestMessage(HttpMethod.Delete, Map3DMath.JobUrl(_server, id)), allowNotFound: true, ct).ConfigureAwait(false) != null;

    /// <summary>
    /// 영역의 3D 로 바뀌는 줌 바꾸기(PATCH /api/jobs/{id} {"enter_zoom":f}) — 다시 굽지 않는다.
    /// 서버가 저장했으면 true, 그 기능을 모르면(404 · 405) false — 호출자가 GIS 로컬 설정(Map3DAreaZoomStore)에 저장한다.
    /// 그 밖의 거절은 Map3DRequestException, 닿지 않으면 Map3DServerUnavailableException.
    /// </summary>
    public async Task<bool> UpdateSceneEnterZoomAsync(string id, double enterZoom, CancellationToken ct = default)
    {
        var body = new JObject { ["enter_zoom"] = enterZoom }.ToString(Formatting.None);
        try
        {
            return await SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), Map3DMath.JobUrl(_server, id))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }, allowNotFound: true, ct).ConfigureAwait(false) != null;
        }
        catch (Map3DRequestException ex) when (ex.StatusCode == HttpStatusCode.MethodNotAllowed) { return false; }
    }

    /// <summary>
    /// GET /api/jobs 응답의 scenes 해석 — {"scenes":[...]} 또는 배열 그대로. id 나 bbox(수 4개)가 없는 항목은 건너뛴다.
    /// created = ISO 문자열 또는 유닉스 초, 건물 수 = counts.buildings.
    /// </summary>
    internal static IReadOnlyList<Map3DSceneInfo> ParseScenes(string json)
    {
        // 날짜는 문자열 그대로 받아(DateParseHandling.None) 시간대 없는 값을 지역 시각으로 읽는다
        using var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var root = JToken.ReadFrom(reader);
        var array = root as JArray ?? (root as JObject)?["scenes"] as JArray;
        var list = new List<Map3DSceneInfo>();
        if (array == null) return list;
        foreach (var o in array.OfType<JObject>())
        {
            string? id = o["id"]?.Type == JTokenType.String ? o.Value<string>("id") : null;
            var bboxToken = o["bbox"] as JArray;
            if (string.IsNullOrEmpty(id) || bboxToken == null || bboxToken.Count != 4
                || bboxToken.Any(t => t.Type is not (JTokenType.Float or JTokenType.Integer))) continue;
            var counts = o["counts"] as JObject;
            list.Add(new Map3DSceneInfo(
                id,
                o["name"]?.Type == JTokenType.String ? o.Value<string>("name") : null,
                bboxToken.Select(t => t.Value<double>()).ToArray(),
                ParseCreated(o["created"]),
                counts?["buildings"]?.Type == JTokenType.Integer ? counts.Value<int>("buildings") : null,
                o["kind"]?.Type == JTokenType.String ? o.Value<string>("kind") : null)
                {
                    EnterZoom = ReadZoom(o["enter_zoom"]),
                    ManualHeights = ReadCount(o["manual_heights"]),
                    ManualUnmatched = ReadCount(o["manual_unmatched"]),
                    Rect = Map3DRotRect.Parse(o["rect"]),
                    UserBuildings = ReadCount(o["user_buildings"]),
                });
        }
        return list;
    }

    private static int ReadCount(JToken? t) => t?.Type == JTokenType.Integer && t.Value<long>() is long n && n > 0 && n < int.MaxValue ? (int)n : 0;

    // ── 건물 높이 직접 입력(map-3d heights.py) ──

    /// <summary>건물 높이 직접 입력(PUT /api/jobs/{id}/buildings/{bid}/height {"height", "floors"}) — 서버가 고친 건물 줄.</summary>
    public async Task<Map3DBuildingSaved> SetBuildingHeightAsync(string job, int buildingId, double height, int? floors, CancellationToken ct = default)
    {
        var body = new JObject { ["height"] = height, ["floors"] = floors is int f ? f : JValue.CreateNull() }.ToString(Formatting.None);
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Put, BuildingHeightUrl(job, buildingId))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }, allowNotFound: false, ct).ConfigureAwait(false);
        return ParseBuildingSaved(json!);
    }

    /// <summary>자동으로 되돌리기(DELETE /api/jobs/{id}/buildings/{bid}/height) — 굽기 때 자동 추정 높이로 돌아간 건물 줄.</summary>
    public async Task<Map3DBuildingSaved> ResetBuildingHeightAsync(string job, int buildingId, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Delete, BuildingHeightUrl(job, buildingId)), allowNotFound: false, ct).ConfigureAwait(false);
        return ParseBuildingSaved(json!);
    }

    /// <summary>「모두 자동으로」(DELETE /api/jobs/{id}/height-overrides) — 자동으로 되돌린 건물 줄들(형식이 틀린 줄은 뺀다).</summary>
    public async Task<IReadOnlyList<Map3DBuildingSaved>> ResetAllBuildingHeightsAsync(string job, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{Map3DMath.JobUrl(_server, job)}/height-overrides"),
                                   allowNotFound: false, ct).ConfigureAwait(false);
        var list = new List<Map3DBuildingSaved>();
        if (JObject.Parse(json!)["buildings"] is JArray rows)
            foreach (var row in rows.OfType<JObject>())
                if (Map3DBuilding.Parse(row) is { } pick) list.Add(new Map3DBuildingSaved(row, pick));
        return list;
    }

    // ── 직접 그린 건물(map-3d userbuildings.py) ──

    /// <summary>그 장면 범위의 직접 그린 건물(GET /api/jobs/{id}/user-buildings) — 옛 서버(404)면 빈 목록.</summary>
    public async Task<IReadOnlyList<Map3DUserBuildingItem>> ListUserBuildingsAsync(string job, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Get, UserBuildingsUrl(job)), allowNotFound: true, ct).ConfigureAwait(false);
        return json == null ? Array.Empty<Map3DUserBuildingItem>() : Map3DUserBuilding.ParseList(json);
    }

    /// <summary>넣기 · 고치기(PUT /api/jobs/{id}/user-buildings/{uid}) — 서버가 저장한 항목.</summary>
    public Task<Map3DUserBuildingItem> PutUserBuildingAsync(string job, string uid, IReadOnlyList<UbPoint> outline, double height, int? floors,
                                                            string? name, CancellationToken ct = default)
        => PutUserBuildingAsync(job, uid, outline, height, floors, name, Map3DUserBuilding.RoofFlat, Map3DUserBuilding.UseAuto, ct);

    /// <summary>넣기 · 고치기 — 지붕 모양(flat · gable · cone · dome) · 외벽 성격(auto · commercial · industrial · residential)까지.</summary>
    public async Task<Map3DUserBuildingItem> PutUserBuildingAsync(string job, string uid, IReadOnlyList<UbPoint> outline, double height, int? floors,
                                                                  string? name, string roof, string use, CancellationToken ct = default)
    {
        var body = Map3DUserBuilding.PutBody(outline, height, floors, name, roof, use).ToString(Formatting.None);
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Put, $"{UserBuildingsUrl(job)}/{Uri.EscapeDataString(uid)}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }, allowNotFound: false, ct).ConfigureAwait(false);
        return Map3DUserBuilding.ParseItem(JObject.Parse(json!)["item"] as JObject)
               ?? throw new Map3DRequestException(HttpStatusCode.OK, "건물 응답 형식이 올바르지 않습니다");
    }

    /// <summary>하나 지우기(DELETE …/user-buildings/{uid}) — 지웠으면 true, 이미 없으면(404) false.</summary>
    public async Task<bool> DeleteUserBuildingAsync(string job, string uid, CancellationToken ct = default)
        => await SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{UserBuildingsUrl(job)}/{Uri.EscapeDataString(uid)}"), allowNotFound: true, ct)
               .ConfigureAwait(false) != null;

    /// <summary>「모두 지우기」(DELETE …/user-buildings) — 지운 수.</summary>
    public async Task<int> DeleteAllUserBuildingsAsync(string job, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Delete, UserBuildingsUrl(job)), allowNotFound: false, ct).ConfigureAwait(false);
        return JObject.Parse(json!)["deleted"]?.Type == JTokenType.Integer ? JObject.Parse(json!).Value<int>("deleted") : 0;
    }

    private string UserBuildingsUrl(string job) => $"{Map3DMath.JobUrl(_server, job)}/user-buildings";

    private string BuildingHeightUrl(string job, int buildingId)
        => $"{Map3DMath.JobUrl(_server, job)}/buildings/{buildingId.ToString(CultureInfo.InvariantCulture)}/height";

    internal static Map3DBuildingSaved ParseBuildingSaved(string json)
    {
        var building = JObject.Parse(json)["building"] as JObject;
        if (Map3DBuilding.Parse(building) is not { } pick)
            throw new Map3DRequestException(HttpStatusCode.OK, "건물 응답 형식이 올바르지 않습니다");
        return new Map3DBuildingSaved(building!, pick);
    }

    /// <summary>enter_zoom — 유한한 수만(그 밖은 null = 세팅값).</summary>
    private static double? ReadZoom(JToken? t)
        => t?.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(t.Value<double>()) ? t.Value<double>() : null;

    private static DateTime? ParseCreated(JToken? t)
    {
        if (t == null) return null;
        if (t.Type is JTokenType.Integer or JTokenType.Float)
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(t.Value<double>() * 1000d)).LocalDateTime;
        if (t.Type == JTokenType.String && DateTimeOffset.TryParse(t.Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
            return d.LocalDateTime;
        return null;
    }

    /// <summary>보내고 본문을 돌려준다. 404 + allowNotFound → null. 그 밖의 실패 → Map3DRequestException(detail).
    /// 연결 실패 · 시간 초과 → Map3DServerUnavailableException. 호출자 취소는 그대로 OperationCanceledException.</summary>
    private async Task<string?> SendAsync(HttpRequestMessage request, bool allowNotFound, CancellationToken ct, TimeSpan? timeout = null)
    {
        // 시간 제한 = 요청마다(보통 RequestTimeout, 굽기 시작은 BakeStartTimeout) — 보내기 · 받기 · 본문 읽기까지
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout ?? RequestTimeout);
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, limit.Token).ConfigureAwait(false); }
        catch (HttpRequestException ex) { throw new Map3DServerUnavailableException(ex.Message, ex); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw new Map3DServerUnavailableException("응답 시간 초과", ex, isTimeout: true); }

        using (response)
        {
            string text;
            try { text = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false); }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw new Map3DServerUnavailableException("응답 시간 초과", ex, isTimeout: true); }
            if (response.IsSuccessStatusCode) return text;
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return null;
            throw new Map3DRequestException(response.StatusCode, ReadDetail(text) ?? $"HTTP {(int)response.StatusCode}");
        }
    }

    /// <summary>FastAPI 오류 본문 {"detail": ...} 의 detail(문자열이 아니면 JSON 그대로).</summary>
    internal static string? ReadDetail(string text)
    {
        try
        {
            var detail = JObject.Parse(text)["detail"];
            if (detail == null) return null;
            return detail.Type == JTokenType.String ? detail.ToString() : detail.ToString(Formatting.None);
        }
        catch (JsonException) { return string.IsNullOrWhiteSpace(text) ? null : text.Trim(); }
    }

    internal static Map3DJob ParseJob(string json)
    {
        var o = JObject.Parse(json);
        string? id = o["id"]?.ToString();
        string? status = o["status"]?.ToString();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(status))
            throw new Map3DRequestException(HttpStatusCode.OK, "작업 응답 형식이 올바르지 않습니다");
        double progress = o["progress"]?.Type is JTokenType.Float or JTokenType.Integer ? Math.Clamp(o["progress"]!.Value<double>(), 0d, 1d) : 0d;
        return new Map3DJob(id, status, progress, o["message"]?.Type == JTokenType.String ? o["message"]!.ToString() : null);
    }

    public void Dispose() => _http.Dispose();
}

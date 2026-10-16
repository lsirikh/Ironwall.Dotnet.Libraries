using System;
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

/// <summary>굽기 작업 상태. Status = "queued" | "running" | "done" | "error", Progress 0~1.</summary>
public sealed record Map3DJob(string Id, string Status, double Progress, string? Message)
{
    public bool IsDone => Status == "done";
    public bool IsError => Status == "error";
}

/// <summary>구운 장면. Id 는 3D 화면 주소의 job 값으로 쓴다. Bbox = [minLon, minLat, maxLon, maxLat].</summary>
public sealed record Map3DScene(string Id, double[] Bbox);

/// <summary>서버에 닿지 않음(연결 거부 · 시간 초과).</summary>
public sealed class Map3DServerUnavailableException : Exception
{
    public Map3DServerUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
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
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>굽기 시작(POST /api/jobs) → 202 작업.</summary>
    public async Task<Map3DJob> StartBakeAsync(double[] bbox, CancellationToken ct = default)
    {
        var body = new JObject { ["bbox"] = new JArray(bbox.Cast<object>().ToArray()) }.ToString(Formatting.None);
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Post, Map3DMath.JobsUrl(_server))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }, allowNotFound: false, ct).ConfigureAwait(false);
        return ParseJob(json!);
    }

    /// <summary>작업 진행 조회(GET /api/jobs/{id}).</summary>
    public async Task<Map3DJob> GetJobAsync(string id, CancellationToken ct = default)
    {
        var json = await SendAsync(new HttpRequestMessage(HttpMethod.Get, Map3DMath.JobUrl(_server, id)), allowNotFound: false, ct).ConfigureAwait(false);
        return ParseJob(json!);
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
        return new Map3DScene(id, bbox);
    }

    /// <summary>보내고 본문을 돌려준다. 404 + allowNotFound → null. 그 밖의 실패 → Map3DRequestException(detail).
    /// 연결 실패 · 시간 초과 → Map3DServerUnavailableException. 호출자 취소는 그대로 OperationCanceledException.</summary>
    private async Task<string?> SendAsync(HttpRequestMessage request, bool allowNotFound, CancellationToken ct)
    {
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, ct).ConfigureAwait(false); }
        catch (HttpRequestException ex) { throw new Map3DServerUnavailableException(ex.Message, ex); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { throw new Map3DServerUnavailableException("응답 시간 초과", ex); }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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

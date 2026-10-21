using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>판본 원문까지 고정하는 프로브 대역 — 8.0.6 처럼 세대(V8_0) 안의 판본 분기를 시험한다.</summary>
internal sealed class VersionedContractProbe : IServerContractProbe
{
    public VersionedContractProbe(EnumServerContract contract, string? rawVersion)
    {
        Contract = contract;
        RawVersion = rawVersion;
    }
    public EnumServerContract Contract { get; }
    public string? RawVersion { get; }
    public bool IsResolved => true;
    public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
}

/// <summary>요청 한 건(메서드 · 경로 · 질의 · 본문)의 기록.</summary>
internal sealed record ScriptedCall(string Method, string Endpoint, Dictionary<string, string>? Query, JObject? Body);

/// <summary>
/// 요청마다 응답을 만들어 주는 <see cref="IApiService"/> 대역 — 페이지 순회처럼 <b>여러 번</b> 묻는 경로를 시험한다.
/// 본문은 실제 <c>ApiService</c> 와 같은 직렬화기(Newtonsoft 기본)를 거쳐 기록한다.
/// </summary>
internal sealed class ScriptedApiService : IApiService
{
    private readonly Func<ScriptedCall, (HttpStatusCode Status, string Json)> _responder;

    public ScriptedApiService(Func<ScriptedCall, (HttpStatusCode Status, string Json)> responder) => _responder = responder;

    public List<ScriptedCall> Calls { get; } = new();

    private Task<HttpResponseMessage> Handle(string method, string endpoint, object? body, Dictionary<string, string>? query = null)
    {
        var call = new ScriptedCall(method, endpoint,
            query == null ? null : new Dictionary<string, string>(query),
            body == null ? null : JObject.Parse(JsonConvert.SerializeObject(body)));
        Calls.Add(call);
        var (status, json) = _responder(call);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => Handle("DELETE", endpoint, null);
    public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Handle("DELETE", endpoint, body);
    public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Handle("GET", endpoint, null, parameters);
    public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Handle("PATCH", endpoint, body);
    public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Handle("POST", endpoint, null);
    public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Handle("POST", endpoint, body);
    public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Handle("PUT", endpoint, body);

    public void Initialize() { }
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public string Url => string.Empty;
    public string ApiKey => string.Empty;
    public string UserId => string.Empty;
    public string Phone => string.Empty;
}

internal static class Spec8015Kit
{
    public const string BaseUrl = "https://gop.test/api";

    public static DeviceApiService Service(IApiService http, EnumServerContract contract, string? rawVersion = null)
        => new(null, http, new ApiSetupModel { Url = BaseUrl }, new VersionedContractProbe(contract, rawVersion ?? contract.ToString()));

    public static (DeviceApiService svc, CapturingApiService http) Capturing(EnumServerContract contract, string? rawVersion = null, string? responseJson = null, HttpStatusCode status = HttpStatusCode.OK)
    {
        var http = new CapturingApiService { ResponseStatus = status };
        if (responseJson != null) http.ResponseJson = responseJson;
        return (Service(http, contract, rawVersion), http);
    }
}

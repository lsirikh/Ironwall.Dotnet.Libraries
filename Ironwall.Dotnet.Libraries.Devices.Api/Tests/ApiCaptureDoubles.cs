using Ironwall.Dotnet.Libraries.Api.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>계약 세대를 고정하는 프로브 대역 — 서버에 붙지 않는 계약 테스트용.</summary>
internal sealed class FixedContractProbe : IServerContractProbe
{
    public FixedContractProbe(EnumServerContract contract) => Contract = contract;
    public EnumServerContract Contract { get; }
    public string? RawVersion => Contract.ToString();
    public bool IsResolved => true;
    public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
}

/// <summary>
/// HTTP 호출을 가로채 마지막 요청의 메서드·경로·본문을 기록하는 <see cref="IApiService"/> 대역.
/// 본문은 실제 <c>ApiService</c> 와 같은 직렬화기(Newtonsoft 기본)를 거친다 — <c>ShouldSerialize*</c> 게이트가 그대로 반영된다.
/// </summary>
internal sealed class CapturingApiService : IApiService
{
    public string? Method { get; private set; }
    public string? Endpoint { get; private set; }
    public JObject? Body { get; private set; }
    public bool Throw { get; set; }

    private Task<HttpResponseMessage> Capture(string method, string endpoint, object? body)
    {
        if (Throw) throw new HttpRequestException("simulated transport failure");

        Method = method;
        Endpoint = endpoint;
        Body = body == null ? null : JObject.Parse(JsonConvert.SerializeObject(body));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"data\":null,\"meta\":{}}", Encoding.UTF8, "application/json"),
        });
    }

    public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => Capture("DELETE", endpoint, null);
    public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Capture("DELETE", endpoint, body);
    public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Capture("GET", endpoint, null);
    public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Capture("PATCH", endpoint, body);
    public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Capture("POST", endpoint, null);
    public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Capture("POST", endpoint, body);
    public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Capture("PUT", endpoint, body);

    public void Initialize() { }
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public string Url => string.Empty;
    public string ApiKey => string.Empty;
    public string UserId => string.Empty;
    public string Phone => string.Empty;
}

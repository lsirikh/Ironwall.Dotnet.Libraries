using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.Tests;
/****************************************************************************
   Purpose      : 매핑 게이트웨이 HTTP 대역 — 요청(경로 · 쿼리)을 기록하고 각본 응답을 준다(네트워크 없음)
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>계약 세대를 고정하는 프로브 대역.</summary>
internal sealed class MappingFixedContractProbe : IServerContractProbe
{
    public MappingFixedContractProbe(EnumServerContract contract) => Contract = contract;
    public EnumServerContract Contract { get; }
    public string? RawVersion => Contract.ToString();
    public bool IsResolved => true;
    public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
}

/// <summary>GET 요청마다 (경로, 쿼리) 를 받아 (상태, 본문) 을 돌려주는 각본 대역.</summary>
internal sealed class MappingScriptedApi : IApiService
{
    private readonly Func<string, IReadOnlyDictionary<string, string>, (HttpStatusCode Status, string Json)> _script;

    public MappingScriptedApi(Func<string, IReadOnlyDictionary<string, string>, (HttpStatusCode Status, string Json)> script)
        => _script = script;

    public List<(string Method, string Endpoint, Dictionary<string, string> Query)> Calls { get; } = new();

    private Task<HttpResponseMessage> Reply(string method, string endpoint, Dictionary<string, string>? query)
    {
        var q = query is null ? new Dictionary<string, string>() : new Dictionary<string, string>(query);
        Calls.Add((method, endpoint, q));
        var (status, json) = _script(endpoint, q);
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }

    public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null) => Reply("GET", endpoint, parameters);
    public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint) => Reply("DELETE", endpoint, null);
    public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Reply("DELETE", endpoint, null);
    public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Reply("PATCH", endpoint, null);
    public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content) => Reply("POST", endpoint, null);
    public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Reply("POST", endpoint, null);
    public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Reply("PUT", endpoint, null);

    public void Initialize() { }
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public string Url => string.Empty;
    public string ApiKey => string.Empty;
    public string UserId => string.Empty;
    public string Phone => string.Empty;

    /// <summary>시험용 게이트웨이(8.0 계약, 주소 http://t/api).</summary>
    public MappingWorkbenchGateway Gateway()
        => new(this, new ApiSetupModel { Url = "http://t/api" }, new MappingFixedContractProbe(EnumServerContract.V8_0));
}

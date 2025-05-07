using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Ironwall.Dotnet.Libraries.Api.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/5/2025 12:26:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ApiService : IApiService
{
    #region - Ctors -
    public ApiService(ILogService log
                    , ApiSetupModel setupModel)
    {
        _log = log;
        _setupModel = setupModel;
    }
    #endregion
    #region - Implementation of Interface -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        Initialize();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize()
    {
        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(_setupModel.Username) && !string.IsNullOrEmpty(_setupModel.Password))
            handler.Credentials = new NetworkCredential(_setupModel.Username, _setupModel.Password);

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_setupModel.Url),
            Timeout = TimeSpan.FromSeconds(TIMEOUT)
        };
    }

    /// <summary>
    /// GET 요청 처리
    /// </summary>
    public async Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            var url = endpoint;

            // QueryString 추가
            if (parameters != null)
            {
                var queryString = new FormUrlEncodedContent(parameters).ReadAsStringAsync().Result;
                url += "?" + queryString;
            }

            return await _client.GetAsync(url);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] GET 요청 실패: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = ex.Message };
        }
    }

    /// <summary>
    /// POST 요청 처리 (JSON 데이터)
    /// </summary>
    public async Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _client.PostAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] POST 요청 실패: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = ex.Message };
        }
    }

    /// <summary>
    /// POST 요청 처리 (FormData)
    /// </summary>
    public async Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            return await _client.PostAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] FormData POST 요청 실패: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = ex.Message };
        }
    }

    /// Delete 요청 처리
    /// </summary>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> DeleteRequestAsync(string endpoint)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            return await _client.DeleteAsync(endpoint);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] DELETE 요청 실패: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = ex.Message };
        }
    }

    /// <summary>
    /// Patch 요청 처리
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="endpoint"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = content
            };
            return await _client.SendAsync(request);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] PATCH 요청 실패: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = ex.Message };
        }
    }

    /// <summary>
    /// Put 요청
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="endpoint"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] PUT 요청 실패: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = ex.Message };
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public string Url => _setupModel.Url;
    public string ApiKey => _setupModel.ApiKey;
    public string UserId => _setupModel.Username;
    public string Phone => _setupModel.Phone;
    #endregion
    #region - Attributes -
    private readonly ILogService _log;
    private readonly ApiSetupModel _setupModel;
    private HttpClient? _client;
    private const int TIMEOUT = 10;
    #endregion
}

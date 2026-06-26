using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

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
    public ApiService(ILogService? log
                    , ApiSetupModel setupModel
                    , DelegatingHandler? authHandler = null)
    {
        _log = log;
        _setupModel = setupModel;
        _authHandler = authHandler;   // FR-5: Bearer 등 메시지 핸들러 파이프라인(Account 전용 주입, Device/Event=null)
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
        var httpHandler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(_setupModel.Username) && !string.IsNullOrEmpty(_setupModel.Password))
            httpHandler.Credentials = new NetworkCredential(_setupModel.Username, _setupModel.Password);

        // FR-5: authHandler(BearerAuthHandler 등) 주입 시 파이프라인 최상단에 끼운다. 없으면 평범한 HttpClientHandler.
        HttpMessageHandler pipeline = httpHandler;
        if (_authHandler != null)
        {
            _authHandler.InnerHandler = httpHandler;
            pipeline = _authHandler;
        }

        // FR-4: setupModel.Timeout 존중(0 이하면 기본 TIMEOUT 폴백). 기존엔 하드코딩 const 만 써서 설정이 무시되던 버그.
        var timeoutSec = _setupModel.Timeout > 0 ? _setupModel.Timeout : TIMEOUT;

        // BaseAddress 끝 슬래시 정규화: base가 "…/api"(슬래시 없음)이면 상대 endpoint("auth/login")가
        //   마지막 세그먼트 "/api"를 떨궈 "/auth/login"(404)로 가는 HttpClient 결합 함정. "/"로 강제해
        //   "auth/login" → "…/api/auth/login" 정상 결합. (절대/leading-slash endpoint엔 영향 없음)
        var baseUrl = string.IsNullOrEmpty(_setupModel.Url) || _setupModel.Url.EndsWith("/")
            ? _setupModel.Url
            : _setupModel.Url + "/";
        _client = new HttpClient(pipeline)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(timeoutSec)
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
                var queryString = await new FormUrlEncodedContent(parameters).ReadAsStringAsync().ConfigureAwait(false);
                url += "?" + queryString;
            }

            return await _client.GetAsync(url);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] GET 요청 실패: {ex.Message}");
            return BuildExceptionResponse(ex);
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

            var json = JsonConvert.SerializeObject(body);
            //var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _client.PostAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] POST 요청 실패: {ex.Message}");
            return BuildExceptionResponse(ex);
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
            return BuildExceptionResponse(ex);
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
            return BuildExceptionResponse(ex);
        }
    }

    /// <summary>
    /// Delete 요청 처리 (body 포함 — 서버 벌크해제용, v4.3+)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="endpoint"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
            {
                Content = content
            };
            return await _client.SendAsync(request);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] DELETE(body) 요청 실패: {ex.Message}");
            return BuildExceptionResponse(ex);
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

            var json = JsonConvert.SerializeObject(body);
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
            return BuildExceptionResponse(ex);
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

            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] PUT 요청 실패: {ex.Message}");
            return BuildExceptionResponse(ex);
        }
    }

    /// <summary>예외 → 상태코드 매핑 (FR-6): 타임아웃 504 / 연결실패 503 / 그 외 500. 기존 BadRequest(400) 일괄변환 폐지(401/503/504 구분 가능).</summary>
    internal static HttpResponseMessage BuildExceptionResponse(Exception ex) => ex switch
    {
        TaskCanceledException   => new HttpResponseMessage(HttpStatusCode.GatewayTimeout)     { ReasonPhrase = "Request timed out" },
        HttpRequestException h   => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { ReasonPhrase = h.Message },
        _                        => new HttpResponseMessage(HttpStatusCode.InternalServerError){ ReasonPhrase = ex.Message },
    };
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
    private readonly ILogService? _log;
    private readonly ApiSetupModel _setupModel;
    private HttpClient? _client;
    private readonly DelegatingHandler? _authHandler;
    private const int TIMEOUT = 10;
    #endregion
}

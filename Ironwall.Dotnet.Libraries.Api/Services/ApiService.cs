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
public class ApiService : IApiService, IApiHeaderRequestService
{
    // 요청 body 직렬화 공통 설정 — DateTime 필드를 aware ISO8601로 내보낸다(Unspecified/Local → 로컬 KST offset 부착).
    //
    // 명세 §3.4 대조 (2026-09-18):
    //  · D19(오프셋 + 마이크로초 6자리 고정)는 **응답** 규칙이다. 실측 확인: meta.timestamp =
    //    "2026-09-18T09:42:53.104788+09:00". 우리는 MetaDto.Timestamp 를 string 으로 받고
    //    DateParseHandling.None 이라 소수부 자릿수에 영향받지 않는다.
    //  · **입력**은 "offset 포함 aware 권장"이고, 관측 시각 계열(observed_at·installed_at·replaced_at)만
    //    **오프셋 필수**(없으면 422). DateTimeZoneHandling.Local 이 DateTime 전부에 오프셋을 붙이므로 이 요구를 만족한다.
    //    설정 없는 SerializeObject 는 Unspecified 를 offset 없이 naive 로 내보내 422 를 유발한다 — 그래서 이 설정이 필요하다.
    //  · 소수부 자릿수·"Z" vs "+00:00" 는 **입력에서 자유**다(실측: 7자리·6자리·1자리·naive·date-only 전부 200).
    //    Newtonsoft 는 DateTimeOffset 의 0 오프셋을 "Z" 가 아니라 "+00:00" 으로 쓴다 — 명세가 양쪽을 받으므로 정합이다.
    //    ("...Z" 를 기대하는 기존 단위테스트 4건은 **테스트 기대치 드리프트**이고 계약 위반이 아니다.)
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Local,
    };

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

        // FR-2: 세션 식별용 X-Client-Id(주체별 고유). 로그인 포함 전 요청에 일관 부착.
        // 빈값이면 미부착(하위호환 안전), 패턴(^[A-Za-z0-9._:-]{1,64}$) 위반이면 미부착+경고(서버는 위반값을 무시).
        var clientId = _setupModel.ClientId;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(clientId, "^[A-Za-z0-9._:-]{1,64}$"))
                _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Id", clientId);
            else
                _log?.Warning($"[ApiService] X-Client-Id 패턴 위반으로 미부착: '{clientId}'");
        }
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

            var url = await BuildUrlAsync(endpoint, parameters).ConfigureAwait(false);

            return await _client.GetAsync(url);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] GET 요청 실패: {ex.Message}");
            return BuildExceptionResponse(ex);
        }
    }

    /// <summary>
    /// 엔드포인트 + 쿼리 파라미터 → 최종 URL. <b>쿼리 조립의 단일 지점</b>이다.
    /// </summary>
    /// <remarks>
    /// <para><b>① 빈 값은 키째 뺀다</b>(명세 §12.1.1 <c>EMPTY_STRING</c> — "값이 없으면 키를 빼십시오").
    /// <see cref="FormUrlEncodedContent"/> 는 빈 값을 드롭하지 <b>않아서</b> 딕셔너리에 <c>""</c> 가 한 번 들어가면
    /// <c>?status=</c> 가 그대로 나가고 서버는 <b>422</b> 를 낸다(실측: <c>?status=</c> ·
    /// <c>?status=+</c>(공백) 모두 <c>VALUE_NOT_ALLOWED</c>/<c>CONSTRAINT</c>).
    /// 호출부 가드가 지금까지 유일한 방어선이었고 전 지점이 <c>IsNullOrEmpty</c> 라 <b>공백 한 칸을 통과</b>시켰다 —
    /// 여기서 <see cref="string.IsNullOrWhiteSpace"/> 기준으로 일괄 차단한다. 드롭은 경고 로그로 남긴다(조용히 사라지지 않게).</para>
    /// <para><b>② 결합자를 검사한다</b> — 엔드포인트에 이미 <c>?</c> 가 있으면 <c>&amp;</c> 로 잇는다.
    /// 종전에는 무조건 <c>"?"</c> 를 붙여 <c>…?a=1?b=2</c> 가 될 수 있었다(현재 호출부 0건 · 신규 1건으로 실현).</para>
    /// <para>값이 전부 비어 드롭되면 쿼리 자체를 붙이지 않는다(<c>…?</c> 꼬리 방지).</para>
    /// </remarks>
    private async Task<string> BuildUrlAsync(string endpoint, Dictionary<string, string>? parameters)
    {
        if (parameters == null || parameters.Count == 0) return endpoint;

        var effective = new Dictionary<string, string>(parameters.Count);
        foreach (var kv in parameters)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;

            if (string.IsNullOrWhiteSpace(kv.Value))
            {
                // 서버는 빈 문자열을 '값'으로 보지 않는다 — 실어 보내면 422 다. 키를 뺀다.
                _log?.Warning($"[ApiService] 빈 쿼리 값이라 키를 제외했습니다(422 방지): '{kv.Key}' → {endpoint}");
                continue;
            }

            effective[kv.Key] = kv.Value;
        }

        if (effective.Count == 0) return endpoint;

        var queryString = await new FormUrlEncodedContent(effective).ReadAsStringAsync().ConfigureAwait(false);
        var separator = endpoint.Contains('?') ? "&" : "?";
        return endpoint + separator + queryString;
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

            var json = JsonConvert.SerializeObject(body, _jsonSettings);
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

            var json = JsonConvert.SerializeObject(body, _jsonSettings);
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

            var json = JsonConvert.SerializeObject(body, _jsonSettings);
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

            var json = JsonConvert.SerializeObject(body, _jsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] PUT 요청 실패: {ex.Message}");
            return BuildExceptionResponse(ex);
        }
    }

    /// <summary>
    /// 헤더를 이 요청에만 실어 JSON 을 보낸다(<see cref="IApiHeaderRequestService"/>) — 조건부 쓰기(<c>If-Match</c>)용.
    /// </summary>
    /// <remarks>기본 헤더(<c>DefaultRequestHeaders</c>)는 건드리지 않는다 — 다른 호출에 헤더가 새지 않는다.</remarks>
    public async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string endpoint,
        object? body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken token = default)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException("HttpClient 인스턴스가 생성되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("엔드포인트 URL이 올바르지 않습니다.", nameof(endpoint));

            using var request = new HttpRequestMessage(method, endpoint);
            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body, _jsonSettings);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            if (headers != null)
            {
                foreach (var header in headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return await _client.SendAsync(request, token);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ApiService] {method} 요청 실패: {ex.Message}");
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

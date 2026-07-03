using System.Net;
using System.Net.Http.Headers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;

/// <summary>
/// HttpClient 파이프라인에 삽입되는 메시지 핸들러 (FR-5).
/// <para>① 매 요청 <see cref="ITokenStorageService.AccessToken"/>을 Authorization: Bearer 로 per-request 주입.</para>
/// <para>② 401 수신 시 <c>SemaphoreSlim(1,1)</c> single-flight 로 <see cref="IAccountApiService.RefreshAsync"/>를 1회만 수행 후 원요청 1회 재시도.</para>
/// <para>③ 403 은 refresh 미시도(무한루프 차단). refresh 최종 실패 시 <see cref="SessionExpired"/> 1회 발화(앱이 SessionExpiredEvent 로 변환).</para>
/// <para>Device/Event/Account named ApiService 가 동일 핸들러를 공유하면 토큰이 자동 동기화된다.
/// IAccountApiService 는 순환 의존 회피를 위해 <see cref="Func{TResult}"/> 지연 해석으로 받는다.</para>
/// </summary>
public class BearerAuthHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ITokenStorageService _store;
    private readonly Func<IAccountApiService> _accountApiFactory;
    private readonly ILogService? _log;

    /// <summary>refresh 최종 실패(세션 만료) 시 1회 발화. 앱(버킷 C)이 구독해 SessionExpiredEvent 발행/로그아웃 수행.</summary>
    public event Action? SessionExpired;

    public BearerAuthHandler(ITokenStorageService store, Func<IAccountApiService> accountApiFactory, ILogService? log = null)
    {
        _store = store;
        _accountApiFactory = accountApiFactory;
        _log = log;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isAuth = IsAuthEndpoint(request.RequestUri);

        // 로그인/갱신은 Bearer 미부착(로그인=신규 자격, 갱신=바디 refresh_token). 로그아웃·그 외는 access 토큰 부착.
        var staleToken = _store.AccessToken;
        if (!IsLoginOrRefresh(request.RequestUri))
            ApplyBearer(request, staleToken);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;   // 200/403 등 — 403 은 권한 문제라 refresh 미시도

        // ★ auth 엔드포인트(로그인/갱신/로그아웃)의 401은 refresh·세션만료로 처리하지 않는다:
        //    오답 로그인 401을 '세션 만료'로 오인해 가짜 ForceLogout 하거나, /auth/refresh 재진입으로 _refreshLock 이 스톨하던 문제 차단.
        if (isAuth)
            return response;

        var refreshed = await TryRefreshSingleFlightAsync(staleToken, cancellationToken).ConfigureAwait(false);
        if (!refreshed)
        {
            _log?.Warning("[BearerAuthHandler] refresh 실패 — 세션 만료 신호 발화");
            SessionExpired?.Invoke();
            return response;
        }

        // 새 토큰으로 1회 재시도 (HttpRequestMessage 는 1회성이라 clone 필요)
        response.Dispose();
        var retry = await CloneAsync(request).ConfigureAwait(false);
        ApplyBearer(retry, _store.AccessToken);
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>auth 액션 엔드포인트(로그인/갱신/로그아웃) 여부 — 401 refresh·세션만료 로직 제외 대상.</summary>
    private static bool IsAuthEndpoint(Uri? uri)
        => MatchesPath(uri, "/auth/login") || MatchesPath(uri, "/auth/refresh") || MatchesPath(uri, "/auth/logout");

    /// <summary>Bearer 미부착 대상 — 로그인(신규 자격)·갱신(바디 refresh_token 사용).</summary>
    private static bool IsLoginOrRefresh(Uri? uri)
        => MatchesPath(uri, "/auth/login") || MatchesPath(uri, "/auth/refresh");

    private static bool MatchesPath(Uri? uri, string suffix)
        => uri is not null && uri.AbsolutePath.TrimEnd('/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static void ApplyBearer(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<bool> TryRefreshSingleFlightAsync(string? staleToken, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 다른 요청이 이미 토큰을 갱신했으면 재요청만으로 충분 → 성공 처리
            if (_store.AccessToken != staleToken && !string.IsNullOrEmpty(_store.AccessToken))
                return true;

            var refreshToken = _store.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                _store.Clear();
                return false;
            }

            var gen = _store.Generation;   // FR-FL-05: refresh 시작 시점 세대 캡처
            var result = await _accountApiFactory().RefreshAsync(refreshToken, ct).ConfigureAwait(false);
            if (result.Success && !string.IsNullOrEmpty(result.Data?.AccessToken))
            {
                // 강제 로그아웃(Clear)이 refresh 진행 중 끼어들었으면(세대 변경) 폐기 세션 부활 차단 → 실패 처리
                if (_store.SetTokensIfGeneration(gen, result.Data.AccessToken, result.Data.RefreshToken))
                    return true;
                _log?.Warning("[BearerAuthHandler] refresh 성공했으나 세션 폐기됨(generation 변경) — 부활 차단");
                return false;
            }

            _store.Clear();
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        if (request.Content != null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        foreach (var h in request.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        return clone;
    }
}

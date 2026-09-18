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

    /// <summary>refresh 결과 — Renewed(갱신 성공) / Terminal(종단 실패=세션 만료) / Transient(일시 실패=재시도 위임, 세션 유지).</summary>
    private enum RefreshOutcome { Renewed, Terminal, Transient }
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

        // 폐기 세션(SESSION_REVOKED: 중복로그인 축출/강제/비번변경 등)이면 refresh 왕복이 무의미 — 즉시 세션 만료 처리(session-revoked-08).
        if (await IsSessionRevokedAsync(response).ConfigureAwait(false))
        {
            _log?.Warning($"[BearerAuthHandler] SESSION_REVOKED 감지 — refresh 생략·세션 만료 발화 (401 trigger={request.RequestUri?.AbsolutePath})");
            SessionExpired?.Invoke();
            return response;
        }

        var outcome = await TryRefreshSingleFlightAsync(staleToken, cancellationToken).ConfigureAwait(false);
        if (outcome == RefreshOutcome.Transient)
        {
            // 일시 오류(네트워크/5xx/429) — 세션 강제종료 대신 원 401 반환(토큰 보존, 다음 요청·사용자 재시도에 위임). token-refresh-10
            _log?.Warning($"[BearerAuthHandler] refresh 일시 실패 — 세션 유지·재시도 위임 (401 trigger={request.RequestUri?.AbsolutePath})");
            return response;
        }
        if (outcome == RefreshOutcome.Terminal)
        {
            // 진단: 어느 요청의 401이 트리거였는지(로그인 직후 특정 엔드포인트 401 원인 추적용).
            _log?.Warning($"[BearerAuthHandler] refresh 종단 실패 — 세션 만료 신호 발화 (401 trigger={request.RequestUri?.AbsolutePath})");
            SessionExpired?.Invoke();
            return response;
        }

        // Renewed → 새 토큰으로 1회 재시도 (HttpRequestMessage 는 1회성이라 clone 필요)
        response.Dispose();
        var retry = await CloneAsync(request).ConfigureAwait(false);
        ApplyBearer(retry, _store.AccessToken);
        var retryResponse = await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        // 재시도도 401이면 종단 세션 만료로 escalate(token-refresh-15 — 재-refresh 재귀 금지, 좀비 세션 방지)
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            _log?.Warning("[BearerAuthHandler] refresh 후 재시도도 401 — 세션 만료 escalate");
            SessionExpired?.Invoke();
        }
        return retryResponse;
    }

    /// <summary>auth 액션 엔드포인트(로그인/갱신/로그아웃) 여부 — 401 refresh·세션만료 로직 제외 대상.</summary>
    private static bool IsAuthEndpoint(Uri? uri)
        => MatchesPath(uri, "/auth/login") || MatchesPath(uri, "/auth/refresh") || MatchesPath(uri, "/auth/logout");

    /// <summary>Bearer 미부착 대상 — 로그인(신규 자격)·갱신(바디 refresh_token 사용).</summary>
    private static bool IsLoginOrRefresh(Uri? uri)
        => MatchesPath(uri, "/auth/login") || MatchesPath(uri, "/auth/refresh");

    private static bool MatchesPath(Uri? uri, string suffix)
        => uri is not null && uri.AbsolutePath.TrimEnd('/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>401 본문의 <c>error.code == SESSION_REVOKED</c> 판별 — 폐기 세션이면 refresh 생략(불필요 왕복 제거).
    /// 본문은 기본 ResponseContentRead 로 버퍼되어 상위(ApiMessageHelper)가 재읽어도 안전. 파싱 실패/비JSON은 false.</summary>
    private static async Task<bool> IsSessionRevokedAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body)) return false;
            var code = Newtonsoft.Json.Linq.JObject.Parse(body)["error"]?["code"]?.ToString();
            return string.Equals(code, "SESSION_REVOKED", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static void ApplyBearer(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<RefreshOutcome> TryRefreshSingleFlightAsync(string? staleToken, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 다른 요청이 이미 토큰을 갱신했으면 재요청만으로 충분 → 성공 처리
            if (_store.AccessToken != staleToken && !string.IsNullOrEmpty(_store.AccessToken))
                return RefreshOutcome.Renewed;

            var refreshToken = _store.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                _store.Clear();
                return RefreshOutcome.Terminal;
            }

            var gen = _store.Generation;   // FR-FL-05: refresh 시작 시점 세대 캡처
            var result = await _accountApiFactory().RefreshAsync(refreshToken, ct).ConfigureAwait(false);
            if (result.Success && !string.IsNullOrEmpty(result.Data?.AccessToken))
            {
                // 강제 로그아웃(Clear)이 refresh 진행 중 끼어들었으면(세대 변경) 폐기 세션 부활 차단 → 종단 처리
                // session_id 는 refresh 로 회전하지 않는다(§9.2.4) — 서버가 응답에 실어 주면 그 값을 명시 승계한다(없으면 null → 기존 값 유지).
                if (_store.SetTokensIfGeneration(gen, result.Data.AccessToken, result.Data.RefreshToken, result.Data.SessionId))
                    return RefreshOutcome.Renewed;
                _log?.Warning("[BearerAuthHandler] refresh 성공했으나 세션 폐기됨(generation 변경) — 부활 차단");
                return RefreshOutcome.Terminal;
            }

            // 실패 분류(token-refresh-10): 일시(네트워크/5xx/429)면 토큰 보존·재시도 위임(Transient), 종단(401/자격 만료/SESSION_REVOKED)이면 Clear+세션 만료(Terminal).
            var code = result.Error?.Code;
            var sc = result.StatusCode;
            // 네트워크 예외는 code=INTERNAL_ERROR(AccountApiService catch), 서버 과부하는 429/5xx. StatusCode 미상(0)은 종단으로 간주(자격오류 등 안전측).
            var transient = code == "INTERNAL_ERROR" || sc == 429 || sc == 502 || sc == 503 || sc == 504;
            _log?.Warning($"[BearerAuthHandler] auth/refresh 거부: success={result.Success}, code={code}, status={sc}, transient={transient}, msg='{(string.IsNullOrEmpty(result.Message) ? result.Error?.Message : result.Message)}'");
            if (transient) return RefreshOutcome.Transient;   // Clear 안 함 — 토큰 보존
            _store.Clear();
            return RefreshOutcome.Terminal;
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

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

    /// <summary>
    /// <b>SSO 모드 재인증 훅</b> — SSO PRD FR-06. <c>null</c> 이면 레거시(refresh) 모드다.
    ///
    /// <para><b>왜 필요한가</b>: SSO 교환(<c>POST /api/auth/sso-exchange</c>)은 <c>refresh_token</c> 을 주지 않는다.
    /// 그대로 두면 401 한 번에 <c>refreshToken</c> 이 비어 즉시 <see cref="SessionExpired"/>(강제 로그아웃)가 된다.
    /// SSO 모드에서는 refresh 대신 이 훅이 <b>새 SSO 앱 토큰을 받아 재교환</b>하고 토큰 저장소를 갱신한다.</para>
    ///
    /// <para><b>왜 정적인가</b>: 핸들러는 도메인마다(계정·장비·이벤트·보고서·추적) 따로 만들어지지만
    /// 토큰 저장소는 하나이고 <see cref="_refreshLock"/> 도 이미 <b>프로세스 전역</b>이다. SSO 여부도 프로세스 전체의
    /// 모드라, 다섯 모듈을 고치지 않고 한 자리에서 켠다. 재교환은 <see cref="_refreshLock"/> 안에서 부르므로
    /// 다섯 도메인이 동시에 401 을 받아도 <b>한 번만</b> 일어난다.</para>
    ///
    /// <para>훅의 책임: 새 토큰을 <b>저장소에 넣고</b>(세대 검사 포함) 결과만 돌려준다. 핸들러는 저장소의 새 토큰으로 재시도한다.
    /// 시험은 설정 후 반드시 <c>null</c> 로 되돌린다.</para>
    /// </summary>
    public static Func<CancellationToken, Task<SsoReauthOutcome>>? SsoReauthenticator { get; set; }

    /// <summary>
    /// <b>만료 전 재교환</b> — SSO PRD FR-06 · 3자 계약 "만료 120초 전 재발급". <see cref="SessionLifecycle"/> 의 만료 타이머가 부른다.
    /// <para>401 재교환과 <b>같은 전역 락</b> 안에서 훅을 부른다 — 타이머와 401 이 동시에 와도 교환은 한 번이다
    /// (두 번 교환하면 뒤 교환이 앞 교환 세션을 끝내 진행 중 요청이 <c>SESSION_REVOKED</c> 를 받는다).</para>
    /// <para>락을 얻는 사이 다른 경로가 이미 토큰을 바꿨으면 훅을 부르지 않고 <see cref="SsoReauthOutcome.Renewed"/>.
    /// 훅이 없으면(SSO 모드 아님) <c>null</c>. 훅 예외는 <see cref="SsoReauthOutcome.Transient"/>.</para>
    /// </summary>
    public static async Task<SsoReauthOutcome?> RenewSsoAheadAsync(ITokenStorageService store, string? staleToken, CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var reauth = SsoReauthenticator;
            if (reauth is null) return null;
            if (!string.IsNullOrEmpty(store.AccessToken) && !string.Equals(store.AccessToken, staleToken, StringComparison.Ordinal))
                return SsoReauthOutcome.Renewed;
            try { return await reauth(ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException) { return SsoReauthOutcome.Transient; }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

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
            // ★ SSO 모드 경합(FR-06): 재교환은 같은 (계정, X-Client-Id) 의 **앞선 교환 세션을 끝낸다**
            //   (서버 sso.py — 옛 access 는 401 SESSION_REVOKED). 그래서 재교환 직전에 나간 요청은
            //   우리가 스스로 갈아 끼운 세션 때문에 SESSION_REVOKED 를 받는다. 이걸 진짜 폐기로 보면
            //   **재교환할 때마다 진행 중이던 요청이 강제 로그아웃을 일으킨다.**
            //   판별: 이 요청을 보낸 뒤 저장소 토큰이 바뀌었으면 우리 재교환이 대체한 것 → 새 토큰으로 1회 재시도.
            //   토큰이 그대로면 진짜 폐기(관리자 강제 로그아웃 등) → 기존대로 만료.
            if (SsoReauthenticator is not null && IsSupersededByOurRenewal(staleToken))
            {
                _log?.Info($"[BearerAuthHandler] SESSION_REVOKED 이나 토큰이 이미 재교환됨 — 대체된 옛 세션으로 보고 새 토큰으로 재시도 (trigger={request.RequestUri?.AbsolutePath})");
                return await RetryWithCurrentTokenAsync(request, response, cancellationToken).ConfigureAwait(false);
            }

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

        // Renewed → 새 토큰으로 1회 재시도
        return await RetryWithCurrentTokenAsync(request, response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 저장소의 <b>현재</b> 토큰으로 원요청을 1회 재시도한다(HttpRequestMessage 는 1회성이라 clone).
    /// 재시도도 401 이면 종단 세션 만료로 escalate — 재-refresh 재귀 금지(token-refresh-15, 좀비 세션 방지).
    /// </summary>
    private async Task<HttpResponseMessage> RetryWithCurrentTokenAsync(
        HttpRequestMessage request, HttpResponseMessage original, CancellationToken cancellationToken)
    {
        original.Dispose();
        var retry = await CloneAsync(request).ConfigureAwait(false);
        ApplyBearer(retry, _store.AccessToken);
        var retryResponse = await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            _log?.Warning("[BearerAuthHandler] refresh 후 재시도도 401 — 세션 만료 escalate");
            SessionExpired?.Invoke();
        }
        return retryResponse;
    }

    /// <summary>이 요청을 보낸 뒤 저장소 토큰이 다른 유효 값으로 바뀌었는가 — 우리 재교환이 옛 세션을 대체했다는 신호.</summary>
    private bool IsSupersededByOurRenewal(string? staleToken)
    {
        var current = _store.AccessToken;
        return !string.IsNullOrEmpty(current) && !string.Equals(current, staleToken, StringComparison.Ordinal);
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
                // ★ SSO 모드(FR-06): 교환은 refresh 를 주지 않으므로 여기가 정상 경로다.
                //   refresh 대신 재교환한다 — 훅이 새 SSO 앱 토큰(매번 새 jti)을 받아 교환하고 저장소를 갱신한다.
                //   이미 _refreshLock 안이라 다섯 도메인의 동시 401 이 재교환 한 번으로 모인다.
                var reauth = SsoReauthenticator;
                if (reauth is not null)
                {
                    SsoReauthOutcome o;
                    try { o = await reauth(ct).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // 훅의 예외는 세션을 죽이지 않는다 — 일시 실패로 보고 다음 요청·사용자 재시도에 맡긴다.
                        _log?.Warning($"[BearerAuthHandler] SSO 재교환 훅 예외 — 일시 실패로 처리: {ex.GetType().Name} {ex.Message}");
                        return RefreshOutcome.Transient;
                    }

                    switch (o)
                    {
                        case SsoReauthOutcome.Renewed:
                            return RefreshOutcome.Renewed;
                        case SsoReauthOutcome.Transient:
                            _log?.Warning("[BearerAuthHandler] SSO 재교환 일시 실패 — 세션 유지·재시도 위임");
                            return RefreshOutcome.Transient;
                        default:
                            _log?.Warning("[BearerAuthHandler] SSO 재교환 종단 실패(retryable=false) — 세션 만료");
                            _store.Clear();
                            return RefreshOutcome.Terminal;
                    }
                }

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

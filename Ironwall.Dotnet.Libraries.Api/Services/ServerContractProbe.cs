using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Api.Services;
/****************************************************************************
   Purpose      : 서버 계약 세대(6.3 / 7.0 / 8.0+) 감지 프로브 — FR-08
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="IServerContractProbe"/> 구현 — <c>GET {root}/openapi.json</c> 의 <c>info.version</c> 을
/// 1회 확보해 <see cref="EnumServerContract"/> 로 정규화하고 캐시한다.
/// </summary>
/// <remarks>
/// <para><b>버전 원천</b> — <c>openapi.json</c> 은 <b>무인증 200</b> 이다(실측 2026-09-18, 운영 6.3.2 / 개발 8.0.1 양쪽).
/// 로그인·토큰이 필요 없으므로 세션 축출 위험이 없다.</para>
/// <para><b>경로 주의</b> — <c>openapi.json</c> 은 <c>/api</c> 아래가 아니라 <b>루트</b>에 있다
/// (실측: <c>https://localhost:8000/openapi.json</c>). <see cref="IApiSetupModel.Url"/> 이 보통 <c>…/api</c> 라서
/// 그대로 붙이면 404 다 — 루트 후보를 먼저 시도한다.</para>
/// <para><b>실패 정책(NFR-01/NFR-02)</b> — 네트워크·파싱·미상 버전은 <b>예외를 던지지 않고</b>
/// <see cref="EnumServerContract.V6_3"/> 폴백 + 경고 로그 + <c>false</c> 반환이다.
/// 운영이 6.3.2 이므로 틀렸을 때 손해가 가장 작은 방향이다.</para>
/// <para><b>스레드</b> — 부팅 시퀀스·백그라운드 양쪽에서 불릴 수 있다. <see cref="SemaphoreSlim"/> 1개로
/// 조회를 직렬화해 동시 호출에 중복 왕복이 나지 않게 한다. 캐시 필드는 <c>volatile</c> 로 읽는다.</para>
/// <para><b>인증서</b> — 로컬 개발 서버는 mkcert 자체서명이지만 mkcert 루트 CA 가 Windows 신뢰 저장소에
/// 설치돼 있어 검증을 우회할 필요가 없다. 레포에 <c>ServerCertificateCustomValidationCallback</c> 선례가
/// <b>0건</b>이므로(2026-09-18 확인) <see cref="ApiService"/> 와 동일하게 평범한
/// <see cref="HttpClientHandler"/> 를 쓴다 — 검증 비활성화는 도입하지 않는다.</para>
/// </remarks>
public sealed class ServerContractProbe : IServerContractProbe, IDisposable
{
    #region - Ctors -
    public ServerContractProbe(ILogService? log, IApiSetupModel setup)
    {
        _log = log;
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
    }
    #endregion
    #region - Implementation of Interface -
    /// <inheritdoc/>
    public EnumServerContract Contract => _contract;

    /// <inheritdoc/>
    public string? RawVersion => _rawVersion;

    /// <inheritdoc/>
    public bool IsResolved => _isResolved;

    /// <inheritdoc/>
    public Task<bool> ResolveAsync(CancellationToken token = default)
        => AcquireAsync(force: false, token);

    /// <inheritdoc/>
    public Task<bool> RefreshAsync(CancellationToken token = default)
        => AcquireAsync(force: true, token);
    #endregion
    #region - Overrides -
    public void Dispose()
    {
        try
        {
            _client?.Dispose();
            _gate.Dispose();
        }
        catch { /* 종료 경로 — 삼킨다 */ }
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// 계약 세대를 확보한다. <paramref name="force"/> 가 <c>false</c> 면 이미 확보된 경우 재조회하지 않는다(멱등).
    /// 어떤 실패도 예외로 새어나가지 않는다.
    /// </summary>
    private async Task<bool> AcquireAsync(bool force, CancellationToken token)
    {
        // lock 밖 빠른 경로 — 이미 확보했고 강제 갱신이 아니면 왕복 자체를 하지 않는다.
        if (!force && _isResolved) return true;

        var entered = false;
        try
        {
            await _gate.WaitAsync(token).ConfigureAwait(false);
            entered = true;

            // 대기 중 다른 호출이 먼저 확보했을 수 있다(중복 왕복 방지).
            if (!force && _isResolved) return true;

            if (force)
            {
                // 캐시 폐기 — 재확보 실패 시에도 폴백값(V6_3)을 유지해야 한다.
                _isResolved = false;
                _rawVersion = null;
                _contract = FALLBACK;
            }

            return await FetchAndNormalizeAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log?.Warning($"[ServerContract] 확보가 취소되었습니다. 폴백 {FALLBACK} 유지(NFR-01).");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerContract] 확보 중 예기치 않은 오류: {ex.Message}. 폴백 {FALLBACK} 유지(NFR-01).");
            return false;
        }
        finally
        {
            if (entered)
            {
                try { _gate.Release(); } catch (ObjectDisposedException) { /* Dispose 경합 */ }
            }
        }
    }

    /// <summary>후보 URL 을 차례로 두드려 <c>info.version</c> 을 찾고 정규화한다.</summary>
    private async Task<bool> FetchAndNormalizeAsync(CancellationToken token)
    {
        var candidates = BuildCandidates(_setup.Url);
        if (candidates.Count == 0)
        {
            _log?.Warning($"[ServerContract] BaseAddress 가 비었거나 절대 URI 가 아닙니다(Url='{_setup.Url}'). 폴백 {FALLBACK} 유지(NFR-01).");
            return false;
        }

        var client = EnsureClient();
        string? lastFailure = null;

        foreach (var uri in candidates)
        {
            try
            {
                using var res = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    lastFailure = $"{uri} → HTTP {(int)res.StatusCode}";
                    continue;
                }

                var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                var raw = ExtractInfoVersion(json);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    lastFailure = $"{uri} → info.version 없음";
                    continue;
                }

                var contract = Normalize(raw!, out var recognized);
                _rawVersion = raw;
                _contract = contract;
                _isResolved = true;

                if (recognized)
                    _log?.Info($"[ServerContract] {raw} → {contract}");
                else
                    _log?.Warning($"[ServerContract] 미상 버전 '{raw}' → 폴백 {contract} 적용(NFR-01).");

                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;   // 호출자 취소는 AcquireAsync 가 처리
            }
            catch (Exception ex)
            {
                // 타임아웃(TaskCanceledException) · TLS · DNS · 연결거부 전부 여기로 — 다음 후보로 넘어간다.
                lastFailure = $"{uri} → {ex.GetType().Name}: {ex.Message}";
            }
        }

        _log?.Warning($"[ServerContract] 버전 확보 실패({lastFailure}). 폴백 {FALLBACK} 유지(NFR-01).");
        return false;
    }

    /// <summary>
    /// <c>openapi.json</c> 후보 경로. 루트 우선 — 실측상 <c>/api</c> 아래에는 없다.
    /// </summary>
    /// <remarks>
    /// ① <c>scheme://authority/openapi.json</c>(정상 경로)<br/>
    /// ② 서브패스 호스팅 대비 — base 경로에서 마지막 <c>/api</c> 를 떼고 붙인 경로<br/>
    /// ③ 최후 — base 경로 그대로 아래(<c>…/api/openapi.json</c>)
    /// </remarks>
    private static List<Uri> BuildCandidates(string? baseUrl)
    {
        var result = new List<Uri>();
        if (string.IsNullOrWhiteSpace(baseUrl)) return result;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var b)) return result;

        var authority = b.GetLeftPart(UriPartial.Authority);
        var path = b.AbsolutePath.TrimEnd('/');   // "/api" · "" · "/gop/api"

        void Add(string absolute)
        {
            if (Uri.TryCreate(absolute, UriKind.Absolute, out var u) &&
                !result.Exists(x => string.Equals(x.AbsoluteUri, u.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
                result.Add(u);
        }

        Add($"{authority}/{OPENAPI}");

        if (path.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            Add($"{authority}{path.Substring(0, path.Length - 4)}/{OPENAPI}");

        if (path.Length > 0)
            Add($"{authority}{path}/{OPENAPI}");

        return result;
    }

    /// <summary><c>info.version</c> 만 뽑는다. 스키마가 달라도 예외를 내지 않는다.</summary>
    private static string? ExtractInfoVersion(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("info", out var info)) return null;
            if (info.ValueKind != JsonValueKind.Object) return null;
            if (!info.TryGetProperty("version", out var ver)) return null;
            return ver.ValueKind == JsonValueKind.String ? ver.GetString() : ver.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 버전 문자열 → 계약 세대. <c>6.x</c>(6.3.x 포함)→V6_3 · <c>7.x</c>→V7_0 · <c>8.x</c> 이상→V8_0.
    /// 파싱 실패·그 밖(5.x 등)은 폴백 V6_3 이고 <paramref name="recognized"/> 가 <c>false</c> 다.
    /// </summary>
    internal static EnumServerContract Normalize(string raw, out bool recognized)
    {
        recognized = false;
        if (string.IsNullOrWhiteSpace(raw)) return FALLBACK;

        var trimmed = raw.Trim().TrimStart('v', 'V');
        var major = LeadingInt(trimmed);
        if (major < 0) return FALLBACK;

        recognized = true;
        if (major >= 8) return EnumServerContract.V8_0;
        if (major == 7) return EnumServerContract.V7_0;
        if (major == 6) return EnumServerContract.V6_3;   // 6.3.x 뿐 아니라 6.x 전체를 6.3 계약으로 본다

        // 5.x 이하 — 우리가 아는 계약이 아니다. 폴백이지만 "미상"으로 표시한다.
        recognized = false;
        return FALLBACK;
    }

    /// <summary>선행 숫자만 정수로. 없으면 -1(파싱 실패).</summary>
    private static int LeadingInt(string s)
    {
        var i = 0;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        if (i == 0) return -1;
        return int.TryParse(s.Substring(0, i), out var v) ? v : -1;
    }

    /// <summary>
    /// 프로브 전용 HttpClient. <see cref="ApiService"/> 와 같은 평범한 <see cref="HttpClientHandler"/> 구성이며
    /// 인증 핸들러·자격증명은 붙이지 않는다(openapi.json 은 무인증 200 — 세션에 손대지 않는다).
    /// </summary>
    private HttpClient EnsureClient()
    {
        var client = _client;
        if (client != null) return client;

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        var timeoutSec = _setup.Timeout > 0 ? _setup.Timeout : TIMEOUT;
        client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSec) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

        _client = client;
        return client;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    /// <summary>판정 실패 시의 보수적 기본값(NFR-01) — 운영 서버가 6.3.2 다.</summary>
    private const EnumServerContract FALLBACK = EnumServerContract.V6_3;
    private const string OPENAPI = "openapi.json";
    private const int TIMEOUT = 10;

    private readonly ILogService? _log;
    private readonly IApiSetupModel _setup;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private HttpClient? _client;
    private volatile EnumServerContract _contract = FALLBACK;
    private volatile string? _rawVersion;
    private volatile bool _isResolved;
    #endregion
}

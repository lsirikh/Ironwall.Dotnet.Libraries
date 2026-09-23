using System.Collections.Concurrent;
using System.Net.Http;

namespace LiveApiRoundTrip;

/// <summary>
/// TERMINAL DelegatingHandler handed to <c>ApiService(log, setup, authHandler)</c>.
///
/// Why terminal: ApiService assigns <c>authHandler.InnerHandler = new HttpClientHandler()</c>
/// (no TLS override). The local test server uses a self-signed cert, so we must own the
/// transport. We therefore never call <c>base.SendAsync</c>; we forward through our own
/// invoker (BearerAuthHandler -> cert-bypassing HttpClientHandler).
///
/// The captured body is the REAL serialized wire body produced by ApiService's own
/// Newtonsoft settings - it is NOT a re-serialization of the DTO (trap #2).
/// </summary>
public sealed class WireCaptureHandler : DelegatingHandler
{
    readonly HttpMessageInvoker _invoker;
    readonly ConcurrentQueue<WireExchange> _log;
    // 실행 전체에서 하나로 이어지는 번호. 점검이 두 번째 Bootstrap(다른 사용자 로그인)을 만들면
    // 처리기가 둘이 되는데, 번호를 처리기마다 따로 매기면 같은 Recorder.Wire 안에서 번호가 겹쳐
    // LastSeq/Since 가 주 연결의 요청을 건너뛰었다(모든 뒤 점검이 "body=(none)" 거짓 실패).
    static int _seq;

    public string CurrentTag { get; set; } = "";

    public WireCaptureHandler(HttpMessageHandler realChain, ConcurrentQueue<WireExchange> log)
    {
        _invoker = new HttpMessageInvoker(realChain, disposeHandler: false);
        _log = log;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Safety net: assert loopback on EVERY single request, not just at startup.
        if (request.RequestUri is not null) LoopbackGuard.Assert(request.RequestUri);

        var ex = new WireExchange
        {
            Seq = Interlocked.Increment(ref _seq),
            Method = request.Method.Method,
            Uri = request.RequestUri?.ToString() ?? "",
            Tag = CurrentTag,
        };

        if (request.Content is not null)
        {
            try { ex.RequestBody = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
            catch (Exception e) { ex.RequestBody = "<<unreadable: " + e.Message + ">>"; }
        }

        HttpResponseMessage resp;
        try
        {
            resp = await _invoker.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            ex.Status = -1;
            ex.ResponseBody = "<<transport failure: " + e.GetType().Name + ": " + e.Message + ">>";
            _log.Enqueue(ex);
            throw;
        }

        ex.Status = (int)resp.StatusCode;
        if (resp.Content is not null)
        {
            // Buffer so the caller can still read it.
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            ex.ResponseBody = System.Text.Encoding.UTF8.GetString(bytes);
            var replacement = new ByteArrayContent(bytes);
            foreach (var h in resp.Content.Headers) replacement.Headers.TryAddWithoutValidation(h.Key, h.Value);
            resp.Content = replacement;
        }
        _log.Enqueue(ex);
        return resp;
    }
}

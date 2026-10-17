using System.Text.RegularExpressions;

namespace LiveApiRoundTrip;

/// <summary>Hard guard: this harness may only ever speak to loopback.</summary>
public static class LoopbackGuard
{
    public static void Assert(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("SAFETY ABORT: empty base url.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"SAFETY ABORT: not an absolute uri: {url}");
        Assert(uri);
    }

    public static void Assert(Uri uri)
    {
        var host = uri.Host;
        var ok = host is "127.0.0.1" or "localhost" or "::1" or "[::1]"
                 || host.StartsWith("127.", StringComparison.Ordinal);
        if (!ok)
            throw new InvalidOperationException(
                $"SAFETY ABORT: non-loopback host '{host}' in {uri}. This harness refuses to talk to anything but 127.0.0.1.");
    }
}

/// <summary>One captured request/response pair, as it actually went on the wire.</summary>
public sealed class WireExchange
{
    public int Seq;
    public string Method = "";
    public string Uri = "";
    public string RequestBody = "";
    public int Status;
    public string ResponseBody = "";
    public string Tag = "";
    public DateTimeOffset At = DateTimeOffset.Now;

    static readonly Regex TokenRx = new(@"(eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,})", RegexOptions.Compiled);
    static readonly Regex PwRx = new(@"(""(?:password|Password|user_password|pw)""\s*:\s*)""[^""]*""", RegexOptions.Compiled);

    public static string Redact(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        s = TokenRx.Replace(s, "***JWT_REDACTED***");
        s = PwRx.Replace(s, @"$1""***REDACTED***""");
        return s;
    }

    public string RequestBodyRedacted => Redact(RequestBody);
    public string ResponseBodyRedacted => Redact(ResponseBody);
}

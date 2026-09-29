using System.Text.RegularExpressions;

namespace LiveNatsPipeline;

/// <summary>
/// Hard isolation gates. Every publish (ours and the pipeline's own) and every URL goes through here.
/// </summary>
public static class Safety
{
    /// <summary>The ONLY NATS group this probe may publish to or run its pipeline under.</summary>
    public const string Domain = "sensorway";
    public const string Group = "unit999";
    public const string Subsystem = "gis";
    public static readonly string SubjectPrefix = $"{Domain}.{Group}.";

    /// <summary>Refuse any subject outside sensorway.unit999.* (the headed GIS round lives on unit001 and global).</summary>
    public static void AssertSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject) || !subject.StartsWith(SubjectPrefix, StringComparison.Ordinal)
            || subject.Contains('*') || subject.Contains('>'))
            throw new InvalidOperationException(
                $"SAFETY ABORT: subject '{subject}' is outside {SubjectPrefix}* - this probe never publishes to another group.");
    }

    public static void AssertLoopback(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"SAFETY ABORT: not an absolute uri: {url}");
        var host = uri.Host;
        var ok = host is "127.0.0.1" or "localhost" or "::1" or "[::1]" || host.StartsWith("127.", StringComparison.Ordinal);
        if (!ok || url.Contains("123.141.236.253"))
            throw new InvalidOperationException($"SAFETY ABORT: non-loopback host '{host}' in {url}.");
    }

    static readonly Regex TokenRx = new(@"eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}", RegexOptions.Compiled);
    public static string Redact(string s) => string.IsNullOrEmpty(s) ? s ?? "" : TokenRx.Replace(s, "***JWT***");
}

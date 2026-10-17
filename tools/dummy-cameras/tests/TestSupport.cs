using System.Collections.Concurrent;
using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace DummyCameras.Tests;

/// <summary>ILogService fake - collects lines.</summary>
internal sealed class TestLog : ILogService
{
    private readonly ConcurrentQueue<string> _lines = new();

    public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }

    public IReadOnlyCollection<string> Lines => _lines.ToArray();

    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _lines.Enqueue($"ERROR {msg}");
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _lines.Enqueue($"INFO {msg}");
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _lines.Enqueue($"WARN {msg}");
}

internal static class Wait
{
    /// <summary>Polls a condition (no fixed sleeps in assertions).</summary>
    public static async Task<bool> UntilAsync(Func<bool> condition, TimeSpan timeout, int pollMs = 50)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition()) return true;
            await Task.Delay(pollMs);
        }
        return condition();
    }
}

internal static class TestDirs
{
    public static string NewOutDir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dummycam-tests", $"{name}-{Guid.NewGuid():N}"[..(name.Length + 9)]);
        Directory.CreateDirectory(dir);
        return dir;
    }
}

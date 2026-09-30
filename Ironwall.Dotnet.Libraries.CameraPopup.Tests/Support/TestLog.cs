using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>ILogService 가짜 — 줄을 모아 두고 시험 출력으로 흘린다.</summary>
internal sealed class TestLog : ILogService
{
    private readonly ConcurrentQueue<string> _lines = new();

    public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }

    public IReadOnlyCollection<string> Lines => _lines.ToArray();

    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _lines.Enqueue($"ERROR {msg}");
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _lines.Enqueue($"INFO {msg}");
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _lines.Enqueue($"WARN {msg}");
}

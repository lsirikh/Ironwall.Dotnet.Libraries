using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace LiveNatsPipeline;

public sealed record LogLine(long Seq, DateTime At, string Level, string Message, string Where);

/// <summary>
/// The pipeline's ILogService. Every line the REAL library/host code writes is kept (with caller file:line)
/// so a verdict can quote the exact log evidence.
/// </summary>
public sealed class CaptureLog : ILogService
{
    readonly ConcurrentQueue<LogLine> _lines = new();
    long _seq;
    readonly StreamWriter? _file;
    readonly object _fileGate = new();

    public CaptureLog(string? filePath = null)
    {
        if (filePath != null)
            _file = new StreamWriter(filePath, false, new UTF8Encoding(true)) { AutoFlush = true };
    }

    public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }

    public long Mark => Interlocked.Read(ref _seq);

    public IEnumerable<LogLine> Since(long mark) => _lines.Where(l => l.Seq > mark).OrderBy(l => l.Seq);

    public void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        => Add("ERROR", msg, filePath, lineNumber);
    public void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        => Add("INFO", msg, filePath, lineNumber);
    public void Warning(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        => Add("WARN", msg, filePath, lineNumber);

    public void Probe(string msg) => Add("PROBE", msg, "", 0);

    void Add(string level, string msg, string file, int line)
    {
        var where = string.IsNullOrEmpty(file) ? "" : $"{Path.GetFileName(file)}:{line}";
        var l = new LogLine(Interlocked.Increment(ref _seq), DateTime.Now, level, msg ?? "", where);
        _lines.Enqueue(l);
        if (_file != null)
            lock (_fileGate) _file.WriteLine($"{l.At:HH:mm:ss.fff} {l.Level,-5} [{l.Where}] {l.Message}");
    }
}

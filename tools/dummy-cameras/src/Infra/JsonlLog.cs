using System.Text.Encodings.Web;
using System.Text.Json;

namespace DummyCameras.Infra;

/// <summary>
/// Append-only JSON Lines file (one object per line, UTF-8, flushed per line) - the assertion surface for headed tests.
/// Thread-safe. Never logs credentials: callers pass only operation data.
/// </summary>
public sealed class JsonlLog : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private readonly StreamWriter? _writer;

    public JsonlLog(string? path)
    {
        Path_ = path;
        if (path is null) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(fs, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
    }

    public string? Path_ { get; }

    /// <summary>Also raised for in-process observers (tests) - outside the lock.</summary>
    public event Action<IReadOnlyDictionary<string, object?>>? Written;

    public void Write(IReadOnlyDictionary<string, object?> entry)
    {
        var line = JsonSerializer.Serialize(entry, Json);
        lock (_gate) { _writer?.WriteLine(line); }
        Written?.Invoke(entry);
    }

    public void Dispose()
    {
        lock (_gate) { _writer?.Dispose(); }
    }
}

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host;

/// <summary>
/// 호스트 파일 로그. 줄마다 즉시 flush — 네이티브 충돌로 프로세스가 사라져도 마지막 줄까지 남게.
/// 경로: <c>{logDir}\camera-popup-host-yyyyMMdd.log</c>(기본 %LOCALAPPDATA%\Ironwall\CameraPopupHost\logs).
/// 비밀번호 · 계정은 찍지 않는다(VideoProviderInfo.ToString 이 가린다).
/// </summary>
internal sealed class HostLog
{
    private readonly object _gate = new();
    private readonly StreamWriter? _writer;
    private readonly int _pid = Environment.ProcessId;

    public string? FilePath { get; }

    public HostLog(string? directory)
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ironwall", "CameraPopupHost", "logs")
                : directory!;
            Directory.CreateDirectory(dir);
            FilePath = Path.Combine(dir, $"camera-popup-host-{DateTime.Now:yyyyMMdd}.log");
            var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _writer = null; // 로그를 못 써도 호스트는 동작한다.
            Debug.WriteLine($"[CameraPopupHost] log init failed: {ex.Message}");
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{_pid}] [T{Environment.CurrentManagedThreadId}] {message}");
        lock (_gate)
        {
            try { _writer?.WriteLine(line); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { /* 로그 실패는 무시 */ }
        }
    }
}

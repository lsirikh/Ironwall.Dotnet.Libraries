using System.Diagnostics;

namespace DummyCameras.Infra;

/// <summary>
/// A child process (MediaMTX · ffmpeg) whose stdout/stderr go to a log file, assigned to the kill-on-close job object.
/// Keeps the last lines in memory for the status endpoint.
/// </summary>
public sealed class ManagedProcess : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter? _log;
    private readonly object _gate = new();
    private readonly Queue<string> _tail = new();

    private ManagedProcess(Process process, StreamWriter? log)
    {
        _process = process;
        _log = log;
    }

    public static ManagedProcess Start(string exe, IEnumerable<string> args, string workingDir, string? logPath)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,   // ffmpeg reads stdin for 'q'; give it a pipe so it never grabs the console
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        StreamWriter? log = null;
        if (logPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            log = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { AutoFlush = true };
            log.WriteLine($"==== {DateTime.Now:O} start: {exe} {string.Join(' ', psi.ArgumentList)}");
        }

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var mp = new ManagedProcess(p, log);
        p.OutputDataReceived += (_, e) => mp.OnLine(e.Data);
        p.ErrorDataReceived += (_, e) => mp.OnLine(e.Data);
        p.Start();
        ChildProcessJob.Assign(p);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return mp;
    }

    private void OnLine(string? line)
    {
        if (line is null) return;
        lock (_gate)
        {
            _log?.WriteLine(line);
            _tail.Enqueue(line);
            while (_tail.Count > 40) _tail.Dequeue();
        }
    }

    public int Id => _process.Id;
    public bool HasExited { get { try { return _process.HasExited; } catch (InvalidOperationException) { return true; } } }
    public int? ExitCode { get { try { return _process.HasExited ? _process.ExitCode : null; } catch (InvalidOperationException) { return null; } } }
    public Task WaitForExitAsync(CancellationToken ct) => _process.WaitForExitAsync(ct);

    public IReadOnlyList<string> Tail { get { lock (_gate) return _tail.ToList(); } }

    public void Kill()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        Kill();
        try { _process.WaitForExit(3000); } catch { }
        _process.Dispose();
        lock (_gate) _log?.Dispose();
    }
}

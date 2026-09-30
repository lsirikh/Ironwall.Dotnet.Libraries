using System.Globalization;
using DummyCameras.Infra;
using DummyCameras.Ptz;

namespace DummyCameras.Streams;

/// <summary>
/// Writes each camera's overlay text (name · type · ONVIF port · PTZ position/motion · active faults) for ffmpeg's
/// <c>drawtext textfile=…:reload=N</c>, so a PTZ command is VISIBLE on the video in headed tests.
/// Replaces the file atomically (write temp → move) and retries while ffmpeg has it open.
/// </summary>
public sealed class OverlayWriter : IDisposable
{
    private readonly string _dir;
    private readonly IReadOnlyList<(CameraSpec Cam, PtzSimulator? Ptz)> _cams;
    private readonly Dictionary<string, string> _last = new();
    private readonly Timer _timer;

    public OverlayWriter(string dir, IReadOnlyList<(CameraSpec Cam, PtzSimulator? Ptz)> cams)
    {
        _dir = dir;
        _cams = cams;
        WriteAll();
        _timer = new Timer(_ => WriteAll(), null, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
    }

    public static string Text(CameraSpec cam, PtzSnapshot? s, DateTime nowUtc)
    {
        var lines = new List<string>
        {
            $"{cam.Name}  {(cam.IsPtz ? "PTZ" : "FIXED")}  onvif:{cam.OnvifPort}",
        };
        if (s is not null)
        {
            string motion = s.Motion switch
            {
                "goto" => $"MOVING -> {(s.TargetPreset == "home" ? "HOME" : "P" + s.TargetPreset)} ({Math.Max(0, (s.ArrivesAtUtc!.Value - nowUtc).TotalSeconds).ToString("0.0", CultureInfo.InvariantCulture)}s)",
                "continuous" => $"MOVING v=({s.Velocity.Pan:+0.0;-0.0},{s.Velocity.Tilt:+0.0;-0.0},{s.Velocity.Zoom:+0.0;-0.0})",
                _ => "IDLE",
            };
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"P {s.Position.Pan:+0.00;-0.00}  T {s.Position.Tilt:+0.00;-0.00}  Z {s.Position.Zoom:0.00}  {motion}"));
        }
        var faults = cam.Faults.ToString();
        if (faults != "none") lines.Add("fault: " + faults);
        // drawtext expands '%' sequences and treats '\' specially - keep the text plain.
        return string.Join('\n', lines).Replace("%", "pct").Replace("\\", "/");
    }

    private void WriteAll()
    {
        var now = DateTime.UtcNow;
        foreach (var (cam, ptz) in _cams)
        {
            var text = Text(cam, ptz?.Status(now), now);
            lock (_last)
            {
                if (_last.TryGetValue(cam.Id, out var prev) && prev == text) continue;
                _last[cam.Id] = text;
            }
            var path = Path.Combine(_dir, $"{cam.Id}.txt");
            var tmp = path + ".tmp";
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.WriteAllText(tmp, text);
                    File.Move(tmp, path, overwrite: true);
                    break;
                }
                catch (IOException) { Thread.Sleep(10); }
                catch (UnauthorizedAccessException) { Thread.Sleep(10); }
            }
        }
    }

    public void Dispose() => _timer.Dispose();
}

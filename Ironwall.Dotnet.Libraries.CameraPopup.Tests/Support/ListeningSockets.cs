using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>
/// 프로세스가 연 "들어오는 연결을 받을 수 있는" 소켓(TCP 대기 · UDP 바인드) — Windows 방화벽 창이 뜨는 조건(FW).
/// <c>netstat -ano</c> 를 파싱한다(상태 글자는 언어마다 달라 TCP 대기는 상대 주소 <c>0.0.0.0:0</c> · <c>[::]:0</c> 로 가린다).
/// 결과 꼴: <c>"TCP 0.0.0.0:554"</c> · <c>"UDP [::]:5353"</c>.
/// </summary>
internal static class ListeningSockets
{
    public static IReadOnlyList<string> Of(int pid)
    {
        var psi = new ProcessStartInfo("netstat", "-ano") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(psi)!;
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var result = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var f = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 4 || !int.TryParse(f[^1], out var owner) || owner != pid) continue;
            if (f[0] == "TCP" && f.Length == 5 && f[2] is "0.0.0.0:0" or "[::]:0") result.Add($"TCP {f[1]}");
            else if (f[0] == "UDP") result.Add($"UDP {f[1]}");
        }
        return result;
    }

    /// <summary>루프백에만 묶인 소켓 — 밖에서 닿지 않아 방화벽 창이 뜨지 않는다.</summary>
    public static bool IsLoopback(string socket)
    {
        var address = socket[4..];
        return address.StartsWith("127.", StringComparison.Ordinal) || address.StartsWith("[::1]", StringComparison.Ordinal);
    }

    /// <summary><paramref name="until"/> 가 참이 되거나 <paramref name="duration"/> 이 지날 때까지 표본을 떠 합집합을 돌려준다.</summary>
    public static async Task<IReadOnlySet<string>> SampleAsync(int pid, TimeSpan duration, Func<bool>? until = null)
    {
        var seen = new SortedSet<string>(StringComparer.Ordinal);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < duration && until?.Invoke() != true)
        {
            foreach (var s in Of(pid)) seen.Add(s);
            await Task.Delay(200);
        }
        foreach (var s in Of(pid)) seen.Add(s);
        return seen;
    }
}

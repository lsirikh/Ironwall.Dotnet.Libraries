using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 시험 호스트에서 끌기 진단 기록(<c>IRONWALL_DRAG_TRACE</c>)을 켠다 — 재현 시험이 실창과 같은 기록(칸 모양 · 후보 점 · Tab 이 캔버스에 닿았나)을 읽어 단언에 쓴다.
/// </summary>
/// <remarks>
/// <c>DragTrace</c> 는 경로를 처음 한 번만 읽는다(정적 필드) — 그래서 어셈블리가 처음 쓰일 때 켠다. 이미 값이 있으면 그대로 둔다.
/// 시험 호스트가 아니면(GIS 앱) 아무것도 하지 않는다(<see cref="TestCrashLog.IsTestHost"/>).
/// </remarks>
internal static class TestDragTrace
{
    /// <summary>이 프로세스의 기록 파일(꺼져 있으면 <c>null</c>).</summary>
    internal static string? Path => Environment.GetEnvironmentVariable("IRONWALL_DRAG_TRACE") is { Length: > 0 } p ? p : null;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!TestCrashLog.IsTestHost || Path is not null) return;
        Environment.SetEnvironmentVariable("IRONWALL_DRAG_TRACE", System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devices-ui-test-drag-trace-{Environment.ProcessId}.log"));
    }

    /// <summary>지금 기록 끝(바이트) — <see cref="ReadFrom"/> 와 짝.</summary>
    internal static long Mark() => Path is { } p && File.Exists(p) ? new FileInfo(p).Length : 0;

    /// <summary><paramref name="mark"/> 뒤에 쓰인 기록(다른 시험이 같은 때 쓴 줄이 섞일 수 있다 — 단언은 '들어 있나' 로).</summary>
    internal static string ReadFrom(long mark)
    {
        if (Path is not { } p || !File.Exists(p)) return string.Empty;
        using var stream = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(Math.Min(mark, stream.Length), SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

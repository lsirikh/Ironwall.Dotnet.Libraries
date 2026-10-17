using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 간헐 크래시의 흔적(시험 진단 전용) — 처리 안 된 예외 · 관찰 안 된 Task 예외를 전체 스택 + 스레드 이름과 함께
/// <c>%TEMP%\devices-ui-test-crash.log</c> 에 덧붙인다. 다음 간헐 크래시("Collection was modified" 등)가 스택을 남기게.
/// </summary>
/// <remarks>
/// 이 프로젝트는 시험 코드가 제품 어셈블리(<c>Devices.Ui.dll</c>)에 함께 들어간다 — 그래서 <b>시험 호스트 프로세스일 때만</b> 건다
/// (진입 어셈블리가 <c>testhost</c> 이거나 프로세스 이름이 <c>testhost</c> 로 시작). GIS 앱에서는 아무것도 하지 않는다.
/// </remarks>
internal static class TestCrashLog
{
    private static readonly object Gate = new();

    internal static string LogPath => Path.Combine(Path.GetTempPath(), "devices-ui-test-crash.log");

    /// <summary>지금 시험 호스트 안인가.</summary>
    internal static bool IsTestHost
    {
        get
        {
            var entry = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
            var process = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
            return entry.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
                   || process.StartsWith("testhost", StringComparison.OrdinalIgnoreCase);
        }
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!IsTestHost) return;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("UnhandledException" + (e.IsTerminating ? " (terminating)" : string.Empty), e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Write("UnobservedTaskException", e.Exception);
    }

    /// <summary>한 건을 덧붙인다 — 실패해도 던지지 않는다(진단이 크래시를 키우지 않게).</summary>
    internal static void Write(string kind, Exception? exception)
    {
        try
        {
            var thread = Thread.CurrentThread;
            var text = new StringBuilder()
                .AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} · {kind} · pid {Environment.ProcessId}")
                .AppendLine($"thread: id {thread.ManagedThreadId} · name '{thread.Name ?? "(없음)"}' · pool {thread.IsThreadPoolThread} · background {thread.IsBackground} · apartment {thread.GetApartmentState()}")
                .AppendLine(exception?.ToString() ?? "(예외 객체 없음)")
                .AppendLine();
            lock (Gate) File.AppendAllText(LogPath, text.ToString(), Encoding.UTF8);
        }
        catch (Exception)
        {
            // 진단 기록 실패는 무시한다 — 크래시 경로에서 또 던지면 원래 예외의 흔적까지 잃는다.
        }
    }
}

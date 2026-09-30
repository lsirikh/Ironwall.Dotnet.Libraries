using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>
/// 시험 프로세스(= GIS 대역)에 처리 안 된 예외가 났는지 센다. 생존 시험은 끝에 0 을 단언한다.
/// (진짜 처리 안 된 예외라면 시험 실행 자체가 중단된다 — 그것 역시 실패로 드러난다.)
/// </summary>
internal static class CrashWitness
{
    private static int _unhandled;
    private static int _unobserved;

    public static int Unhandled => Volatile.Read(ref _unhandled);
    public static int Unobserved => Volatile.Read(ref _unobserved);

    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Interlocked.Increment(ref _unhandled);
        TaskScheduler.UnobservedTaskException += (_, _) => Interlocked.Increment(ref _unobserved);
    }
}

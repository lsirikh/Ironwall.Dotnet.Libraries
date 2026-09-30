namespace Ironwall.Dotnet.Libraries.CameraPopup.Host;

/// <summary>
/// 즉시 종료. <c>Environment.Exit</c> 는 종료자 · 정리 코드를 돌리는데, LibVLC 가 네이티브에서 멈춰 있으면
/// 그 정리가 끝나지 않아 프로세스가 좀비로 남을 수 있다 → 로그를 남기고 TerminateProcess 로 끝낸다.
/// 종료 코드는 감시자가 읽는다(<c>HostExitCodes</c>).
/// </summary>
internal static class HostExit
{
    private static HostLog? _log;
    private static int _exiting;

    public static void Initialize(HostLog log) => _log = log;

    public static void Now(int exitCode, string reason)
    {
        if (Interlocked.Exchange(ref _exiting, 1) == 1) return;
        _log?.Info($"exit code={exitCode} reason={reason}");
        NativeMethods.TerminateProcess(NativeMethods.GetCurrentProcess(), unchecked((uint)exitCode));
        Environment.Exit(exitCode); // TerminateProcess 가 실패했을 때만 여기 온다.
    }
}

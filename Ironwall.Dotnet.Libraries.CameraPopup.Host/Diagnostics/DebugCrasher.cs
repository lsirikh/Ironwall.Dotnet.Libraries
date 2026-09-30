using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Diagnostics;

/// <summary>
/// 디버그 전용 고장 주입(호스트가 <c>--debug-commands</c> 로 시작됐을 때만 불린다).
/// 생존 시험이 "네이티브 충돌 · 멈춤은 호스트 안에 머물고 GIS 는 산다"를 실제로 증명하는 데 쓴다.
/// </summary>
internal static class DebugCrasher
{
    public static void Execute(DebugCommandKind kind, Dispatcher dispatcher, HostLog log)
    {
        log.Warn($"debug command {kind}");
        switch (kind)
        {
            case DebugCommandKind.Hang:
                // UI 스레드를 영원히 막는다 → 심박 응답(UI 스레드 경유)이 끊긴다.
                dispatcher.BeginInvoke(() => Thread.Sleep(Timeout.Infinite));
                break;
            case DebugCommandKind.FailFast:
                Environment.FailFast("camera popup host debug FailFast");
                break;
            case DebugCommandKind.NativeAccessViolation:
                // 네이티브 코드(ntdll RtlMoveMemory) 안에서 잘못된 주소에 쓴다 — .NET 이 잡을 수 없는 0xC0000005.
                var thread = new Thread(() => NativeMethods.RtlMoveMemory(new IntPtr(0x10), new IntPtr(0x20), new IntPtr(64)))
                { IsBackground = true, Name = "debug-av" };
                thread.Start();
                break;
            case DebugCommandKind.UnhandledException:
                new Thread(() => throw new InvalidOperationException("camera popup host debug unhandled exception"))
                { IsBackground = true, Name = "debug-throw" }.Start();
                break;
            case DebugCommandKind.DispatcherException:
                dispatcher.BeginInvoke(() => throw new InvalidOperationException("camera popup host debug dispatcher exception"));
                break;
        }
    }
}

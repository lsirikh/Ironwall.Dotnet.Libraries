using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Diagnostics;

/// <summary>
/// 디버그 전용 고장 주입(호스트가 <c>--debug-commands</c> 로 시작됐을 때만 불린다).
/// 생존 시험이 "네이티브 충돌 · 멈춤은 호스트 안에 머물고 GIS 는 산다"를 실제로 증명하는 데 쓴다.
/// </summary>
internal static class DebugCrasher
{
    public static void Execute(DebugCommandKind kind, Dispatcher dispatcher, HostLog log)
        => Execute(new DebugCommand { Kind = kind }, dispatcher, log, _ => { }, () => { });

    /// <summary>
    /// Normal 4줄 + Render 4줄 — 일 하나가 4 ms 를 태우고 곧바로 자기를 다시 넣는다. 디스패처에 늘 Normal · Render 일이 있어
    /// Background 이하 일은 끝날 때까지 한 번도 돌지 못한다(Send 표식은 일 사이에 돈다 — 멈춤으로 보이지 않는다).
    /// </summary>
    private static void StartUiFlood(Dispatcher dispatcher, int durationMs)
    {
        long until = Environment.TickCount64 + durationMs;
        foreach (var priority in new[] { DispatcherPriority.Normal, DispatcherPriority.Render })
        {
            for (int i = 0; i < 4; i++)
            {
                Action? chain = null;
                chain = () =>
                {
                    if (Environment.TickCount64 >= until) return;
                    long spinUntil = Environment.TickCount64 + 4;
                    while (Environment.TickCount64 < spinUntil) { }
                    dispatcher.BeginInvoke(priority, chain!);
                };
                dispatcher.BeginInvoke(priority, chain);
            }
        }
    }

    /// <param name="send">GIS 로 보내기.</param>
    /// <param name="closePipe">파이프를 끊는다(호스트는 아직 산 채로).</param>
    public static void Execute(DebugCommand command, Dispatcher dispatcher, HostLog log, Action<IIpcMessage> send, Action closePipe)
    {
        var kind = command.Kind;
        log.Warn($"debug command {kind} {command.Argument}");
        switch (kind)
        {
            case DebugCommandKind.UiBusy:
                // UI 스레드를 잠깐만 막는다(창을 몰아 여는 바쁨) — 심박은 끊기면 안 된다.
                int busyMs = Math.Clamp(command.Argument, 0, 60_000);
                dispatcher.BeginInvoke(() => Thread.Sleep(busyMs));
                break;
            case DebugCommandKind.UiFlood:
                StartUiFlood(dispatcher, Math.Clamp(command.Argument, 0, 900_000));
                break;
            case DebugCommandKind.PlannedExitSlow:
                // 메모리 한도 종료 흉내: 예고 → 파이프 끊김 → 프로세스는 늦게 끝난다(큰 프로세스의 정리 시간).
                int lingerMs = Math.Clamp(command.Argument, 0, 60_000);
                new Thread(() =>
                {
                    send(new HostError { Code = HostError.MemoryLimitCode, Message = "debug" });
                    Thread.Sleep(200);
                    var exit = new Thread(() => HostExit.After(lingerMs, HostExitCodes.MemoryLimit, "debug planned exit")) { IsBackground = true, Name = "debug-planned-exit" };
                    exit.Start();
                    Thread.Sleep(50); // 종료를 먼저 맡은 뒤에 파이프를 끊는다(끊김 → "client disconnected" 종료가 앞지르지 않게)
                    closePipe();
                })
                { IsBackground = true, Name = "debug-planned" }.Start();
                break;
            case DebugCommandKind.Hang:
                // UI 스레드를 영원히 막는다 → 심박은 계속 오지만 UiStallMs 가 자란다(감시자가 긴 기준으로 죽인다).
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

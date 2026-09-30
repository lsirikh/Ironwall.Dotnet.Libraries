using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host;

internal static class NativeMethods
{
    /// <summary>충돌 시 WER "작동이 중지되었습니다" 대화 상자를 띄우지 않는다 — 대화 상자가 떠 있으면
    /// 프로세스가 내려가지 않아 감시자의 재시작이 늦어진다.</summary>
    public const uint SEM_FAILCRITICALERRORS = 0x0001;
    public const uint SEM_NOGPFAULTERRORBOX = 0x0002;
    public const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    [DllImport("kernel32.dll")]
    public static extern uint SetErrorMode(uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    /// <summary>디버그 전용 — 네이티브 코드 안에서 접근 위반을 일으키는 데 쓴다.</summary>
    [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
    public static extern void RtlMoveMemory(IntPtr destination, IntPtr source, IntPtr length);

    public static bool TryGetClientProcessId(NamedPipeServerStream server, out int processId)
    {
        processId = 0;
        if (!GetNamedPipeClientProcessId(server.SafePipeHandle, out var pid)) return false;
        processId = unchecked((int)pid);
        return true;
    }
}

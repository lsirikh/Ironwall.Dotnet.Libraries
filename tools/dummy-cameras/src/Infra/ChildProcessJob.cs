using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DummyCameras.Infra;

/// <summary>
/// Windows Job Object with KILL_ON_JOB_CLOSE: every child (MediaMTX · ffmpeg) is assigned to it, so the children die
/// with this process even on a crash or a killed test host - no orphan keeps port 8554 or a publisher running.
/// No-op off Windows.
/// </summary>
public static class ChildProcessJob
{
    private static readonly Lazy<IntPtr> Job = new(CreateJob);

    public static void Assign(Process process)
    {
        if (!OperatingSystem.IsWindows()) return;
        var job = Job.Value;
        if (job == IntPtr.Zero) return;
        try { AssignProcessToJobObject(job, process.Handle); }
        catch (InvalidOperationException) { /* already exited */ }
    }

    private static IntPtr CreateJob()
    {
        if (!OperatingSystem.IsWindows()) return IntPtr.Zero;
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) return IntPtr.Zero;
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE },
        };
        int len = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var ptr = Marshal.AllocHGlobal(len);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ptr, (uint)len);
        }
        finally { Marshal.FreeHGlobal(ptr); }
        return handle;   // intentionally never closed: closing the last handle kills the children (at process exit)
    }

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
}

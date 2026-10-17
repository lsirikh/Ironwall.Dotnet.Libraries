using System;
using System.Runtime.InteropServices;

namespace Ironwall.Dotnet.Libraries.SystemResources.Native;

/****************************************************************************
   Purpose      : 물리 메모리 사용률 취득(kernel32 GlobalMemoryStatusEx)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// kernel32 <c>GlobalMemoryStatusEx</c>로 물리 메모리 사용률을 읽는다. <c>dwMemoryLoad</c>가
/// 0..100 사용률 %를 <b>직접</b> 제공하므로 나눗셈·PDH·로케일 이슈가 없다. 관리자 권한 불요.
/// </summary>
internal static class MemoryInterop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;      // 0..100 사용률 %
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    /// <summary>
    /// 물리 메모리 사용률과 절대값을 읽는다.
    /// </summary>
    /// <param name="loadPercent">사용률 %(dwMemoryLoad, 0..100).</param>
    /// <param name="totalBytes">물리 메모리 총량.</param>
    /// <param name="usedBytes">사용 중 물리 메모리(Total − Avail).</param>
    /// <returns>성공 여부.</returns>
    public static bool TryRead(out double loadPercent, out ulong totalBytes, out ulong usedBytes)
    {
        loadPercent = 0;
        totalBytes = 0;
        usedBytes = 0;

        // dwLength는 호출 전 반드시 설정해야 한다(미설정 시 FALSE 반환).
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref m))
            return false;

        loadPercent = m.dwMemoryLoad;
        totalBytes = m.ullTotalPhys;
        usedBytes = m.ullTotalPhys >= m.ullAvailPhys ? m.ullTotalPhys - m.ullAvailPhys : 0;
        return true;
    }
}

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime;

namespace Ironwall.Dotnet.Libraries.Streaming.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:14:59 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 메모리 관리 헬퍼
/// </summary>
public static class MemoryHelper
{
    /// <summary>
    /// 현재 메모리 사용량 조회 (MB)
    /// </summary>
    public static double GetCurrentMemoryUsageMB()
    {
        return GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    }

    /// <summary>
    /// 강제 가비지 컬렉션
    /// </summary>
    public static void ForceGarbageCollection()
    {
        GC.Collect(2, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// 메모리 압박 체크
    /// </summary>
    public static bool IsMemoryPressure(long thresholdBytes)
    {
        return GC.GetTotalMemory(false) > thresholdBytes;
    }

    /// <summary>
    /// Large Object Heap 압축
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CompactLargeObjectHeap()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        ForceGarbageCollection();
    }

    /// <summary>
    /// 프로세스 메모리 정보 조회
    /// </summary>
    public static (long WorkingSet, long PrivateMemory, long VirtualMemory) GetProcessMemoryInfo()
    {
        using var process = Process.GetCurrentProcess();
        return (
            process.WorkingSet64,
            process.PrivateMemorySize64,
            process.VirtualMemorySize64
        );
    }
}
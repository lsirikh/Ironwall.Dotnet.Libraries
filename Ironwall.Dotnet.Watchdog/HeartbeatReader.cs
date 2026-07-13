using System.IO.MemoryMappedFiles;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;

namespace Ironwall.Dotnet.Watchdog;
/****************************************************************************
   Purpose      : 하트비트 공유메모리 판독(대상 PID 기준, PID 변경 시 재오픈).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 대상 앱이 기록하는 하트비트 공유메모리(<see cref="WatchdogNaming.HeartbeatMap"/>)를 판독한다.
/// 대상 PID가 바뀌면(재시작) 자동으로 재오픈한다. EpochMs는 오프셋 0(8B 정렬)에서 원자적으로 읽는다.
/// </summary>
internal sealed class HeartbeatReader : IDisposable
{
    private int _openedPid = -1;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;

    /// <summary>대상의 마지막 하트비트 UTC epoch(ms). 공유메모리 부재 시 null.</summary>
    public long? ReadEpochMs(int currentPid)
    {
        if (currentPid != _openedPid)
        {
            Close();
            _openedPid = currentPid;
        }

        if (!EnsureOpen(currentPid)) return null;

        try
        {
            return _accessor!.ReadInt64(WatchdogHeartbeat.OffsetEpochMs);
        }
        catch
        {
            Close();
            return null;
        }
    }

    private bool EnsureOpen(int pid)
    {
        if (_accessor != null) return true;
        try
        {
            _mmf = MemoryMappedFile.OpenExisting(WatchdogNaming.HeartbeatMap(pid), MemoryMappedFileRights.Read);
            _accessor = _mmf.CreateViewAccessor(0, WatchdogHeartbeat.Size, MemoryMappedFileAccess.Read);
            return true;
        }
        catch
        {
            Close();
            return false; // 아직 생성 전이거나 종료됨
        }
    }

    private void Close()
    {
        try { _accessor?.Dispose(); } catch { }
        try { _mmf?.Dispose(); } catch { }
        _accessor = null;
        _mmf = null;
    }

    public void Dispose() => Close();
}

using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : sout 로컬 릴레이용 포트 풀 관리 (Phase 2, FR-03)
   Created By   : GHLee
   Created On   : 2026-05-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// sout 로컬 RTSP 릴레이에 사용할 포트를 CameraKey별로 관리하는 레지스트리.
/// 포트 범위: 15554~15700. thread-safe.
/// </summary>
public class RtspRelayPortRegistry
{
    private const int PORT_MIN = 15554;
    private const int PORT_MAX = 15700;

    private readonly ConcurrentDictionary<string, int> _allocated = new();
    private readonly SortedSet<int> _available;
    private readonly object _lock = new();

    public RtspRelayPortRegistry()
    {
        _available = new SortedSet<int>(Enumerable.Range(PORT_MIN, PORT_MAX - PORT_MIN + 1));
    }

    /// <summary>
    /// cameraKey에 포트를 할당한다. 이미 할당된 포트가 있으면 그대로 반환.
    /// 가용 포트가 없으면 -1 반환.
    /// </summary>
    public int Acquire(string cameraKey)
    {
        if (_allocated.TryGetValue(cameraKey, out var existing))
            return existing;

        lock (_lock)
        {
            // double-check
            if (_allocated.TryGetValue(cameraKey, out existing))
                return existing;

            var port = PickAvailablePort();
            if (port < 0) return -1;

            _available.Remove(port);
            _allocated[cameraKey] = port;
            return port;
        }
    }

    /// <summary>
    /// cameraKey에 항상 새 포트를 강제 할당한다.
    /// 기존 포트는 풀에 반환 후 새 포트 할당. TIME_WAIT 방지용.
    /// 가용 포트가 없으면 -1 반환.
    /// </summary>
    public int AcquireNew(string cameraKey)
    {
        lock (_lock)
        {
            // 기존 포트 반환
            if (_allocated.TryRemove(cameraKey, out var old))
                _available.Add(old);

            var port = PickAvailablePort();
            if (port < 0) return -1;

            _available.Remove(port);
            _allocated[cameraKey] = port;
            return port;
        }
    }

    /// <summary>
    /// cameraKey에 할당된 포트를 풀에 반환한다.
    /// </summary>
    public void Release(string cameraKey)
    {
        lock (_lock)
        {
            if (_allocated.TryRemove(cameraKey, out var port))
                _available.Add(port);
        }
    }

    /// <summary>
    /// cameraKey에 현재 할당된 포트를 반환. 없으면 -1.
    /// </summary>
    public int GetPort(string cameraKey) =>
        _allocated.TryGetValue(cameraKey, out var p) ? p : -1;

    /// <summary>
    /// 해당 포트에 이미 TCP 리스너가 바인딩되어 있는지 확인 (SE-11 지원).
    /// </summary>
    public static bool IsPortBound(int port)
    {
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            return props.GetActiveTcpListeners().Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 현재 바인딩되지 않은 가용 포트 중 가장 작은 값을 선택.
    /// _lock 보유 상태에서 호출해야 함.
    /// </summary>
    private int PickAvailablePort()
    {
        foreach (var port in _available)
        {
            if (!IsPortBound(port))
                return port;
        }
        return -1;
    }

    /// <summary>
    /// 전체 할당 해제 (Dispose 시 호출).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var port in _allocated.Values)
                _available.Add(port);
            _allocated.Clear();
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// LibVLC 의 RTSP(live555)가 호스트에 들어오는 UDP 포트를 여는 것을 막는다(FW — Windows 방화벽 창
/// "이 앱의 일부 기능이 차단됨": 관리자로 실행해도 뜨고, '취소' 를 누르면 영구 차단 규칙이 생긴다).
/// <para><b>원인(실측)</b>:
/// <list type="number">
/// <item><c>:rtsp-tcp</c> 로 잘 재생되는 스트림도 열 때마다 live555 가 UDP 소켓을 <c>0.0.0.0</c> 에 잠깐 묶는다 —
/// RTP · RTCP 그룹소켓(<c>0.0.0.0:0</c>, 영상은 TCP 로 오므로 쓰지 않는다)과 자기 IP 알아내기 멀티캐스트 시험(<c>0.0.0.0:15947</c>).
/// 몇 ms 만에 닫혀 netstat 표본에는 안 잡히지만(FW-002 표본 0개) bind 를 가로채면 스트림마다 보인다 — 이 bind 가 창을 띄운다.
/// 그래서 하네스 작업 폴더 · 시험 bin 처럼 exe 경로가 새로울 때마다 창이 떴다.</item>
/// <item>서버가 TCP SETUP 을 461 로 거절하면 live555 가 UDP 로 다시 SETUP 하며 RTP · RTCP 를 <c>0.0.0.0</c> 에 묶어 재생 내내 연다
/// (FW-003 고치기 전: <c>UDP 0.0.0.0:54010</c> · <c>:54011</c>). 이 전환은 플러그인 안에 박혀 있어 LibVLC 옵션으로 끌 수 없다.</item>
/// </list></para>
/// <para><b>고침</b>: <c>liblive555_plugin.dll</c> 을 LibVLC 보다 먼저 올려 그 가져오기 표(IAT)의 <c>ws2_32!bind</c> 칸을 이 클래스의
/// 함수로 바꾼다. 루프백 밖 주소로의 <b>UDP</b> bind 는 같은 포트의 루프백(127.0.0.1 · ::1)으로 바꿔 묶고(그룹소켓은 그대로 만들어진다),
/// 15947(멀티캐스트 시험)만 <c>WSAEACCES</c> 로 거절한다(live555 는 호스트 이름 조회로 넘어간다 — 루프백 소켓으로는 멀티캐스트를 되받지 못해 기다린다).
/// 461 뒤 UDP 재생은 루프백 소켓에 밖의 패킷이 오지 않아 실패로 끝난다(방화벽 창보다 낫다).
/// TCP bind(연결 전 <c>0.0.0.0:0</c> — 대기가 아니라 창이 안 뜬다)와 루프백은 그대로 둔다.
/// UDP 로만 되는 카메라용으로 이 장치를 끄는 설정: <c>CameraPopupHostOptions.AllowRtspUdpFallback</c>(기본 끔 — 켜면 창이 다시 뜰 수 있다).
/// 무엇이 실패해도 던지지 않는다(그때는 옛 동작).</para>
/// </summary>
internal static unsafe class UdpBindGuard
{
    private const int SocketError = -1;
    private const int WsaEAccess = 10013;
    private const ushort AfInet = 2;
    private const ushort AfInet6 = 23;

    /// <summary>live555 <c>ourIPAddress()</c> 가 자기 주소를 알아내려 멀티캐스트를 되받는 포트(<c>GroupsockHelper.cpp</c> testPort).</summary>
    private const int OurIpProbePort = 15947;

    private static delegate* unmanaged<nint, byte*, int, int> _bind;
    private static HostLog? _log;
    private static int _movedLogged;

    /// <summary>바꿨으면 true. <c>Core.Initialize()</c> 뒤 · 첫 RTSP 재생 전에 한 번만 부른다.</summary>
    public static bool TryApply(HostLog log)
    {
        try
        {
            if (IntPtr.Size != 8) return false;
            var core = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .FirstOrDefault(m => string.Equals(m.ModuleName, "libvlccore.dll", StringComparison.OrdinalIgnoreCase));
            IntPtr ws2 = LoadLibraryW("ws2_32.dll");
            if (core is null || ws2 == IntPtr.Zero) return false;
            var plugin = Path.Combine(Path.GetDirectoryName(core.FileName)!, "plugins", "access", "liblive555_plugin.dll");
            // LibVLC 가 나중에 같은 경로를 올리면 이미 올라온 이 모듈(바꾼 IAT 그대로)을 다시 쓴다.
            IntPtr module = LoadLibraryExW(plugin, IntPtr.Zero, LoadWithAlteredSearchPath);
            if (module == IntPtr.Zero)
            {
                log.Warn($"udp bind guard skipped: cannot load {plugin} (error {Marshal.GetLastWin32Error()})");
                return false;
            }
            _bind = (delegate* unmanaged<nint, byte*, int, int>)GetProcAddress(ws2, "bind");
            if (_bind == null) return false;
            _log = log;
            int patched = PatchImports((byte*)module, (nint)_bind, (nint)(delegate* unmanaged<nint, byte*, int, int>)&Bind);
            if (patched == 0)
            {
                log.Warn("udp bind guard skipped: bind import not found in liblive555_plugin (unknown build) — RTSP may open inbound UDP ports");
                return false;
            }
            log.Info($"udp bind guard applied to liblive555_plugin ({patched} slot) — UDP binds stay on loopback (no inbound ports)");
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warn($"udp bind guard failed: {ex.GetType().Name} {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 이 주소에 묶으면 밖에서 들어올 수 있는가(순수) — IPv4 · IPv6 이면서 루프백(127/8 · ::1 · ::ffff:127/104)이 아닌 주소.
    /// IP 가 아닌 주소족은 false.
    /// </summary>
    public static bool OpensInboundPort(ReadOnlySpan<byte> sockaddr)
    {
        if (sockaddr.Length < 2) return false;
        ushort family = (ushort)(sockaddr[0] | sockaddr[1] << 8);
        if (family == AfInet) return sockaddr.Length >= 8 && sockaddr[4] != 127;
        if (family != AfInet6 || sockaddr.Length < 24) return false;
        var address = sockaddr.Slice(8, 16);
        bool v6Loopback = address[..15].IndexOfAnyExcept((byte)0) < 0 && address[15] == 1;
        bool mappedV4Loopback = address[..10].IndexOfAnyExcept((byte)0) < 0 && address[10] == 0xFF && address[11] == 0xFF && address[12] == 127;
        return !v6Loopback && !mappedV4Loopback;
    }

    /// <summary>IP 주소 자리를 루프백(127.0.0.1 · ::1)으로 바꾼다(포트 · 나머지는 그대로, 순수).</summary>
    public static void ToLoopback(Span<byte> sockaddr)
    {
        if ((ushort)(sockaddr[0] | sockaddr[1] << 8) == AfInet)
        {
            sockaddr[4] = 127; sockaddr[5] = 0; sockaddr[6] = 0; sockaddr[7] = 1;
            return;
        }
        sockaddr.Slice(8, 16).Clear();
        sockaddr[23] = 1;
    }

    [UnmanagedCallersOnly]
    private static int Bind(nint socket, byte* name, int length)
    {
        try
        {
            if (name != null && length > 0 && OpensInboundPort(new ReadOnlySpan<byte>(name, length)) && IsDatagram(socket))
            {
                if ((name[2] << 8 | name[3]) == OurIpProbePort)
                {
                    WSASetLastError(WsaEAccess);
                    return SocketError;
                }
                // 같은 주소족의 루프백에 묶는다(호출자 메모리는 건드리지 않는다). 거절하면 live555 가 RTP · RTCP 짝 포트를
                // 찾느라 소켓을 끝없이 새로 만든다(실측: 수천 개) — 그래서 RTCP(짝 +1 고정 포트)도 거절하지 않고 옮긴다.
                byte* moved = stackalloc byte[length];
                var copy = new Span<byte>(moved, length);
                new ReadOnlySpan<byte>(name, length).CopyTo(copy);
                ToLoopback(copy);
                if (Interlocked.Exchange(ref _movedLogged, 1) == 0)
                    _log?.Info("udp bind moved to loopback (live555 RTP/RTCP sockets — media comes over RTSP/TCP)");
                return _bind(socket, moved, length);
            }
        }
        catch
        {
            // 네이티브 경계를 넘기지 않는다 — 판단을 못 하면 원래 bind 로.
        }
        return _bind(socket, name, length);
    }

    private static bool IsDatagram(nint socket)
    {
        int type = 0, size = sizeof(int);
        return getsockopt(socket, 0xFFFF /* SOL_SOCKET */, 0x1008 /* SO_TYPE */, &type, &size) == 0 && type == 2 /* SOCK_DGRAM */;
    }

    /// <summary>PE32+ 가져오기 표에서 <paramref name="target"/> 을 가리키는 칸을 모두 <paramref name="replacement"/> 로 바꾼다.</summary>
    private static int PatchImports(byte* image, nint target, nint replacement)
    {
        int pe = *(int*)(image + 0x3C);
        if (*(uint*)(image + pe) != 0x00004550 || *(ushort*)(image + pe + 24) != 0x20B) return 0;
        uint importRva = *(uint*)(image + pe + 24 + 120);   // 선택 헤더 데이터 디렉터리 1 = 가져오기
        if (importRva == 0) return 0;
        int count = 0;
        for (byte* descriptor = image + importRva; *(uint*)(descriptor + 12) != 0; descriptor += 20)
        {
            for (nint* slot = (nint*)(image + *(uint*)(descriptor + 16)); *slot != 0; slot++)
            {
                if (*slot != target || !VirtualProtect((nint)slot, (nuint)sizeof(nint), PageReadWrite, out uint old)) continue;
                *slot = replacement;
                VirtualProtect((nint)slot, (nuint)sizeof(nint), old, out _);
                count++;
            }
        }
        return count;
    }

    private const uint LoadWithAlteredSearchPath = 0x00000008;
    private const uint PageReadWrite = 0x04;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string fileName, IntPtr file, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(nint address, nuint size, uint newProtect, out uint oldProtect);

    [DllImport("ws2_32.dll")]
    private static extern int getsockopt(nint socket, int level, int optionName, int* optionValue, int* optionLength);

    [DllImport("ws2_32.dll")]
    private static extern void WSASetLastError(int error);
}

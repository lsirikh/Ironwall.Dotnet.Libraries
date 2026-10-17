using System.Runtime.InteropServices;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// LibVLC 3.0 의 Windows 대기 구현을 진짜 <c>WaitOnAddress</c> 로 잇는다(T-09 T7 — 타일 60개 CPU 57~72% 의 원인).
/// <para><b>원인(실측)</b>: libvlccore 3.0.21 은 <c>WaitOnAddress</c> · <c>WakeByAddressAll</c> · <c>WakeByAddressSingle</c> 을
/// <c>kernel32.dll</c> 에서 찾는데, 이 셋은 kernel32 가 내보내지 않는다(kernelbase · api-ms-win-core-synch-l1-2-0 에만 있다).
/// 그래서 어느 Windows 에서나 대체 구현(임계 구역 + 조건 변수 <b>32칸</b>)으로 떨어진다. 대체 구현의 "깨우기"는
/// 같은 칸에 걸린 <b>모든</b> 대기 스레드를 깨운다 — 플레이어가 수십 개면 스레드 수백 개가 서로를 헛깨워
/// 코어 20여 개를 태운다(타일 6개 0.5코어 → 30개 26코어, 뜨거운 스레드는 전부
/// <c>RtlSleepConditionVariableCS</c> · <c>RtlEnterCriticalSection</c> · <c>RtlWakeAllConditionVariable</c> 안).</para>
/// <para><b>고침</b>: libvlccore 를 올린 직후 · LibVLC 인스턴스를 만들기 전(아직 LibVLC 스레드가 하나도 없을 때)에
/// 그 함수 포인터 세 칸을 kernelbase 의 진짜 함수로 바꾼다. 서명은 같다(libvlccore 가 원래 부르려던 API 다).</para>
/// <para><b>안전장치</b>: 칸을 주소로 찍지 않고 <b>모양</b>으로 찾는다 — 쓰기 가능한 구역에서
/// [코드 포인터 A, A, 코드 포인터 B(≠A), 그리고 바로 뒤 세 칸이 kernel32 의
/// WakeAllConditionVariable · SleepConditionVariableCS · InitializeConditionVariable] 가 <b>정확히 한 곳</b>일 때만 바꾼다.
/// 하나도 없거나 둘 이상이면(다른 판의 libvlccore) 아무것도 바꾸지 않고 옛 동작 그대로 간다. 무엇이 실패해도 던지지 않는다.
/// 끄기: 환경 변수 <c>IRONWALL_CAMHOST_NO_WAITPATCH=1</c>.</para>
/// </summary>
internal static class LibVlcWaitPatch
{
    public const string DisableVariable = "IRONWALL_CAMHOST_NO_WAITPATCH";

    private const uint SectionExecute = 0x20000000;
    private const uint SectionWrite = 0x80000000;

    /// <summary>바꿨으면 true. <c>Core.Initialize()</c> 뒤 · <c>new LibVLC(...)</c> 앞에서 한 번만 부른다.</summary>
    public static bool TryApply(HostLog log)
    {
        try
        {
            if (Environment.GetEnvironmentVariable(DisableVariable) == "1")
            {
                log.Info("libvlc wait patch disabled by environment");
                return false;
            }
            if (IntPtr.Size != 8) return false;

            IntPtr core = GetModuleHandleW("libvlccore.dll");
            IntPtr kernel32 = GetModuleHandleW("kernel32.dll");
            IntPtr kernelBase = GetModuleHandleW("kernelbase.dll");
            if (core == IntPtr.Zero || kernel32 == IntPtr.Zero || kernelBase == IntPtr.Zero) return false;

            IntPtr wait = GetProcAddress(kernelBase, "WaitOnAddress");
            IntPtr wakeAll = GetProcAddress(kernelBase, "WakeByAddressAll");
            IntPtr wakeSingle = GetProcAddress(kernelBase, "WakeByAddressSingle");
            if (wait == IntPtr.Zero || wakeAll == IntPtr.Zero || wakeSingle == IntPtr.Zero) return false;

            var conditionApis = new HashSet<long>
            {
                GetProcAddress(kernel32, "WakeAllConditionVariable").ToInt64(),
                GetProcAddress(kernel32, "SleepConditionVariableCS").ToInt64(),
                GetProcAddress(kernel32, "InitializeConditionVariable").ToInt64(),
            };
            if (conditionApis.Count != 3 || conditionApis.Contains(0)) return false;

            var sections = ReadSections(core);
            var code = sections.Where(s => (s.Characteristics & SectionExecute) != 0).ToArray();
            bool InCode(long p) => code.Any(s => p >= s.Start && p < s.End);

            IntPtr found = IntPtr.Zero;
            int matches = 0;
            foreach (var section in sections.Where(s => (s.Characteristics & SectionWrite) != 0 && (s.Characteristics & SectionExecute) == 0))
            {
                for (long address = section.Start; address + 6 * 8 <= section.End; address += 8)
                {
                    long a = Marshal.ReadInt64(new IntPtr(address));
                    if (a == 0 || !InCode(a)) continue;
                    long b = Marshal.ReadInt64(new IntPtr(address + 8));
                    long c = Marshal.ReadInt64(new IntPtr(address + 16));
                    if (a != b || c == a || !InCode(c)) continue;
                    var tail = new HashSet<long>
                    {
                        Marshal.ReadInt64(new IntPtr(address + 24)),
                        Marshal.ReadInt64(new IntPtr(address + 32)),
                        Marshal.ReadInt64(new IntPtr(address + 40)),
                    };
                    if (!tail.SetEquals(conditionApis)) continue;
                    matches++;
                    found = new IntPtr(address);
                }
            }
            if (matches != 1)
            {
                log.Warn($"libvlc wait patch skipped: signature matches={matches} (unknown libvlccore build) — fallback waits stay");
                return false;
            }

            // [WakeByAddressAll_, WakeByAddressSingle_] 는 어느 칸이 어느 것인지 모른다(둘 다 같은 대체 함수를 가리킨다) —
            // 둘 다 "전부 깨우기"로 둔다. 하나만 깨워도 되는 곳에서 전부 깨우는 것은 옛 대체 구현도 하던 일이고(헛깨움 허용 코드),
            // 이제는 같은 주소를 기다리는 스레드만 깨운다.
            Marshal.WriteInt64(found, 0, wakeAll.ToInt64());
            Marshal.WriteInt64(found, 8, wakeAll.ToInt64());
            Marshal.WriteInt64(found, 16, wait.ToInt64());
            log.Info($"libvlc wait patch applied at libvlccore+0x{found.ToInt64() - core.ToInt64():X} (WaitOnAddress instead of the 32-bucket fallback)");
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warn($"libvlc wait patch failed: {ex.GetType().Name} {ex.Message} — fallback waits stay");
            return false;
        }
    }

    private static List<Section> ReadSections(IntPtr module)
    {
        long b = module.ToInt64();
        int pe = Marshal.ReadInt32(module, 0x3C);
        if (Marshal.ReadInt32(module, pe) != 0x00004550) throw new InvalidDataException("not a PE image");
        int count = (ushort)Marshal.ReadInt16(module, pe + 6);
        int optionalSize = (ushort)Marshal.ReadInt16(module, pe + 20);
        var list = new List<Section>(count);
        for (int i = 0; i < count; i++)
        {
            int header = pe + 24 + optionalSize + 40 * i;
            uint virtualSize = unchecked((uint)Marshal.ReadInt32(module, header + 8));
            uint virtualAddress = unchecked((uint)Marshal.ReadInt32(module, header + 12));
            uint characteristics = unchecked((uint)Marshal.ReadInt32(module, header + 36));
            list.Add(new Section(b + virtualAddress, b + virtualAddress + (virtualSize & ~7u), characteristics));
        }
        return list;
    }

    private readonly record struct Section(long Start, long End, uint Characteristics);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);
}

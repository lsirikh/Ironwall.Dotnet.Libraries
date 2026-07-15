using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.SystemResources.Native;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : PDH/kernel32 기반 실제 리소스 프로브
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// PDH(CPU/GPU) + kernel32(RAM)로 원시 사용률을 취득하는 <see cref="IResourceProbe"/> 실구현.
/// English 카운터(로케일 독립) 사용. 각 카운터 add 실패는 해당 지표만 미가용 처리(나머지 독립 동작).
/// 스레드 안전 아님 — 소비자(UI 스레드 DispatcherTimer)가 직렬 호출하는 전제(모니터가 재진입 가드).
/// </summary>
internal sealed class PdhResourceProbe : IResourceProbe
{
    // CPU 사용률 = "바쁜 시간 %"(0..100).
    // ⚠ % Processor Utility 금지: 주파수(Turbo) 배율(% Processor Performance)을 포함해 base 주파수 대비
    //    실제 작업량을 재므로, 고주파 머신에서 busy% × 1.4~1.7로 부풀려져 중부하에서 100% 고정 + 작업관리자
    //    체감치와 크게 벌어짐(실측: Performance 145% 머신에서 busy 67% → Utility 100%). → % Processor Time 사용.
    private const string CpuTimePath = @"\Processor Information(_Total)\% Processor Time";   // Processor Information = >64 논리코어 지원
    private const string CpuTimeFallbackPath = @"\Processor(_Total)\% Processor Time";       // 폴백(클래식 Processor, ≤64코어 그룹)
    private const string GpuPath = @"\GPU Engine(*)\Utilization Percentage";

    private readonly ILogService? _log;

    private IntPtr _query = IntPtr.Zero;
    private IntPtr _cpuCounter = IntPtr.Zero;
    private IntPtr _gpuCounter = IntPtr.Zero;
    private bool _cpuAdded;
    private bool _gpuAdded;
    private bool _opened;

    public PdhResourceProbe(ILogService? log = null) => _log = log;

    public void Open()
    {
        if (_opened) return;

        uint st = PdhInterop.PdhOpenQueryW(null, IntPtr.Zero, out _query);
        if (st != PdhInterop.ERROR_SUCCESS || _query == IntPtr.Zero)
            throw new InvalidOperationException($"PdhOpenQuery 실패 (0x{st:X8})");

        // CPU: % Processor Time(바쁜 시간 %, 주파수 무관) → 실패 시 클래식 Processor 폴백.
        uint c = PdhInterop.PdhAddEnglishCounterW(_query, CpuTimePath, IntPtr.Zero, out _cpuCounter);
        if (c != PdhInterop.ERROR_SUCCESS)
        {
            _log?.Warning($"[SysRes] '{CpuTimePath}' 미가용(0x{c:X8}) → 클래식 Processor 폴백");
            c = PdhInterop.PdhAddEnglishCounterW(_query, CpuTimeFallbackPath, IntPtr.Zero, out _cpuCounter);
        }
        _cpuAdded = c == PdhInterop.ERROR_SUCCESS;
        if (!_cpuAdded) _log?.Warning($"[SysRes] CPU 카운터 추가 실패(0x{c:X8})");

        // GPU: 와일드카드(없는 환경=NO_OBJECT → GPU 미가용).
        uint g = PdhInterop.PdhAddEnglishCounterW(_query, GpuPath, IntPtr.Zero, out _gpuCounter);
        _gpuAdded = g == PdhInterop.ERROR_SUCCESS;
        if (!_gpuAdded) _log?.Info($"[SysRes] GPU Engine 카운터 미가용(0x{g:X8}) → GPU 표시 생략");

        // 프라이밍 collect — rate 카운터는 2회 collect 사이 간격이 있어야 유효.
        PdhInterop.PdhCollectQueryData(_query);
        _opened = true;
    }

    public ProbeReading Sample()
    {
        if (!_opened) Open();

        // RAM은 PDH와 독립(항상 시도).
        bool ramOk = MemoryInterop.TryRead(out double ram, out ulong ramTotal, out ulong ramUsed);

        double cpu = 0; bool cpuOk = false;
        double gpu = 0; bool gpuOk = false; int adapters = 0;

        uint st = PdhInterop.PdhCollectQueryData(_query);
        if (st == PdhInterop.ERROR_SUCCESS)
        {
            if (_cpuAdded) cpuOk = TryReadSingle(_cpuCounter, out cpu);
            if (_gpuAdded)
            {
                var agg = ReadGpuArray();
                gpu = agg.Percent; gpuOk = agg.Available; adapters = agg.AdapterCount;
            }
        }
        else
        {
            // collect 자체 실패(카운터 손상/access denied 등) — CPU/GPU 미가용, RAM은 위에서 독립 취득.
            _log?.Warning($"[SysRes] PdhCollectQueryData 실패(0x{st:X8})");
        }

        return new ProbeReading(cpu, cpuOk, gpu, gpuOk, adapters, ram, ramOk, ramTotal, ramUsed);
    }

    private static bool TryReadSingle(IntPtr counter, out double value)
    {
        value = 0;
        uint st = PdhInterop.PdhGetFormattedCounterValue(counter, PdhInterop.PDH_FMT_DOUBLE, out _, out var v);
        if (st != PdhInterop.ERROR_SUCCESS) return false;
        if (!PdhInterop.IsValidData(v.CStatus)) return false;   // rate 워밍업/무효 데이터
        value = v.doubleValue;
        return true;
    }

    private GpuAggregate ReadGpuArray()
    {
        // 2-call 크기 관용구: 먼저 필요 버퍼 크기 질의.
        uint bufSize = 0, itemCount = 0;
        uint st = PdhInterop.PdhGetFormattedCounterArrayW(
            _gpuCounter, PdhInterop.PDH_FMT_DOUBLE, ref bufSize, ref itemCount, IntPtr.Zero);
        if (st != PdhInterop.PDH_MORE_DATA || bufSize == 0)
            return new GpuAggregate(0, false, 0);   // 인스턴스 없음/무효

        int itemSize = Marshal.SizeOf<PdhInterop.PDH_FMT_COUNTERVALUE_ITEM_DOUBLE>();

        // 인스턴스 수가 호출 사이 변할 수 있어 PDH_MORE_DATA면 재시도(최대 3회).
        for (int attempt = 0; attempt < 3; attempt++)
        {
            IntPtr buf = Marshal.AllocHGlobal(checked((int)bufSize));
            try
            {
                st = PdhInterop.PdhGetFormattedCounterArrayW(
                    _gpuCounter, PdhInterop.PDH_FMT_DOUBLE, ref bufSize, ref itemCount, buf);

                if (st == PdhInterop.PDH_MORE_DATA)
                    continue;   // bufSize 갱신됨 → 더 큰 버퍼로 재시도
                if (st != PdhInterop.ERROR_SUCCESS)
                    return new GpuAggregate(0, false, 0);

                var list = new List<KeyValuePair<string, double>>(checked((int)itemCount));
                for (int i = 0; i < itemCount; i++)
                {
                    IntPtr p = IntPtr.Add(buf, i * itemSize);
                    var item = Marshal.PtrToStructure<PdhInterop.PDH_FMT_COUNTERVALUE_ITEM_DOUBLE>(p);
                    if (!PdhInterop.IsValidData(item.CStatus)) continue;
                    string name = item.szName == IntPtr.Zero
                        ? string.Empty
                        : (Marshal.PtrToStringUni(item.szName) ?? string.Empty);
                    list.Add(new KeyValuePair<string, double>(name, item.doubleValue));
                }
                return GpuAggregator.Aggregate(list);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        return new GpuAggregate(0, false, 0);
    }

    public void Close()
    {
        if (_query != IntPtr.Zero)
        {
            try { PdhInterop.PdhCloseQuery(_query); } catch { /* 종료 경로 예외 무시 */ }
            _query = IntPtr.Zero;
        }
        _cpuCounter = IntPtr.Zero;
        _gpuCounter = IntPtr.Zero;
        _cpuAdded = false;
        _gpuAdded = false;
        _opened = false;
    }

    public void Dispose() => Close();
}

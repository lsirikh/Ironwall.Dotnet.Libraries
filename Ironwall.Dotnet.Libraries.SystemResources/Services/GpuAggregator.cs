using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : GPU Engine 와일드카드 인스턴스 → 단일 사용률 집계(순수 함수)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// <c>\GPU Engine(*)\Utilization Percentage</c> 인스턴스들을 하나의 GPU 사용률로 집계한다(순수·테스트가능).
/// <para>
/// 인스턴스명 예: <c>pid_1292_luid_0x00000000_0x00011059_phys_0_eng_0_engtype_3d</c>.
/// 집계 규칙: <b>물리 엔진 키=(luid,phys,eng)</b>로 pid들의 사용률을 <b>합산</b>(같은 엔진을 시분할 공유하므로),
/// 엔진당 100 cap → 전체는 모든 엔진 합산값의 <b>최대</b>(가장 바쁜 물리 엔진 = headline GPU%).
/// 병렬 엔진(3d/copy/decode 동시)은 합산하지 않고 max를 취해 과대표시를 방지한다. 멀티 GPU는
/// 어댑터 간 max로 "가장 바쁜 GPU"를 표시(블렌딩 방지).
/// </para>
/// </summary>
public static class GpuAggregator
{
    // luid(0xHIGH_0xLOW) / phys / eng 추출. engtype·pid는 무시.
    private static readonly Regex _rx = new(
        @"luid_(0x[0-9a-fA-F]+_0x[0-9a-fA-F]+)_phys_(\d+)_eng_(\d+)_",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// GPU 인스턴스 샘플을 단일 사용률로 집계한다.
    /// </summary>
    /// <param name="samples">(인스턴스명, 사용률%) 시퀀스.</param>
    public static GpuAggregate Aggregate(IEnumerable<KeyValuePair<string, double>> samples)
    {
        if (samples is null) return new GpuAggregate(0, false, 0);

        var engineSums = new Dictionary<string, double>();
        var adapters = new HashSet<string>();
        bool any = false;

        foreach (var kv in samples)
        {
            double v = kv.Value;
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) continue;

            var m = _rx.Match(kv.Key ?? string.Empty);
            if (!m.Success) continue;   // luid 없는 인스턴스(_Total 등) skip

            any = true;
            string luid = m.Groups[1].Value.ToLowerInvariant();
            adapters.Add(luid);

            // 물리 엔진 단위로 pid 사용률 합산
            string engineKey = luid + "|" + m.Groups[2].Value + "|" + m.Groups[3].Value;
            engineSums.TryGetValue(engineKey, out double cur);
            engineSums[engineKey] = cur + v;
        }

        if (!any) return new GpuAggregate(0, false, 0);

        // 엔진당 100 cap 후, 가장 바쁜 엔진 = 전체 GPU 사용률
        double max = 0;
        foreach (var sum in engineSums.Values)
        {
            double capped = Math.Min(100, sum);
            if (capped > max) max = capped;
        }

        return new GpuAggregate(Math.Clamp(max, 0, 100), true, adapters.Count);
    }
}

/// <summary>GPU 집계 결과.</summary>
/// <param name="Percent">집계 사용률 %(0..100).</param>
/// <param name="Available">유효 인스턴스 존재 여부.</param>
/// <param name="AdapterCount">감지된 어댑터(LUID) 수.</param>
public readonly record struct GpuAggregate(double Percent, bool Available, int AdapterCount);

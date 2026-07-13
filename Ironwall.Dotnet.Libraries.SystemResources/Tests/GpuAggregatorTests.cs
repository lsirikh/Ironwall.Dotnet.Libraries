using System;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.SystemResources.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.SystemResources.Tests;

/// <summary>GPU 와일드카드 집계(순수 함수) 검증.</summary>
public class GpuAggregatorTests
{
    private static KeyValuePair<string, double> S(string name, double v) => new(name, v);

    [Fact]
    public void should_return_unavailable_when_no_instances()
    {
        var r = GpuAggregator.Aggregate(new List<KeyValuePair<string, double>>());

        Assert.False(r.Available);
        Assert.Equal(0, r.Percent);
        Assert.Equal(0, r.AdapterCount);
    }

    [Fact]
    public void should_skip_total_and_invalid_instances()
    {
        var samples = new[]
        {
            S("_Total", 99),                                                   // luid 없음 → skip
            S("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3d", double.NaN),       // NaN → skip
            S("pid_2_luid_0x0_0x1_phys_0_eng_0_engtype_3d", 40),              // 유효
        };

        var r = GpuAggregator.Aggregate(samples);

        Assert.True(r.Available);
        Assert.Equal(40, r.Percent);
        Assert.Equal(1, r.AdapterCount);
    }

    [Fact]
    public void should_cap_at_100_when_same_engine_pids_sum_exceeds()
    {
        // 같은 물리 엔진(luid,phys,eng)에 두 pid: 70 + 50 = 120 → cap 100
        var samples = new[]
        {
            S("pid_1_luid_0x0_0x11059_phys_0_eng_0_engtype_3d", 70),
            S("pid_2_luid_0x0_0x11059_phys_0_eng_0_engtype_3d", 50),
        };

        var r = GpuAggregator.Aggregate(samples);

        Assert.Equal(100, r.Percent);
    }

    [Fact]
    public void should_take_max_of_parallel_engines_not_sum()
    {
        // 병렬 물리 엔진(eng_0 3d=30, eng_1 copy=20)은 합산 아님 → max 30
        var samples = new[]
        {
            S("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3d", 30),
            S("pid_1_luid_0x0_0x1_phys_0_eng_1_engtype_copy", 20),
        };

        var r = GpuAggregator.Aggregate(samples);

        Assert.Equal(30, r.Percent);
    }

    [Fact]
    public void should_return_busiest_adapter_when_multi_gpu()
    {
        // iGPU(luid A, 10%) + dGPU(luid B, 80%) → busiest 80, adapters=2 (블렌딩 없음)
        var samples = new[]
        {
            S("pid_1_luid_0x0_0xAAAA_phys_0_eng_0_engtype_3d", 10),
            S("pid_1_luid_0x0_0xBBBB_phys_0_eng_0_engtype_3d", 80),
        };

        var r = GpuAggregator.Aggregate(samples);

        Assert.Equal(80, r.Percent);
        Assert.Equal(2, r.AdapterCount);
    }
}

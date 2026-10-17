using System;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.SystemResources.Models;
using Ironwall.Dotnet.Libraries.SystemResources.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.SystemResources.Tests;

/// <summary>모니터 순수 로직(워밍업·clamp·히스테리시스·fail-safe·생명주기) 검증 — FakeProbe 목킹.</summary>
public class SystemResourceMonitorTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Local);
        public DateTime UtcNow { get; set; } = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeProbe : IResourceProbe
    {
        public int OpenCount;
        public int CloseCount;
        public int SampleCount;
        public bool ThrowOnSample;
        public ProbeReading Next = new(50, true, 30, true, 1, 40, true, 16_000_000_000UL, 6_400_000_000UL);

        public void Open() => OpenCount++;
        public void Close() => CloseCount++;
        public void Dispose() => CloseCount++;

        public ProbeReading Sample()
        {
            SampleCount++;
            if (ThrowOnSample) throw new InvalidOperationException("boom");
            return Next;
        }
    }

    private static SystemResourceMonitor New(FakeProbe p) => new(p, new FakeClock());

    [Fact]
    public void should_be_idempotent_on_double_start()
    {
        var p = new FakeProbe();
        var m = New(p);

        m.Start();
        m.Start();

        Assert.Equal(1, p.OpenCount);
    }

    [Fact]
    public void should_publish_valid_after_first_sample()
    {
        var p = new FakeProbe();
        var m = New(p);

        var s = m.Sample();

        Assert.True(s.CpuAvailable);
        Assert.Equal(50, s.CpuPercent);
        Assert.Equal(30, s.GpuPercent);
        Assert.Equal(40, s.RamPercent);
        Assert.Same(s, m.Current);
        Assert.Equal(1, p.OpenCount);   // 지연 Start
    }

    [Fact]
    public void should_clamp_cpu_to_100_when_turbo_exceeds()
    {
        var p = new FakeProbe { Next = new(137, true, 0, false, 0, 10, true, 1, 0) };
        var m = New(p);

        var s = m.Sample();

        Assert.Equal(100, s.CpuPercent);
        Assert.Equal(ResourceLevel.Critical, s.CpuLevel);
    }

    [Fact]
    public void should_emit_empty_when_probe_throws_and_not_propagate()
    {
        var p = new FakeProbe { ThrowOnSample = true };
        var m = New(p);

        var s = m.Sample();   // 예외를 던지면 안 됨

        Assert.False(s.CpuAvailable);
        Assert.False(s.RamAvailable);
        Assert.False(s.GpuAvailable);
    }

    [Fact]
    public void should_keep_ticking_after_failure()
    {
        var p = new FakeProbe { ThrowOnSample = true };
        var m = New(p);

        m.Sample();               // 실패
        p.ThrowOnSample = false;
        var s = m.Sample();       // 복귀

        Assert.True(s.CpuAvailable);
    }

    [Fact]
    public void should_mark_gpu_unavailable_but_keep_cpu_and_ram()
    {
        var p = new FakeProbe { Next = new(50, true, 0, false, 0, 40, true, 1, 0) };
        var m = New(p);

        var s = m.Sample();

        Assert.True(s.CpuAvailable);
        Assert.False(s.GpuAvailable);
        Assert.True(s.RamAvailable);
    }

    [Fact]
    public void should_raise_snapshot_updated_event()
    {
        var p = new FakeProbe();
        var m = New(p);
        SystemResourceSnapshot? got = null;
        m.SnapshotUpdated += (_, s) => got = s;

        var s = m.Sample();

        Assert.NotNull(got);
        Assert.Same(s, got);
    }

    [Fact]
    public void should_apply_hysteresis_level_across_samples()
    {
        var p = new FakeProbe { Next = new(61, true, 0, false, 0, 10, true, 1, 0) };
        var m = New(p);

        Assert.Equal(ResourceLevel.Normal, m.Sample().CpuLevel);   // 61 < 62

        p.Next = new(63, true, 0, false, 0, 10, true, 1, 0);
        Assert.Equal(ResourceLevel.Warning, m.Sample().CpuLevel);  // 63 >= 62

        p.Next = new(59, true, 0, false, 0, 10, true, 1, 0);
        Assert.Equal(ResourceLevel.Warning, m.Sample().CpuLevel);  // 59 > 57 → 유지(깜빡임 없음)
    }

    [Fact]
    public void should_reprime_after_stop_start()
    {
        var p = new FakeProbe();
        var m = New(p);

        m.Start();
        m.Stop();
        m.Start();

        Assert.Equal(2, p.OpenCount);
        Assert.True(p.CloseCount >= 1);
    }

    [Fact]
    public void should_return_current_and_not_throw_when_disposed()
    {
        var p = new FakeProbe();
        var m = New(p);
        m.Sample();

        m.Dispose();
        var s = m.Sample();   // disposed → Current 반환, 예외 없음

        Assert.NotNull(s);
    }

    [Fact]
    public void should_expose_empty_before_first_sample()
    {
        var p = new FakeProbe();
        var m = New(p);

        Assert.Same(SystemResourceSnapshot.Empty, m.Current);
        Assert.False(m.Current.RamAvailable);
    }
}

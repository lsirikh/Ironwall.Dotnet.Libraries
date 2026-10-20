using Ironwall.Dotnet.Libraries.Api.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 부팅 판본 확보가 한 번 실패해도 배경에서 다시 묻는다(2026-10-20 실기: 부팅 순간 TLS 한 번 실패 → 그 실행 내내 폴백 V6_3 →
/// v8 서버에 옛 질의 422 → 탐지 장비 "서버에 없음" → 카메라 이벤트 창이 뜨지 않음).
/// </summary>
public class ServerContractBootRetryTests
{
    private sealed class FlakyProbe : IServerContractProbe
    {
        private readonly int _failures;
        public FlakyProbe(int failures) => _failures = failures;
        public int Calls;
        public EnumServerContract Contract { get; private set; } = EnumServerContract.V6_3;
        public string? RawVersion { get; private set; }
        public bool IsResolved { get; private set; }

        public Task<bool> ResolveAsync(CancellationToken token = default)
        {
            if (Interlocked.Increment(ref Calls) > _failures) { IsResolved = true; RawVersion = "8.0.11"; Contract = EnumServerContract.V8_0; }
            return Task.FromResult(IsResolved);
        }

        public Task<bool> RefreshAsync(CancellationToken token = default) => ResolveAsync(token);
    }

    private static async Task<bool> WaitUntil(Func<bool> done, TimeSpan limit)
    {
        var sw = Stopwatch.StartNew();
        while (!done() && sw.Elapsed < limit) await Task.Delay(50);
        return done();
    }

    [Fact]
    public async Task should_retry_in_background_and_resolve_when_first_boot_attempt_fails()
    {
        var probe = new FlakyProbe(failures: 1);
        using var boot = new ServerContractBootService(null, new IServerContractProbe[] { probe }, budgetSeconds: 1);

        await boot.ExecuteAsync();
        Assert.False(probe.IsResolved);                                   // 부팅 시도는 실패 — 폴백 V6_3
        Assert.Equal(EnumServerContract.V6_3, probe.Contract);

        Assert.True(await WaitUntil(() => probe.IsResolved, TimeSpan.FromSeconds(8)), "첫 재시도(2초 뒤)에 확보돼야 한다");
        Assert.Equal(EnumServerContract.V8_0, probe.Contract);
        Assert.Equal(2, probe.Calls);
    }

    [Fact]
    public async Task should_not_retry_when_boot_attempt_succeeds()
    {
        var probe = new FlakyProbe(failures: 0);
        using var boot = new ServerContractBootService(null, new IServerContractProbe[] { probe }, budgetSeconds: 1);

        await boot.ExecuteAsync();
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.True(probe.IsResolved);
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task should_stop_retrying_when_app_stops()
    {
        var probe = new FlakyProbe(failures: int.MaxValue);
        var boot = new ServerContractBootService(null, new IServerContractProbe[] { probe }, budgetSeconds: 1);

        await boot.ExecuteAsync();
        await boot.StopAsync();
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.Equal(1, probe.Calls);                                     // 종료 뒤에는 다시 묻지 않는다
        boot.Dispose();
    }
}

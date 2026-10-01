using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;

/// <summary>ping 한 번 — 시험이 가짜로 바꾼다.</summary>
public interface IPingProbe
{
    /// <summary><paramref name="host"/> 에 ICMP echo 한 번(시간 제한 <paramref name="timeout"/>). 예외를 던지지 않고 실패 표본을 돌려준다.</summary>
    Task<PingSample> SendAsync(string host, TimeSpan timeout, CancellationToken token = default);
}

/// <summary>실제 ICMP ping(<see cref="Ping"/>) — 호출마다 새 인스턴스(동시 호출 안전).</summary>
public sealed class IcmpPingProbe : IPingProbe
{
    public async Task<PingSample> SendAsync(string host, TimeSpan timeout, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return new PingSample(false, 0);
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host.Trim(), timeout, buffer: null, options: null, token).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? new PingSample(true, reply.RoundtripTime) : new PingSample(false, 0);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            return new PingSample(false, 0);
        }
    }
}

/// <summary>
/// 제어기 통신 신호등(fence-wiring-editor FR-14 · NFR-06) — 창이 열린 동안만 <see cref="INTERVAL"/> 마다 ICMP ping(시간 제한 1초 · 백그라운드).
/// </summary>
/// <remarks>
/// <para><b>비행 하나</b> — 앞 ping 이 끝나기 전에는 다음을 보내지 않는다(느린 망에서 쌓이지 않게). <b>창이 닫히면 즉시 멈춘다</b>(<see cref="Stop"/>).</para>
/// <para>상태가 바뀔 때 <see cref="Changed"/>, 표본마다(<see cref="INTERVAL"/> 한 번) <see cref="Sampled"/> 를 올린다 — 평균 · 손실 글자는
/// 상태가 그대로여도 바뀐다. 둘 다 <b>작업 스레드에서</b> 올 수 있다(받는 쪽이 UI 로 넘긴다).
/// 실패 로그는 상태가 "응답 없음"으로 바뀔 때 한 줄뿐이다(폭주 금지).</para>
/// <para>UI 스레드를 쓰지 않는다 — 루프는 <see cref="Task.Run(Func{Task})"/> 위에서 돈다.</para>
/// </remarks>
public sealed class ControllerPingMonitor : IDisposable
{
    public static readonly TimeSpan INTERVAL = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(1);

    private readonly IPingProbe _probe;
    private readonly string _host;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly List<PingSample> _samples = new();
    private CancellationTokenSource? _cts;
    private int _inFlight;
    private SignalLevel _level = SignalLevel.Unknown;

    public ControllerPingMonitor(IPingProbe probe, string host, Action<string>? log = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _host = host ?? string.Empty;
        _log = log;
    }

    /// <summary>지금 상태.</summary>
    public SignalLevel Level { get { lock (_gate) return _level; } }

    /// <summary>최근 표본(오래된 것 → 최근, 최대 <see cref="SignalMath.WINDOW"/>).</summary>
    public IReadOnlyList<PingSample> Samples { get { lock (_gate) return _samples.ToArray(); } }

    /// <summary>돌고 있는가.</summary>
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    /// <summary>상태가 바뀌었다(작업 스레드에서 올 수 있다).</summary>
    public event EventHandler<SignalLevel>? Changed;

    /// <summary>표본 하나가 들어왔다(상태가 같아도 · 작업 스레드에서 올 수 있다) — 평균 · 손실 글자를 고친다.</summary>
    public event EventHandler<SignalLevel>? Sampled;

    /// <summary>시작 — 이미 돌면 그대로. 주소가 없으면 시작하지 않는다(모름).</summary>
    public void Start()
    {
        if (string.IsNullOrWhiteSpace(_host) || IsRunning) return;
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(() => LoopAsync(cts.Token));
    }

    /// <summary>멈춤 — 비행 중인 ping 도 취소한다.</summary>
    public void Stop()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null) return;
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
        cts.Dispose();
    }

    public void Dispose() => Stop();

    /// <summary>
    /// ping 한 번 — 비행 중이면 건너뛴다(<c>false</c>). 시험이 시계 없이 이 길로 부른다.
    /// </summary>
    public async Task<bool> PingOnceAsync(CancellationToken token = default)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return false;
        try
        {
            var sample = await _probe.SendAsync(_host, TIMEOUT, token).ConfigureAwait(false);
            SignalLevel next;
            bool changed;
            lock (_gate)
            {
                _samples.Add(sample);
                if (_samples.Count > SignalMath.WINDOW) _samples.RemoveAt(0);
                next = SignalMath.Classify(_samples);
                changed = next != _level;
                _level = next;
            }
            // 이벤트는 잠금 밖에서(재진입 교착 방지)
            if (changed)
            {
                if (next == SignalLevel.Down) _log?.Invoke($"[Wiring] 제어기 {_host} ping 응답 없음(연속 {SignalMath.DOWN_STREAK}회)");
                Changed?.Invoke(this, next);
            }
            Sampled?.Invoke(this, next);
            return true;
        }
        finally { Interlocked.Exchange(ref _inFlight, 0); }
    }

    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await PingOnceAsync(token).ConfigureAwait(false);
                await Task.Delay(INTERVAL, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Invoke($"[Wiring] 제어기 ping 루프가 멈췄습니다: {ex.Message}"); }
    }
}

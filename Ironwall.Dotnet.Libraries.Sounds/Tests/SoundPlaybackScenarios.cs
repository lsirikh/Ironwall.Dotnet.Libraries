using System.Diagnostics;
using System.IO;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Providers;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using NAudio.Wave;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sounds.Tests;

/// <summary>
/// 재생 시험 하나가 쓰는 <b>자기만의</b> 사운드 서비스 — 설정(자동 멈춤 켬/끔 · 지속 시간) · 소리 목록 · 큐를 다른 시험과 나누지 않는다.
/// <para><b>왜</b>: 예전 재생 시험 4건은 픽스처의 SetupModel 하나를 나눠 쓰면서 서로 반대 설정을 전제했다
/// (자동 멈춤 시험은 켬, 동시 재생 방지 시험은 끔). 그래서 어느 쪽이든 하나는 늘 깨지고, 설정을 바꾸는 시험이 끼면 순서에 따라 결과가 달랐다.</para>
/// </summary>
internal sealed class IsolatedSoundService : IAsyncDisposable
{
    public SoundService Service { get; }
    public SoundSourceProvider Provider { get; }
    public SoundSetupModel Setup { get; }

    private IsolatedSoundService(SoundService service, SoundSourceProvider provider, SoundSetupModel setup)
    {
        Service = service;
        Provider = provider;
        Setup = setup;
    }

    /// <summary>
    /// 실제 소리 파일 · 실제 NAudio 로 만든다(소리가 난다 — 사람이 있는 PC 에서만 돌린다).
    /// 역할마다 다른 파일을 쓴다 — 같은 파일이면 한 SoundModel 을 나눠 써 재생 상태 판정이 섞인다.
    /// </summary>
    public static async Task<IsolatedSoundService> CreateRealAsync(SoundServiceFixture fx, Action<SoundSetupModel> configure)
    {
        var files = fx.ActualSoundFiles;
        Assert.True(files.Count >= 3, $"재생 시험은 역할(탐지 · 장애 · 조치)마다 다른 소리 파일 3개가 필요하다 — 지금 {files.Count}개");

        var setup = new SoundSetupModel(fx.SetupModel)
        {
            DetectionSound = Path.GetFileNameWithoutExtension(files[0]),
            MalfunctionSound = Path.GetFileNameWithoutExtension(files[1]),
            ActionReportSound = Path.GetFileNameWithoutExtension(files[2]),
        };
        configure(setup);

        var provider = new SoundSourceProvider();
        var service = new SoundService(fx.LogService, setup, provider, new AudioDeviceInfoProvider());
        await service.SetSoundProviderAsync(files);
        return new IsolatedSoundService(service, provider, setup);
    }

    /// <summary>가짜 재생기 · 가짜 읽기기로 만든다 — 소리 없이 같은 재생 판정을 본다(파일도 필요 없다).</summary>
    public static async Task<IsolatedSoundService> CreateFakeAsync(FakeWavePlayerFarm farm, Action<SoundSetupModel> configure)
    {
        var files = new List<string> { @"C:\fake-sounds\detection.wav", @"C:\fake-sounds\malfunction.wav", @"C:\fake-sounds\action.wav" };
        var setup = new SoundSetupModel
        {
            DetectionSound = "detection",
            MalfunctionSound = "malfunction",
            ActionReportSound = "action",
            DetectionSoundDuration = 1,
            MalfunctionSoundDuration = 1,
            ActionReportSoundDuration = 1,
        };
        configure(setup);

        var provider = new SoundSourceProvider();
        var service = new SoundService(new LogService(), setup, provider, new AudioDeviceInfoProvider(),
                                       farm.CreatePlayer, _ => FakeWavePlayerFarm.CreateReader());
        await service.SetSoundProviderAsync(files);
        return new IsolatedSoundService(service, provider, setup);
    }

    public ISoundModel Sound(string name) => Provider.First(s => s.Name == name);

    public async ValueTask DisposeAsync() => await Service.StopAllSoundsAsync();
}

/// <summary>
/// 재생 판정 — 실제 소리 시험과 가짜 재생기 시험이 <b>같은 판정</b>을 쓴다(시간 값만 다르다).
/// <para>재생 요청(<c>…SoundPlayAsync</c>)은 큐에 넣고 곧바로 끝난다 — 재생 끝을 알려면 소리의 <c>IsPlaying</c> 과
/// 완료 사건을 기다려야 한다. 예전 시험은 요청 Task 를 재생 끝으로 착각해 설정과 무관하게도 깨질 수 있었다.</para>
/// </summary>
internal static class SoundPlaybackScenarios
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 자동 멈춤 켬 — 지속 시간이 지나면 <b>스스로</b> 멈춘다(안전 취소 전에, 지속 시간보다 먼저 멈추지 않고).
    /// </summary>
    /// <param name="clipAllowance">자동 멈춤은 지속 시간이 지난 뒤 <b>지금 도는 한 바퀴가 끝날 때</b> 멈춘다 — 한 바퀴 길이만큼의 여유.</param>
    public static async Task AutoStopsAfterDurationAsync(IsolatedSoundService iso, EnumEventType kind, TimeSpan clipAllowance)
    {
        var (name, durationSec, autoStop) = kind switch
        {
            EnumEventType.Intrusion => (iso.Setup.DetectionSound!, iso.Setup.DetectionSoundDuration, iso.Setup.IsDetectionAutoSoundStop),
            EnumEventType.Action => (iso.Setup.ActionReportSound!, iso.Setup.ActionReportSoundDuration, iso.Setup.IsActionReportAutoSoundStop),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Assert.True(autoStop, "이 판정은 자동 멈춤 켬 설정에서만 의미가 있다");
        Assert.True(durationSec > 0);

        var duration = TimeSpan.FromSeconds(durationSec);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (kind == EnumEventType.Intrusion) iso.Service.OnDetectionPlaybackCompleted += () => completed.TrySetResult();

        // 안전 취소는 지속 시간 + 한 바퀴보다 넉넉히 — 예전 시험은 안전 취소 = 지속 시간이라 자동 멈춤과 경주했다.
        using var safety = new CancellationTokenSource(duration + clipAllowance + StopTimeout + StopTimeout);
        var sound = iso.Sound(name);
        var sw = Stopwatch.StartNew();

        if (kind == EnumEventType.Intrusion) await iso.Service.DetectionSoundPlayAsync(safety.Token);
        else await iso.Service.ActionReportSoundPlayAsync(safety.Token);

        Assert.True(await WaitUntilAsync(() => sound.IsPlaying, StartTimeout), "재생이 시작되지 않았다");
        Assert.True(await WaitUntilAsync(() => !sound.IsPlaying, duration + clipAllowance + StopTimeout), "지속 시간이 지나도 스스로 멈추지 않았다");
        sw.Stop();

        Assert.False(safety.IsCancellationRequested, "안전 취소가 먼저 걸렸다 — 자동 멈춤이 아니라 취소로 멈췄다");
        Assert.True(sw.Elapsed >= duration, $"지속 시간({duration.TotalSeconds}s)보다 먼저 멈췄다: {sw.Elapsed.TotalMilliseconds:F0}ms");
        if (kind == EnumEventType.Intrusion)
            Assert.True(await Task.WhenAny(completed.Task, Task.Delay(StopTimeout)) == completed.Task, "탐지 재생 완료 사건이 오지 않았다(정상 완료가 아니다)");
    }

    /// <summary>
    /// 자동 멈춤 끔(장애) — 지속 시간이 지나도 계속 울리고, 취소해야만 멈춘다. 취소로 멈춘 것은 완료 사건을 내지 않는다.
    /// </summary>
    public static async Task KeepsPlayingUntilCancelledAsync(IsolatedSoundService iso)
    {
        Assert.False(iso.Setup.IsMalfunctionAutoSoundStop, "이 판정은 자동 멈춤 끔 설정에서만 의미가 있다");

        var completed = false;
        iso.Service.OnMalfunctionPlaybackCompleted += () => completed = true;

        using var cts = new CancellationTokenSource();
        var sound = iso.Sound(iso.Setup.MalfunctionSound!);
        await iso.Service.MalfunctionSoundPlayAsync(cts.Token);

        Assert.True(await WaitUntilAsync(() => sound.IsPlaying, StartTimeout), "재생이 시작되지 않았다");

        // 지속 시간 설정보다 오래 기다려도 계속 울린다(자동 멈춤 끔 = 수동 멈춤)
        await Task.Delay(TimeSpan.FromSeconds(iso.Setup.MalfunctionSoundDuration) + TimeSpan.FromMilliseconds(500));
        Assert.True(sound.IsPlaying, "자동 멈춤 끔인데 스스로 멈췄다");

        cts.Cancel();
        Assert.True(await WaitUntilAsync(() => !sound.IsPlaying, StopTimeout), "취소했는데 멈추지 않았다");
        await Task.Delay(100);
        Assert.False(completed, "취소로 멈췄는데 완료 사건이 났다");
    }

    /// <summary>
    /// 동시 재생 방지 — 탐지가 울리는 동안 장애 요청은 큐에서 기다리고, 두 소리가 같이 울리는 순간이 없다.
    /// </summary>
    /// <param name="activePlayers">출력 장치 수준에서 지금 울리는 재생기 수(가짜 재생기만 셀 수 있다 — 실제 소리는 <c>null</c>).</param>
    public static async Task NeverPlaysTwoAtOnceAsync(IsolatedSoundService iso, TimeSpan observe, Func<int>? activePlayers = null)
    {
        Assert.False(iso.Setup.IsDetectionAutoSoundStop, "이 판정은 탐지 자동 멈춤 끔(계속 울림) 설정에서 본다");

        using var cts = new CancellationTokenSource();
        var detection = iso.Sound(iso.Setup.DetectionSound!);
        var malfunction = iso.Sound(iso.Setup.MalfunctionSound!);
        Assert.NotSame(detection, malfunction);

        await iso.Service.DetectionSoundPlayAsync(cts.Token);
        Assert.True(await WaitUntilAsync(() => detection.IsPlaying, StartTimeout), "탐지 재생이 시작되지 않았다");

        await iso.Service.MalfunctionSoundPlayAsync(cts.Token);
        Assert.Equal(1, iso.Service.GetQueueStatus().QueueCount);   // 두 번째 요청은 큐에서 기다린다

        var sw = Stopwatch.StartNew();
        var maxActive = 0;
        while (sw.Elapsed < observe)
        {
            Assert.True(detection.IsPlaying, "탐지 재생이 중간에 멈췄다");
            Assert.False(malfunction.IsPlaying, "탐지가 울리는 동안 장애 소리가 같이 울렸다");
            if (activePlayers is not null) maxActive = Math.Max(maxActive, activePlayers());
            await Task.Delay(20);
        }
        if (activePlayers is not null) Assert.Equal(1, maxActive);

        cts.Cancel();
        Assert.True(await WaitUntilAsync(() => !detection.IsPlaying && !malfunction.IsPlaying
                                               && !iso.Service.GetQueueStatus().IsProcessing, StopTimeout),
                    "취소 뒤에도 재생 · 큐 처리가 끝나지 않았다");
        if (activePlayers is not null) Assert.Equal(0, activePlayers());
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition()) return true;
            await Task.Delay(10);
        }
        return condition();
    }
}

/// <summary>
/// 가짜 출력 장치 묶음 — 소리를 내지 않는다. <c>Play</c> 하면 <see cref="ClipLength"/> 뒤 한 바퀴가 끝난 것처럼
/// <c>PlaybackStopped</c> 를 내고, <c>Stop</c> 하면 곧바로 낸다(NAudio 와 같은 순서). 지금 울리는 재생기 수를 센다.
/// </summary>
internal sealed class FakeWavePlayerFarm
{
    public static readonly TimeSpan ClipLength = TimeSpan.FromMilliseconds(150);

    private int _active;
    private int _created;

    /// <summary>지금 울리는(Playing) 재생기 수.</summary>
    public int Active => Volatile.Read(ref _active);

    /// <summary>만든 재생기 수.</summary>
    public int Created => Volatile.Read(ref _created);

    public IWavePlayer CreatePlayer()
    {
        Interlocked.Increment(ref _created);
        return new FakeWavePlayer(this);
    }

    /// <summary>1초 분량의 무음 — 읽기기 자리를 채울 뿐 재생 시간은 <see cref="ClipLength"/> 로 정한다.</summary>
    public static WaveStream CreateReader()
        => new RawSourceWaveStream(new MemoryStream(new byte[16000]), new WaveFormat(8000, 16, 1));

    private sealed class FakeWavePlayer : IWavePlayer
    {
        private readonly FakeWavePlayerFarm _farm;
        private readonly object _gate = new();
        private int _generation;

        public FakeWavePlayer(FakeWavePlayerFarm farm) => _farm = farm;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public float Volume { get; set; } = 1f;
        public WaveFormat OutputWaveFormat { get; private set; } = new(8000, 16, 1);

        public void Init(IWaveProvider waveProvider) => OutputWaveFormat = waveProvider.WaveFormat;

        public void Play()
        {
            int generation;
            lock (_gate)
            {
                if (PlaybackState == PlaybackState.Playing) return;
                PlaybackState = PlaybackState.Playing;
                generation = ++_generation;
                Interlocked.Increment(ref _farm._active);
            }

            _ = Task.Delay(ClipLength).ContinueWith(_ => EndPlayback(generation), TaskScheduler.Default);
        }

        public void Stop() => EndPlayback(null);

        public void Pause() { }

        public void Dispose() => EndPlayback(null);

        /// <summary>울리는 중이면 멈추고 사건을 낸다. 한 바퀴 끝 타이머는 그 사이 새로 Play 됐으면 무시한다.</summary>
        private void EndPlayback(int? generation)
        {
            lock (_gate)
            {
                if (PlaybackState != PlaybackState.Playing) return;
                if (generation is not null && generation != _generation) return;
                PlaybackState = PlaybackState.Stopped;
                Interlocked.Decrement(ref _farm._active);
            }
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }
    }
}

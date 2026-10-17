using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// 호스트 카메라 창구의 PTZ 순서 — 누름 이동 · 뗌 정지 · 영상 드래그가 "카메라 준비(ONVIF 연결 수 초)"와 엇갈릴 때.
/// 가짜 제공자의 준비를 시험이 손으로 끝낸다(실제 ONVIF 없음).
/// </summary>
public class HostCameraServicesPtzTests
{
    private sealed class FakePtzProvider : ICameraPtzProvider
    {
        private readonly TaskCompletionSource _prepare = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _prepared;

        public ConcurrentQueue<string> Calls { get; } = new();

        public void FinishPrepare() => _prepare.TrySetResult();

        public async Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
        {
            await _prepare.Task.WaitAsync(ct).ConfigureAwait(false);
            _prepared = true;
            return new PtzReadiness(true, true, false);
        }

        public bool IsPrepared(string cameraId) => _prepared;

        public Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct)
        {
            Calls.Enqueue(FormattableString.Invariant($"move:{pan},{tilt},{zoom}"));
            return Task.FromResult(true);
        }

        public Task StopAsync(string cameraId, CancellationToken ct)
        {
            Calls.Enqueue("stop");
            return Task.CompletedTask;
        }

        public Task<PtzDragOutcome> DragMoveAsync(string cameraId, double viewX, double viewY, double viewAspect, CancellationToken ct)
        {
            Calls.Enqueue(FormattableString.Invariant($"drag:{viewX:0.###},{viewY:0.###}"));
            return Task.FromResult(new PtzDragOutcome(true, PtzDragMoveKind.RelativeGeneric, viewX, viewY, null));
        }

        public Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PtzPresetInfo>?>(null);
        public Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct) => Task.FromResult(false);
        public bool IsImagingCapable(string cameraId) => false;
        public Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct) => Task.FromResult<CameraImagingState?>(null);
        public Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct) => Task.FromResult(false);
        public Task StopFocusAsync(string cameraId, CancellationToken ct) => Task.CompletedTask;
        public void Release(string cameraId) { }
    }

    private static readonly VideoProviderInfo Info = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    private static (HostCameraServices Services, FakePtzProvider Provider) Create()
    {
        var provider = new FakePtzProvider();
        var registry = new CameraProviderRegistry(provider, Array.Empty<ICameraVideoProvider>());
        var log = new HostLog(Path.Combine(Path.GetTempPath(), "ironwall-camhost-tests", "ptz-" + Guid.NewGuid().ToString("N")[..8]));
        return (new HostCameraServices(registry, log, _ => { }), provider);
    }

    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static async Task<bool> UntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition()) return true;
            await Task.Yield();
        }
        return condition();
    }

    [Fact]
    public async Task should_not_move_when_button_is_released_before_camera_finishes_preparing()
    {
        // Arrange — 타일 패드를 눌렀는데 카메라가 아직 준비(ONVIF 연결) 중
        var (services, provider) = Create();
        var move = services.MoveAsync("c1", Info, 0.5, 0, 0, Within(5000));

        // Act — 준비가 끝나기 전에 뗀다 → 그 뒤 준비가 끝난다
        await services.StopAsync("c1", Info, Within(5000));
        provider.FinishPrepare();
        await move;

        // Assert — 뗀 뒤에 이동이 나가면 멈출 사람이 없다(유지 재전송과 함께 계속 돈다). 나가면 안 된다.
        Assert.DoesNotContain(provider.Calls, c => c.StartsWith("move", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_move_when_camera_finishes_preparing_while_button_is_still_held()
    {
        var (services, provider) = Create();
        var move = services.MoveAsync("c1", Info, 0.5, 0, 0, Within(5000));

        provider.FinishPrepare();
        Assert.True(await move);
        await services.StopAsync("c1", Info, Within(5000));

        Assert.Equal(new[] { "move:0.5,0,0", "stop" }, provider.Calls);
    }

    [Fact]
    public async Task should_send_only_the_stop_when_move_then_stop_arrive_from_gis_while_preparing()
    {
        // Arrange — 더블클릭 팝업(파이프) 경로: 준비 전에 이동 · 정지가 차례로 도착
        var (services, provider) = Create();
        services.HandlePtz(new PtzCommand { CameraId = "c1", Operation = PtzOperation.ContinuousMove, Pan = 0.4, Provider = Info });
        services.HandlePtz(new PtzCommand { CameraId = "c1", Operation = PtzOperation.Stop, Provider = Info });

        // Act
        provider.FinishPrepare();

        // Assert — 밀린 이동 뒤 정지 → 정지만
        Assert.True(await UntilAsync(() => !provider.Calls.IsEmpty));
        await Task.Delay(100);
        Assert.Equal(new[] { "stop" }, provider.Calls);
    }

    [Fact]
    public async Task should_dispatch_one_drag_when_drag_move_arrives_from_gis()
    {
        var (services, provider) = Create();
        provider.FinishPrepare();
        await services.ExecuteAsync(new CameraRequest { RequestId = "r", Kind = CameraRequestKind.PreparePtz, CameraId = "c1", Provider = Info });

        services.HandlePtz(new PtzCommand { CameraId = "c1", Operation = PtzOperation.DragMove, ViewX = 0.25, ViewY = -0.1, ViewAspect = 1.5, Provider = Info });

        Assert.True(await UntilAsync(() => !provider.Calls.IsEmpty));
        Assert.Equal(new[] { "drag:0.25,-0.1" }, provider.Calls);
    }

    [Fact]
    public async Task should_keep_only_the_last_drag_when_drags_arrive_while_preparing()
    {
        var (services, provider) = Create();
        for (int i = 1; i <= 5; i++)
            services.HandlePtz(new PtzCommand { CameraId = "c1", Operation = PtzOperation.DragMove, ViewX = i * 0.1, Provider = Info });

        provider.FinishPrepare();

        Assert.True(await UntilAsync(() => !provider.Calls.IsEmpty));
        await Task.Delay(100);
        Assert.Equal(new[] { "drag:0.5,0" }, provider.Calls);   // 준비 중 밀린 드래그는 마지막 하나만
    }

    [Fact]
    public async Task should_drop_tile_drag_when_stop_arrives_before_camera_finishes_preparing()
    {
        var (services, provider) = Create();
        var drag = services.DragMoveAsync("c1", Info, 0.3, 0, 1.5, Within(5000));

        await services.StopAsync("c1", Info, Within(5000));
        provider.FinishPrepare();
        var outcome = await drag;

        Assert.False(outcome.Sent);
        Assert.Equal("superseded", outcome.Reason);
        Assert.DoesNotContain(provider.Calls, c => c.StartsWith("drag", StringComparison.Ordinal));
    }
}

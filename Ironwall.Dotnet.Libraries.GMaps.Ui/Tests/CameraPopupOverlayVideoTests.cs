using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Moq;
using Xunit;
using SettingsKind = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.VideoProviderKind;
using CameraPopupProviderFactory = Ironwall.Dotnet.Libraries.Events.Ui.Helpers.CameraPopupProviderFactory;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : 지도 더블클릭 오버레이 → 팝업 호스트 이관(camera-popup-modes T-02 · FR-04/24/26/29) 헤드리스 시험
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>공유 메모리 프레임 가짜 — 호스트 상태 메시지를 흉내 낸다.</summary>
internal sealed class OverlayTestFrameSource : IFrameSource
{
    public OverlayTestFrameSource(OverlayStreamRequest request) => Request = request;
    public OverlayStreamRequest Request { get; }
    public string StreamId => Request.StreamId ?? "";
    public int Width => Request.Width;
    public int Height => Request.Height;
    public int Stride => Width * 4;
    public long PublishedSequence => 0;
    public StreamState State { get; private set; } = StreamState.Opening;
    public string? StateDetail { get; private set; }
    public bool Disposed { get; private set; }
    public event EventHandler? StateChanged;
    public void Set(StreamState state, string? detail = null) { State = state; StateDetail = detail; StateChanged?.Invoke(this, EventArgs.Empty); }
    public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence) { sequence = 0; return false; }
    public void Dispose() => Disposed = true;
}

/// <summary>감시자 가짜 — 오버레이 열기 · 재시작만 기록(LibVLC · 프로세스 없음).</summary>
internal sealed class OverlayTestHost : ICameraPopupHost
{
    public CameraPopupHostState State { get; private set; } = CameraPopupHostState.Running;
    public string? StateReason => null;
    public List<OverlayTestFrameSource> Opened { get; } = new();
    public int Restarts { get; private set; }
    public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged;
    public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived { add { } remove { } }

    public void ChangeState(CameraPopupHostState state)
    {
        var old = State;
        State = state;
        StateChanged?.Invoke(this, new CameraPopupHostStateChangedEventArgs(old, state, "test", null, null));
    }

    public IFrameSource? OpenOverlay(OverlayStreamRequest request)
    {
        var source = new OverlayTestFrameSource(request);
        Opened.Add(source);
        return source;
    }

    public void Restart() => Restarts++;
    public void Start() { }
    public void CloseOverlay(string streamId) { }
    public void OpenEventWindow(OpenEventWindow request) { }
    public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
    public void BringEventWindowToFront(string eventKey) { }
    public void SetTheme(string theme) { }
    public void SendPtz(PtzCommand command) { }
    public void Dispose() { }
}

public class CameraPopupOverlayVideoTests
{
    private static readonly VideoProviderInfo Provider = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80, Username = "u", Password = "p" };

    /// <summary>디바운스 대기를 시험이 손으로 푼다.</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _waits = new();
        public int Pending => _waits.Count(w => !w.Task.IsCompleted);
        public Task Wait(TimeSpan _, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => tcs.TrySetCanceled());
            _waits.Add(tcs);
            return tcs.Task;
        }
        public async Task ReleaseAllAsync()
        {
            foreach (var w in _waits.ToArray()) w.TrySetResult();
            await Task.Delay(50);
        }
    }

    private static (CameraStreamPopupViewModel Vm, OverlayTestHost Host, ManualDelay Delay) Create()
    {
        var host = new OverlayTestHost();
        var delay = new ManualDelay();
        var vm = new CameraStreamPopupViewModel(7, "정문", new PointLatLng(37, 127), host, Provider, delay.Wait);
        return (vm, host, delay);
    }

    [Fact]
    public async Task should_open_overlay_via_host_at_reported_pixel_size_when_started()
    {
        // Arrange
        var (vm, host, delay) = Create();

        // Act — 시작 → 컨트롤이 실제 픽셀 크기(150% 배율) 보고 → 디바운스 풀림
        vm.StartVideo();
        vm.UpdateVideoViewport(577, 387);
        await delay.ReleaseAllAsync();

        // Assert — 한 번만, 짝수로 정리된 크기, 카메라 id · 제공자 정보 동봉(LibVLC 는 호스트 몫)
        var opened = Assert.Single(host.Opened);
        Assert.Equal((576, 386), (opened.Request.Width, opened.Request.Height));
        Assert.Equal("map-cam-7", opened.Request.StreamId);
        Assert.Equal("7", opened.Request.Camera.CameraId);
        Assert.Same(Provider, opened.Request.Provider);
        Assert.Same(opened, vm.FrameSource);
        Assert.Equal(CameraPopupVideoStatus.Connecting, vm.VideoStatus);
    }

    [Fact]
    public async Task should_reopen_once_at_new_size_when_viewport_changes_beyond_tolerance()
    {
        // Arrange
        var (vm, host, delay) = Create();
        vm.StartVideo();
        await delay.ReleaseAllAsync();
        Assert.Single(host.Opened);

        // Act — 작은 떨림(허용 안) → 무시, 크게보기 토글 두 번 연속 → 디바운스로 한 번만
        vm.UpdateVideoViewport(vm.OpenedVideoSize.Width + 4, vm.OpenedVideoSize.Height + 2);
        vm.UpdateVideoViewport(900, 500);
        vm.UpdateVideoViewport(960, 508);
        await delay.ReleaseAllAsync();

        // Assert
        Assert.Equal(2, host.Opened.Count);
        Assert.Equal((960, 508), (host.Opened[1].Request.Width, host.Opened[1].Request.Height));
        Assert.Equal(host.Opened[0].Request.StreamId, host.Opened[1].Request.StreamId);   // 같은 id = 감시자가 교체
    }

    [Theory]
    [InlineData(StreamState.Opening, "resolving", CameraPopupVideoStatus.Connecting, true)]
    [InlineData(StreamState.Opening, "connecting", CameraPopupVideoStatus.Connecting, false)]
    [InlineData(StreamState.Playing, null, CameraPopupVideoStatus.Playing, false)]
    [InlineData(StreamState.Failed, "open-timeout", CameraPopupVideoStatus.Failed, false)]
    [InlineData(StreamState.Stalled, "end-reached", CameraPopupVideoStatus.Failed, false)]
    [InlineData(StreamState.Failed, "not-supported", CameraPopupVideoStatus.Unsupported, false)]
    public async Task should_show_stream_state_in_overlay_when_host_reports(StreamState state, string? detail, CameraPopupVideoStatus expected, bool resolving)
    {
        var (vm, host, delay) = Create();
        vm.StartVideo();
        await delay.ReleaseAllAsync();

        host.Opened[0].Set(state, detail);

        Assert.Equal(expected, vm.VideoStatus);
        Assert.Equal(resolving, vm.IsResolvingSource);
        Assert.Equal(expected == CameraPopupVideoStatus.Failed, vm.CanRetryVideo);
        Assert.Equal(CameraStreamPopupViewModel.StatusText(expected), vm.VideoStatusText);
    }

    [Fact]
    public async Task should_reopen_only_this_overlay_when_retry_pressed_after_failure()
    {
        var (vm, host, delay) = Create();
        vm.StartVideo();
        await delay.ReleaseAllAsync();
        host.Opened[0].Set(StreamState.Failed, "libvlc-error");
        Assert.Equal("연결 안 됨", vm.VideoStatusText);

        vm.RetryVideoCommand.Execute(null);

        Assert.Equal(2, host.Opened.Count);
        Assert.Equal(CameraPopupVideoStatus.Connecting, vm.VideoStatus);
    }

    [Theory]
    [InlineData(CameraPopupHostState.Suspended)]
    [InlineData(CameraPopupHostState.Unavailable)]
    public async Task should_offer_restart_and_keep_popup_when_host_unavailable(CameraPopupHostState state)
    {
        // Arrange
        var (vm, host, delay) = Create();
        vm.StartVideo();
        await delay.ReleaseAllAsync();

        // Act
        host.ChangeState(state);

        // Assert — "영상 기능을 사용할 수 없습니다 · [다시 시작]", 팝업(PTZ 탭 등)은 그대로
        Assert.Equal(CameraPopupVideoStatus.HostUnavailable, vm.VideoStatus);
        Assert.Equal("영상 기능을 사용할 수 없습니다", vm.VideoStatusText);
        Assert.True(vm.CanRestartHost);
        Assert.True(vm.IsVideoMessageVisible);
        vm.RestartHostCommand.Execute(null);
        Assert.Equal(1, host.Restarts);

        // 호스트가 다시 뜨면(감시자가 같은 스트림을 복원) 연결 중 → 상태 메시지에 따라
        host.ChangeState(CameraPopupHostState.Running);
        Assert.Equal(CameraPopupVideoStatus.Connecting, vm.VideoStatus);
    }

    [Fact]
    public async Task should_show_host_unavailable_without_opening_when_no_host_registered()
    {
        var delay = new ManualDelay();
        var vm = new CameraStreamPopupViewModel(7, "정문", new PointLatLng(37, 127), host: null, Provider, delay.Wait);

        vm.StartVideo();
        await delay.ReleaseAllAsync();

        Assert.Null(vm.FrameSource);
        Assert.Equal(CameraPopupVideoStatus.HostUnavailable, vm.VideoStatus);
        Assert.False(vm.CanRestartHost);   // 다시 띄울 감시자가 없다 — 단추 숨김
    }

    [Fact]
    public async Task should_close_overlay_when_popup_disposed()
    {
        var (vm, host, delay) = Create();
        vm.StartVideo();
        await delay.ReleaseAllAsync();

        await vm.DisposeAsync();
        vm.UpdateVideoViewport(1000, 600);
        await delay.ReleaseAllAsync();

        Assert.True(host.Opened[0].Disposed);
        Assert.Null(vm.FrameSource);
        Assert.Single(host.Opened);   // 닫힌 뒤엔 다시 열지 않는다
    }

    /*──────────────── 제공자 정보 만들기(FR-04 현행 규칙) ────────────────*/

    private static ICameraDeviceModel Camera(string? ip, string? sub, string? main = null, int port = 0)
    {
        var urls = new Mock<Ironwall.Dotnet.Monitoring.Models.Devices.ICameraUrlsModel>();
        urls.SetupGet(u => u.RtspSub).Returns(sub!);
        urls.SetupGet(u => u.RtspMain).Returns(main!);
        var cam = new Mock<ICameraDeviceModel>();
        cam.SetupGet(c => c.IpAddress).Returns(ip!);
        cam.SetupGet(c => c.IpPort).Returns(port);
        cam.SetupGet(c => c.UserName).Returns("admin");
        cam.SetupGet(c => c.UserPassword).Returns("S3cr3tPw");
        cam.SetupGet(c => c.Urls).Returns(urls.Object);
        return cam.Object;
    }

    [Fact]
    public void should_build_onvif_provider_with_stored_fallback_when_onvif_setting_and_ip_present()
    {
        var info = CameraPopupProviderFactory.Build(Camera("10.0.0.5", "rtsp://10.0.0.5/sub", port: 8080), SettingsKind.Onvif)!;

        Assert.Equal(VideoProviderKind.Onvif, info.Kind);
        Assert.Equal("10.0.0.5", info.Host);
        Assert.Equal(8080, info.Port);
        Assert.Equal("rtsp://admin:S3cr3tPw@10.0.0.5/sub", info.FallbackUri);   // 저장 주소 + 계정(현행 URL 조회 폴백)
        Assert.True(info.PreferSubStream);
        Assert.DoesNotContain("S3cr3tPw", info.ToString());
    }

    [Fact]
    public void should_fall_back_to_rtsp_url_provider_when_onvif_setting_but_no_ip()
    {
        var info = CameraPopupProviderFactory.Build(Camera(null, "rtsp://10.0.0.5/sub"), SettingsKind.Onvif)!;

        Assert.Equal(VideoProviderKind.Rtsp, info.Kind);
        Assert.Equal("rtsp://admin:S3cr3tPw@10.0.0.5/sub", info.Uri);
    }

    [Fact]
    public void should_return_null_when_rtsp_url_setting_and_nothing_playable()
    {
        Assert.Null(CameraPopupProviderFactory.Build(Camera(null, null), SettingsKind.RtspUrl));
    }

    [Fact]
    public void should_keep_onvif_ptz_endpoint_when_rtsp_url_provider()
    {
        var info = CameraPopupProviderFactory.Build(Camera("10.0.0.5", null, "rtsp://10.0.0.5/main"), SettingsKind.RtspUrl)!;

        Assert.Equal(VideoProviderKind.Rtsp, info.Kind);
        Assert.Equal("rtsp://admin:S3cr3tPw@10.0.0.5/main", info.Uri);
        Assert.Equal("10.0.0.5", info.Host);   // PTZ 는 ONVIF(현행 팝업과 같다)
        Assert.Equal(80, info.Port);
    }

    [Fact]
    public void should_map_external_vms_placeholder_when_selected()
    {
        var info = CameraPopupProviderFactory.Build(Camera("10.0.0.5", null), SettingsKind.ExternalVms)!;
        Assert.Equal(VideoProviderKind.ExternalVms, info.Kind);
    }

    /*──────────────── §0: GIS 쪽 지도 라이브러리는 LibVLC · ONVIF 를 참조하지 않는다 ────────────────*/

    [Fact]
    public void should_not_reference_libvlc_streaming_or_onvif_when_gmaps_ui_built()
    {
        var refs = typeof(CameraStreamPopupViewModel).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        Assert.DoesNotContain("LibVLCSharp", refs);
        Assert.DoesNotContain("LibVLCSharp.WPF", refs);
        Assert.DoesNotContain("Ironwall.Dotnet.Libraries.Streaming", refs);
        Assert.DoesNotContain("Ironwall.Dotnet.Libraries.OnvifSolution", refs);
        Assert.Contains("Ironwall.Dotnet.Libraries.CameraPopup", refs);
    }
}

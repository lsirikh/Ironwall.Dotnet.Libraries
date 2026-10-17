using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 프리셋 관리 · ROI 설정 창 — 카메라 실시간 영상(호스트 오버레이) · 프리셋 자리로 이동
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MappingCameraVideoTests
{
    #region - 가짜 -
    private sealed class FakeSource : IFrameSource
    {
        public FakeSource(string streamId, int width, int height) { StreamId = streamId; Width = width; Height = height; }
        public string StreamId { get; }
        public int Width { get; }
        public int Height { get; }
        public int Stride => Width * 4;
        public long PublishedSequence => 0;
        public StreamState State { get; private set; } = StreamState.Opening;
        public string? StateDetail { get; private set; }
        public bool Disposed { get; private set; }
        public event EventHandler? StateChanged;

        public void Set(StreamState state, string? detail = null)
        {
            State = state;
            StateDetail = detail;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool HasSubscribers => StateChanged is not null;
        public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence) { sequence = 0; return false; }
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeVideo : IMappingCameraVideo
    {
        public MappingVideoHostState HostState { get; set; } = MappingVideoHostState.Running;
        public event EventHandler? HostStateChanged;
        public List<(int CameraId, string StreamId, int Width, int Height)> Opens { get; } = new();
        public List<FakeSource> Sources { get; } = new();
        public string? FailOpen { get; set; }
        public bool IsPtz { get; set; } = true;
        public List<(int CameraId, int Index)> Gotos { get; } = new();
        public MappingCallResult<bool> GotoResult { get; set; } = MappingCallResult<bool>.Ok(true);

        public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height)
        {
            Opens.Add((cameraId, streamId, width, height));
            if (FailOpen is { } fail) return MappingCallResult<IFrameSource>.Fail(fail);
            var source = new FakeSource(streamId, width, height);
            Sources.Add(source);
            return MappingCallResult<IFrameSource>.Ok(source);
        }

        public bool CanGotoPreset(int cameraId) => IsPtz;

        public async Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default)
        {
            Gotos.Add((cameraId, presetIndex));
            await Task.Yield();
            return GotoResult;
        }

        public void SetHost(MappingVideoHostState state)
        {
            HostState = state;
            HostStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>다시 열기 대기를 시험이 직접 푼다.</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource<bool>> _waits = new();
        public int Pending => _waits.Count(w => !w.Task.IsCompleted);

        public Task Delay(TimeSpan _, CancellationToken __)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waits.Add(tcs);
            return tcs.Task;
        }

        public void ReleaseAll() { foreach (var w in _waits) w.TrySetResult(true); }
    }

    private sealed class FakeHost : ICameraPopupHost
    {
        public CameraPopupHostState State { get; set; } = CameraPopupHostState.Running;
        public string? StateReason => null;
        public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged;
        public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived { add { } remove { } }
        public List<OverlayStreamRequest> Overlays { get; } = new();

        public void Start() { }
        public void Restart() { }
        public IFrameSource? OpenOverlay(OverlayStreamRequest request)
        {
            Overlays.Add(request);
            return new FakeSource(request.StreamId ?? "x", request.Width, request.Height);
        }
        public void CloseOverlay(string streamId) { }
        public void OpenEventWindow(OpenEventWindow request) { }
        public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
        public void BringEventWindowToFront(string eventKey) { }
        public void SetTheme(string theme) { }
        public void SendPtz(PtzCommand command) { }
        public void Dispose() { }

        public void Raise(CameraPopupHostState state)
        {
            var old = State;
            State = state;
            StateChanged?.Invoke(this, new CameraPopupHostStateChangedEventArgs(old, state, null, null, null));
        }
    }

    private sealed class FakeControl : ICameraPopupControl
    {
        public bool IsAvailable { get; set; } = true;
        public List<CameraRequest> Requests { get; } = new();
        public Func<CameraRequest, CameraResponse> Reply { get; set; } = r => new CameraResponse { RequestId = r.RequestId, Kind = r.Kind, Success = true };

        public Task<CameraResponse> RequestAsync(CameraRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(Reply(request));
        }

        public bool Move(string cameraId, VideoProviderInfo provider, double pan, double tilt, double zoom) => true;
        public bool Stop(string cameraId, VideoProviderInfo provider) => true;
        public bool DragMove(string cameraId, VideoProviderInfo provider, double viewX, double viewY, double viewAspect) => true;
        public bool Focus(string cameraId, VideoProviderInfo provider, int direction) => true;
        public void Dispose() { }
    }

    private sealed class FakeDirectory : IEventWindowDeviceDirectory
    {
        public Dictionary<int, ICameraDeviceModel> Cameras { get; } = new();
        public ICameraDeviceModel? FindCamera(int cameraId) => Cameras.TryGetValue(cameraId, out var c) ? c : null;
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => null;
        public string? GroupName(int groupId) => null;
    }

    private static CameraDeviceModel Camera(int id, bool ptz = true) => new(id)
    {
        DeviceName = $"카메라 {id}",
        DeviceType = EnumDeviceType.IpCamera,
        Category = ptz ? EnumCameraType.PTZ : EnumCameraType.FIXED,
        IpAddress = $"10.0.0.{id}",
        IpPort = 80,
        UserName = "admin",
        UserPassword = "pw",
        Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{id}/main", RtspSub = $"rtsp://10.0.0.{id}/sub" },
    };

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(20);
        Assert.True(condition(), "조건이 제한 시간 안에 서지 않았다");
    }
    #endregion

    #region - 영상 상자 상태 -
    [Theory]
    [InlineData(false, MappingVideoHostState.Running, true, StreamState.Playing, MappingVideoStatus.Idle)]
    [InlineData(true, MappingVideoHostState.Unavailable, true, StreamState.Playing, MappingVideoStatus.Unavailable)]
    [InlineData(true, MappingVideoHostState.Running, false, null, MappingVideoStatus.Failed)]
    [InlineData(true, MappingVideoHostState.Starting, true, StreamState.Playing, MappingVideoStatus.Connecting)]
    [InlineData(true, MappingVideoHostState.Running, true, null, MappingVideoStatus.Connecting)]
    [InlineData(true, MappingVideoHostState.Running, true, StreamState.Opening, MappingVideoStatus.Connecting)]
    [InlineData(true, MappingVideoHostState.Running, true, StreamState.Playing, MappingVideoStatus.Playing)]
    [InlineData(true, MappingVideoHostState.Running, true, StreamState.Stalled, MappingVideoStatus.Stalled)]
    [InlineData(true, MappingVideoHostState.Running, true, StreamState.Failed, MappingVideoStatus.Failed)]
    public void should_compute_the_box_status_from_host_and_stream(bool started, MappingVideoHostState host, bool opened, StreamState? stream, MappingVideoStatus expected)
        => Assert.Equal(expected, MappingCameraVideoViewModel.Compute(started, host, opened, stream));

    [Fact]
    public void should_open_the_stream_and_follow_its_state_to_playing()
    {
        var video = new FakeVideo();
        var vm = new MappingCameraVideoViewModel(video, 66, "mapping-presets-66");
        Assert.Equal(MappingVideoStatus.Idle, vm.Status);

        vm.Start(640, 360);

        Assert.Equal((66, "mapping-presets-66", 640, 360), Assert.Single(video.Opens));
        Assert.Same(video.Sources[0], vm.FrameSource);
        Assert.Equal(MappingVideoStatus.Connecting, vm.Status);
        Assert.Equal("영상 연결 중…", vm.StatusText);

        video.Sources[0].Set(StreamState.Playing);

        Assert.Equal(MappingVideoStatus.Playing, vm.Status);
        Assert.False(vm.HasStatusText);
        Assert.Equal("실시간 영상", vm.Headline);
        Assert.False(vm.CanRetry);
    }

    [Fact]
    public void should_say_why_and_offer_retry_when_the_stream_fails()
    {
        var video = new FakeVideo();
        var vm = new MappingCameraVideoViewModel(video, 66, "s");
        vm.Start(640, 360);

        video.Sources[0].Set(StreamState.Failed, CameraErrorCodes.NotSupported);
        Assert.Equal(MappingVideoStatus.Failed, vm.Status);
        Assert.Equal("이 카메라는 영상을 지원하지 않습니다.", vm.StatusText);
        Assert.True(vm.CanRetry);

        vm.Retry();
        Assert.Equal(2, video.Opens.Count);
        Assert.Same(video.Sources[1], vm.FrameSource);
        Assert.False(video.Sources[0].HasSubscribers);     // 옛 원천 구독은 푼다
        Assert.Equal(MappingVideoStatus.Connecting, vm.Status);
    }

    [Fact]
    public void should_show_the_open_failure_reason_without_throwing()
    {
        var video = new FakeVideo { FailOpen = "장비 목록에서 이 카메라를 찾지 못해 영상을 열 수 없습니다." };
        var vm = new MappingCameraVideoViewModel(video, 66, "s");

        vm.Start(640, 360);

        Assert.Null(vm.FrameSource);
        Assert.Equal(MappingVideoStatus.Failed, vm.Status);
        Assert.Equal("장비 목록에서 이 카메라를 찾지 못해 영상을 열 수 없습니다.", vm.StatusText);
    }

    [Fact]
    public void should_say_video_is_unavailable_while_the_host_is_down_and_recover_when_it_returns()
    {
        var video = new FakeVideo { HostState = MappingVideoHostState.Unavailable };
        var vm = new MappingCameraVideoViewModel(video, 66, "s");
        vm.Start(640, 360);
        Assert.Equal(MappingVideoStatus.Unavailable, vm.Status);
        Assert.Equal("영상 기능을 쓸 수 없습니다.", vm.StatusText);

        video.Sources[0].Set(StreamState.Playing);
        video.SetHost(MappingVideoHostState.Running);

        Assert.Equal(MappingVideoStatus.Playing, vm.Status);
    }

    [Fact]
    public async Task should_reopen_once_with_the_last_size_after_the_debounce()
    {
        var video = new FakeVideo();
        var delay = new ManualDelay();
        var vm = new MappingCameraVideoViewModel(video, 66, "s", delay.Delay);
        vm.Start(960, 540);

        var first = vm.Resize(960, 720);
        var second = vm.Resize(960, 768);
        Assert.Single(video.Opens);                      // 기다리는 중 — 아직 열지 않는다

        delay.ReleaseAll();
        await Task.WhenAll(first, second);

        Assert.Equal(2, video.Opens.Count);
        Assert.Equal((960, 768), (video.Opens[1].Width, video.Opens[1].Height));
    }

    [Fact]
    public void should_not_reopen_for_a_tiny_size_change()
    {
        var video = new FakeVideo();
        var delay = new ManualDelay();
        var vm = new MappingCameraVideoViewModel(video, 66, "s", delay.Delay);
        vm.Start(960, 540);

        vm.Resize(960, 542);

        Assert.Equal(0, delay.Pending);
        Assert.Single(video.Opens);
    }

    [Fact]
    public void should_close_the_stream_once_on_dispose_and_ignore_later_calls()
    {
        var video = new FakeVideo();
        var vm = new MappingCameraVideoViewModel(video, 66, "s");
        vm.Start(640, 360);
        var source = video.Sources[0];

        vm.Dispose();
        vm.Dispose();
        vm.Retry();
        video.SetHost(MappingVideoHostState.Unavailable);

        Assert.True(source.Disposed);
        Assert.Null(vm.FrameSource);
        Assert.Equal(MappingVideoStatus.Idle, vm.Status);
        Assert.Single(video.Opens);
    }
    #endregion

    #region - ROI 설정 창 -
    [Theory]
    [InlineData(1920, 1080, 960, 540)]
    [InlineData(1280, 1024, 960, 768)]
    [InlineData(704, 576, 960, 784)]
    [InlineData(0, 0, 960, 540)]
    public void should_ask_the_roi_video_in_the_canvas_aspect(double w, double h, int expectedW, int expectedH)
    {
        var aspect = w > 0 && h > 0 ? w / h : double.NaN;
        Assert.Equal((expectedW, expectedH), MappingRoiEditorViewModel.VideoSize(aspect));
    }

    [Fact]
    public void should_have_no_video_without_a_video_channel()
    {
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370");

        Assert.False(vm.HasVideo);
        Assert.Null(vm.Video);
        Assert.False(vm.HasGotoPreset);
        vm.StartVideo();                                 // 없으면 아무 일도 없다
    }

    [Fact]
    public async Task should_open_the_roi_background_video_and_follow_the_reference_resolution()
    {
        var video = new FakeVideo();
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, 370, 2);
        await vm.LoadAsync();

        vm.StartVideo();

        Assert.True(vm.HasVideo);
        Assert.Equal((370, "mapping-roi-3702", 960, 540), Assert.Single(video.Opens));

        vm.BeginNew();
        vm.EditWidth = "1280";
        vm.EditHeight = "1024";

        await WaitUntil(() => video.Opens.Count >= 2);
        Assert.Equal((960, 768), (video.Opens[^1].Width, video.Opens[^1].Height));
    }

    [Fact]
    public async Task should_move_the_camera_to_the_preset_from_the_roi_window()
    {
        var video = new FakeVideo();
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, 370, 2);
        Assert.True(vm.CanGotoPreset);

        await vm.GotoPresetAsync();

        Assert.Equal((370, 2), Assert.Single(video.Gotos));
        Assert.Equal(DialogMessageSeverity.Info, vm.MessageSeverity);
        Assert.Contains("2. 후문", vm.Message);
    }

    [Fact]
    public async Task should_not_move_the_camera_without_edit_permission_or_for_a_fixed_camera()
    {
        var video = new FakeVideo();
        var readOnly = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", false, video, 370, 2);
        var fixedCam = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, new FakeVideo { IsPtz = false }, 370, 2);

        await readOnly.GotoPresetAsync();
        await fixedCam.GotoPresetAsync();

        Assert.True(readOnly.HasGotoPreset);
        Assert.False(readOnly.CanGotoPreset);
        Assert.False(fixedCam.HasGotoPreset);
        Assert.Empty(video.Gotos);
        Assert.True(readOnly.HasVideo);                  // 보기 권한만 있어도 영상은 본다
    }

    [Fact]
    public async Task should_show_the_move_failure_as_a_warning()
    {
        var video = new FakeVideo { GotoResult = MappingCallResult<bool>.Fail("카메라가 응답하지 않아 프리셋 자리로 이동하지 못했습니다.") };
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, 370, 2);

        await vm.GotoPresetAsync();

        Assert.Equal(DialogMessageSeverity.Warning, vm.MessageSeverity);
        Assert.Equal("카메라가 응답하지 않아 프리셋 자리로 이동하지 못했습니다.", vm.Message);
        Assert.True(vm.CanGotoPreset);                   // 다시 누를 수 있다
    }
    #endregion

    #region - 프리셋 관리 창 -
    [Fact]
    public async Task should_hide_the_video_box_without_a_video_channel()
    {
        var manager = new MappingPresetManagerViewModel(new FakePresetRoiServer(), 370, "CAM-370");
        await manager.LoadAsync();

        Assert.False(manager.HasVideo);
        Assert.False(manager.HasGotoPreset);
        Assert.False(manager.CanGotoPreset);
    }

    [Fact]
    public async Task should_show_the_camera_video_and_move_to_the_selected_preset()
    {
        var video = new FakeVideo();
        var manager = new MappingPresetManagerViewModel(new FakePresetRoiServer(), 370, "CAM-370", true, null, video);
        await manager.LoadAsync();
        manager.Video!.Start(MappingPresetManagerViewModel.VIDEO_WIDTH, MappingPresetManagerViewModel.VIDEO_HEIGHT);

        Assert.True(manager.HasVideo);
        Assert.Equal((370, "mapping-presets-370", 640, 360), Assert.Single(video.Opens));
        Assert.False(manager.CanGotoPreset);             // 고른 것이 없다

        manager.SelectedPreset = manager.Presets.Single(p => p.Id == 3702);
        Assert.True(manager.CanGotoPreset);
        await manager.GotoPresetAsync();

        Assert.Equal((370, 2), Assert.Single(video.Gotos));
        Assert.Contains("'후문' 자리로 옮겼습니다", manager.Message);

        manager.BeginNew();
        Assert.False(manager.CanGotoPreset);             // 서버에 없는 새 프리셋은 움직일 번호가 아직 없다
    }

    [Fact]
    public async Task should_hand_the_video_channel_and_preset_number_to_the_roi_window()
    {
        var video = new FakeVideo();
        MappingRoiEditorViewModel? opened = null;
        var manager = new MappingPresetManagerViewModel(new FakePresetRoiServer(), 370, "CAM-370", true, null, video)
        {
            ShowRoiEditor = editor => { opened = editor; return Task.CompletedTask; },
        };
        await manager.LoadAsync();
        manager.SelectedPreset = manager.Presets.Single(p => p.Id == 3701);

        await manager.EditRoisAsync();

        Assert.NotNull(opened);
        Assert.True(opened!.HasVideo);
        Assert.Equal(370, opened.CameraId);
        Assert.Equal(1, opened.PresetIndex);
        Assert.Equal("mapping-roi-3701", opened.Video!.StreamId);
        Assert.NotEqual(manager.Video!.StreamId, opened.Video.StreamId);   // 두 창이 서로의 스트림을 교체하지 않는다
    }

    [Fact]
    public async Task should_pass_the_workbench_video_channel_to_the_preset_manager()
    {
        var video = new FakeVideo();
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new Ironwall.Dotnet.Libraries.Messages.Dto.Integrations.MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 3, IsEnable = true, UpdatedAt = "T0",
            Camera = new Ironwall.Dotnet.Libraries.Messages.Dto.Integrations.MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var server = new FakePresetServer().With(370, (3701, 1, "정문"));
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: server, presetEditor: server)
        {
            CameraVideo = video,
        };
        await vm.ReloadAsync();
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;

        MappingPresetManagerViewModel? opened = null;
        vm.ShowPresetManager = manager => { opened = manager; return Task.CompletedTask; };
        await vm.ManagePresetsAsync();

        Assert.True(opened?.HasVideo);
        Assert.Equal(370, opened!.Video!.CameraId);
    }
    #endregion

    #region - 호스트 구현 -
    [Fact]
    public void should_open_a_host_overlay_with_the_event_window_provider()
    {
        var host = new FakeHost();
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var channel = new HostMappingCameraVideo(host, directory, control: () => new FakeControl());

        var result = channel.OpenVideo(66, "mapping-roi-9", 961, 541);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(host.Overlays);
        Assert.Equal("mapping-roi-9", request.StreamId);
        Assert.Equal("66", request.Camera.CameraId);
        Assert.Equal("카메라 66", request.Camera.Name);
        Assert.Equal((960, 540), (request.Width, request.Height));   // 짝수로 자른다
        Assert.NotNull(request.Provider);
    }

    [Fact]
    public void should_fail_without_throwing_when_there_is_no_host_or_camera()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var noHost = HostMappingCameraVideo.FromHost(null, directory, null, null);
        var host = new FakeHost();
        var withHost = new HostMappingCameraVideo(host, directory);

        var a = noHost.OpenVideo(66, "s", 640, 360);
        var b = withHost.OpenVideo(99, "s", 640, 360);

        Assert.False(a.IsSuccess);
        Assert.Equal("영상 기능을 쓸 수 없습니다.", a.Message);
        Assert.Equal(MappingVideoHostState.Unavailable, noHost.HostState);
        Assert.False(b.IsSuccess);
        Assert.Empty(host.Overlays);
    }

    [Theory]
    [InlineData(CameraPopupHostState.Running, MappingVideoHostState.Running)]
    [InlineData(CameraPopupHostState.Starting, MappingVideoHostState.Starting)]
    [InlineData(CameraPopupHostState.Restarting, MappingVideoHostState.Starting)]
    [InlineData(CameraPopupHostState.NotStarted, MappingVideoHostState.Unavailable)]
    [InlineData(CameraPopupHostState.Suspended, MappingVideoHostState.Unavailable)]
    [InlineData(CameraPopupHostState.Unavailable, MappingVideoHostState.Unavailable)]
    [InlineData(CameraPopupHostState.Disposed, MappingVideoHostState.Unavailable)]
    public void should_map_host_states(CameraPopupHostState state, MappingVideoHostState expected)
        => Assert.Equal(expected, HostMappingCameraVideo.Map(state));

    [Fact]
    public void should_relay_host_state_changes()
    {
        var host = new FakeHost { State = CameraPopupHostState.Running };
        var channel = new HostMappingCameraVideo(host, new FakeDirectory());
        var raised = 0;
        channel.HostStateChanged += (_, _) => raised++;
        channel.HostStateChanged += (_, _) => throw new InvalidOperationException("구독자 실패");

        host.Raise(CameraPopupHostState.Restarting);       // 구독자 하나가 던져도 호스트 감시자로 새지 않는다

        Assert.Equal(1, raised);
        Assert.Equal(MappingVideoHostState.Starting, channel.HostState);
    }

    [Fact]
    public async Task should_send_goto_preset_with_the_preset_number_as_token()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var control = new FakeControl();
        var channel = new HostMappingCameraVideo(new FakeHost(), directory, control: () => control);

        var result = await channel.GotoPresetAsync(66, 3);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(control.Requests);
        Assert.Equal(CameraRequestKind.GotoPreset, request.Kind);
        Assert.Equal("66", request.CameraId);
        Assert.Equal("3", request.PresetToken);
        Assert.NotNull(request.Provider);
    }

    [Fact]
    public async Task should_not_move_a_fixed_camera_and_should_explain_host_failures()
    {
        var directory = new FakeDirectory();
        directory.Cameras[5] = Camera(5, ptz: false);
        directory.Cameras[66] = Camera(66);
        var control = new FakeControl { Reply = r => CameraResponse.Fail(r, CameraErrorCodes.Timeout, "12000 ms") };
        var channel = new HostMappingCameraVideo(new FakeHost(), directory, control: () => control);

        Assert.False(channel.CanGotoPreset(5));
        Assert.True(channel.CanGotoPreset(66));
        var fixedCam = await channel.GotoPresetAsync(5, 1);
        Assert.False(fixedCam.IsSuccess);
        Assert.Empty(control.Requests);

        var timeout = await channel.GotoPresetAsync(66, 1);
        Assert.False(timeout.IsSuccess);
        Assert.Equal("카메라가 응답하지 않아 프리셋 자리로 이동하지 못했습니다.", timeout.Message);

        control.IsAvailable = false;
        var down = await channel.GotoPresetAsync(66, 1);
        Assert.Equal("영상 기능을 쓸 수 없어 카메라를 움직이지 못했습니다.", down.Message);
    }
    #endregion
}

using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : ROI 설정 창 — 새 ROI 기준 해상도를 카메라 실제 해상도(메인 스트림)로 채운다
   Created By   : Claude
   Created On   : 2026-10-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MappingRoiCameraResolutionTests
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
        public StreamState State => StreamState.Opening;
        public string? StateDetail => null;
        public event EventHandler? StateChanged { add { } remove { } }
        public bool TryCopyLatest(IntPtr destination, int destinationStride, long destinationBytes, out long sequence) { sequence = 0; return false; }
        public void Dispose() { }
    }

    /// <summary>해상도 읽기를 시험이 손으로 끝낸다.</summary>
    private sealed class ResolutionVideo : IMappingCameraVideo
    {
        private TaskCompletionSource<MappingCallResult<MappingVideoResolution>> _reply = NewReply();

        public List<(int Width, int Height)> Opens { get; } = new();
        public List<int> Reads { get; } = new();
        public MappingVideoHostState HostState => MappingVideoHostState.Running;
        public event EventHandler? HostStateChanged { add { } remove { } }

        public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height)
        {
            Opens.Add((width, height));
            return MappingCallResult<IFrameSource>.Ok(new FakeSource(streamId, width, height));
        }

        public bool CanGotoPreset(int cameraId) => true;
        public Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default)
            => Task.FromResult(MappingCallResult<bool>.Ok(true));

        public Task<MappingCallResult<MappingVideoResolution>> ReadVideoResolutionAsync(int cameraId, CancellationToken token = default)
        {
            Reads.Add(cameraId);
            return _reply.Task;
        }

        public void Answer(int width, int height) => _reply.TrySetResult(MappingCallResult<MappingVideoResolution>.Ok(new MappingVideoResolution(width, height)));
        public void Refuse() => _reply.TrySetResult(MappingCallResult<MappingVideoResolution>.Fail("카메라 해상도를 읽지 못했습니다."));

        private static TaskCompletionSource<MappingCallResult<MappingVideoResolution>> NewReply() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>해상도 읽기를 모르는 옛 영상 창구(기본 구현).</summary>
    private sealed class PlainVideo : IMappingCameraVideo
    {
        public MappingVideoHostState HostState => MappingVideoHostState.Running;
        public event EventHandler? HostStateChanged { add { } remove { } }
        public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height)
            => MappingCallResult<IFrameSource>.Ok(new FakeSource(streamId, width, height));
        public bool CanGotoPreset(int cameraId) => false;
        public Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default)
            => Task.FromResult(MappingCallResult<bool>.Ok(true));
    }

    private sealed class FakeControl : ICameraPopupControl
    {
        public bool IsAvailable { get; set; } = true;
        public List<CameraRequest> Requests { get; } = new();
        public Func<CameraRequest, CameraResponse> Reply { get; set; } = r => new CameraResponse
        {
            RequestId = r.RequestId, Kind = r.Kind, CameraId = r.CameraId, Success = true, VideoWidth = 1280, VideoHeight = 1024,
        };

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

    private static readonly MappingRoiPoint[] Square =
    {
        new(0.2, 0.2), new(0.6, 0.2), new(0.6, 0.6), new(0.2, 0.6),
    };

    private static async Task<(MappingRoiEditorViewModel Vm, FakeRoiServer Server)> OpenAsync(IMappingCameraVideo? video, bool withExisting = true)
    {
        var server = new FakeRoiServer();
        if (withExisting)
            server.With(3702, new MappingRoiDetail(51, 3702, "정문 앞", 1280, 720, true, null, Square));
        var vm = new MappingRoiEditorViewModel(server, 3702, "2. 후문", "CAM-370", true, video, 370, 2);
        await vm.LoadAsync();
        return (vm, server);
    }
    #endregion

    #region - 새 ROI -
    [Fact]
    public async Task should_fill_a_new_roi_with_the_camera_main_stream_resolution()
    {
        var video = new ResolutionVideo();
        var (vm, server) = await OpenAsync(video);
        var read = vm.ReadCameraResolutionAsync();
        video.Answer(1280, 1024);
        await read;

        vm.BeginNew();

        Assert.Equal(new[] { 370 }, video.Reads);
        Assert.Equal(new MappingVideoResolution(1280, 1024), vm.CameraResolution);
        Assert.Equal("1280", vm.EditWidth);           // 기존 ROI(1280×720)보다 카메라 해상도가 먼저다
        Assert.Equal("1024", vm.EditHeight);
        Assert.Equal(1.25, vm.CanvasAspect, 6);

        vm.AddPoint(0.1, 0.1);
        vm.AddPoint(0.5, 0.1);
        vm.AddPoint(0.3, 0.5);
        vm.ClosePolygon();
        await vm.SaveAsync();

        Assert.Equal((1280.0, 1024.0), (server.LastDraft!.ResolutionWidth, server.LastDraft.ResolutionHeight));
    }

    [Fact]
    public async Task should_keep_the_resolution_an_existing_roi_was_read_with()
    {
        var video = new ResolutionVideo();
        var (vm, _) = await OpenAsync(video);
        var read = vm.ReadCameraResolutionAsync();
        video.Answer(1280, 1024);
        await read;

        vm.SelectedRoi = vm.Rois[0];

        Assert.Equal("1280", vm.EditWidth);
        Assert.Equal("720", vm.EditHeight);
        Assert.Equal(1280.0 / 720.0, vm.CanvasAspect, 6);
        Assert.False(vm.CanSave);                     // 읽은 값을 바꾸지 않았다
    }

    [Fact]
    public async Task should_fall_back_to_the_existing_roi_then_full_hd_when_the_camera_resolution_is_unknown()
    {
        var refused = new ResolutionVideo();
        var (withRoi, _) = await OpenAsync(refused);
        var read = withRoi.ReadCameraResolutionAsync();
        refused.Refuse();
        await read;
        withRoi.BeginNew();

        var (empty, _) = await OpenAsync(new PlainVideo(), withExisting: false);
        await empty.ReadCameraResolutionAsync();          // 기본 구현 = 읽을 수 없음
        empty.BeginNew();

        var (noVideo, _) = await OpenAsync(null, withExisting: false);
        await noVideo.ReadCameraResolutionAsync();
        noVideo.BeginNew();

        Assert.Null(withRoi.CameraResolution);
        Assert.Equal(("1280", "720"), (withRoi.EditWidth, withRoi.EditHeight));
        Assert.Equal(("1920", "1080"), (empty.EditWidth, empty.EditHeight));
        Assert.Equal(("1920", "1080"), (noVideo.EditWidth, noVideo.EditHeight));
    }

    [Fact]
    public async Task should_apply_a_late_camera_resolution_to_a_new_roi_that_is_still_blank()
    {
        var video = new ResolutionVideo();
        var (vm, _) = await OpenAsync(video, withExisting: false);
        var read = vm.ReadCameraResolutionAsync();
        vm.BeginNew();
        Assert.Equal("1920", vm.EditWidth);             // 아직 모른다

        video.Answer(1280, 1024);
        await read;

        Assert.Equal(("1280", "1024"), (vm.EditWidth, vm.EditHeight));
        Assert.Equal(1.25, vm.CanvasAspect, 6);
    }

    [Fact]
    public async Task should_not_override_a_typed_resolution_or_a_started_drawing_with_a_late_camera_resolution()
    {
        var typedVideo = new ResolutionVideo();
        var (typed, _) = await OpenAsync(typedVideo, withExisting: false);
        var typedRead = typed.ReadCameraResolutionAsync();
        typed.BeginNew();
        typed.EditWidth = "800";
        typed.EditHeight = "600";

        var drawnVideo = new ResolutionVideo();
        var (drawn, _) = await OpenAsync(drawnVideo, withExisting: false);
        var drawnRead = drawn.ReadCameraResolutionAsync();
        drawn.BeginNew();
        Assert.True(drawn.AddPoint(0.1, 0.1));

        typedVideo.Answer(1280, 1024);
        drawnVideo.Answer(1280, 1024);
        await Task.WhenAll(typedRead, drawnRead);

        Assert.Equal(("800", "600"), (typed.EditWidth, typed.EditHeight));
        Assert.Equal(("1920", "1080"), (drawn.EditWidth, drawn.EditHeight));
        Assert.Equal(new MappingVideoResolution(1280, 1024), drawn.CameraResolution);
    }

    [Fact]
    public async Task should_not_touch_a_selected_existing_roi_when_the_camera_resolution_arrives()
    {
        var video = new ResolutionVideo();
        var (vm, _) = await OpenAsync(video);
        var read = vm.ReadCameraResolutionAsync();
        vm.SelectedRoi = vm.Rois[0];

        video.Answer(1280, 1024);
        await read;

        Assert.Equal(("1280", "720"), (vm.EditWidth, vm.EditHeight));
        Assert.False(vm.CanSave);
    }

    [Fact]
    public async Task should_ignore_a_nonsense_camera_resolution()
    {
        var video = new ResolutionVideo();
        var (vm, _) = await OpenAsync(video, withExisting: false);
        var read = vm.ReadCameraResolutionAsync();
        video.Answer(0, 1080);
        await read;
        vm.BeginNew();

        Assert.Null(vm.CameraResolution);
        Assert.Equal(("1920", "1080"), (vm.EditWidth, vm.EditHeight));
    }
    #endregion

    #region - 창 열기 · 영상 -
    [Fact]
    public async Task should_read_the_camera_resolution_when_the_window_opens_and_reshape_the_idle_video()
    {
        var video = new ResolutionVideo();
        var (vm, _) = await OpenAsync(video, withExisting: false);

        await ((IActivate)vm).ActivateAsync(CancellationToken.None);
        Assert.Equal(new[] { 370 }, video.Reads);
        Assert.Equal((960, 540), video.Opens[0]);       // 처음엔 모른다 — 16:9
        video.Answer(1280, 1024);
        await vm.ResolutionTask;

        Assert.False(vm.HasForm);
        Assert.Equal(1.25, vm.CanvasAspect, 6);         // 고른 것이 없는 빈 캔버스도 카메라 비율
        var until = DateTime.UtcNow.AddSeconds(3);
        while (video.Opens.Count < 2 && DateTime.UtcNow < until) await Task.Delay(20);
        Assert.Equal((960, 768), video.Opens[^1]);      // 영상 상자를 카메라 비율로 다시 연다(띠 없음)

        await ((IDeactivate)vm).DeactivateAsync(true, CancellationToken.None);
    }

    [Fact]
    public async Task should_not_ask_for_a_resolution_without_a_camera()
    {
        var video = new ResolutionVideo();
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, cameraId: 0);

        await vm.ReadCameraResolutionAsync();

        Assert.Empty(video.Reads);
        Assert.Null(vm.CameraResolution);
    }
    #endregion

    #region - 호스트 구현 -
    [Fact]
    public async Task should_ask_the_host_for_the_main_stream_resolution_with_the_event_window_provider()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var control = new FakeControl();
        var channel = new HostMappingCameraVideo(null, directory, control: () => control);

        var result = await channel.ReadVideoResolutionAsync(66);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(new MappingVideoResolution(1280, 1024), result.Value);
        var request = Assert.Single(control.Requests);
        Assert.Equal(CameraRequestKind.GetVideoResolution, request.Kind);
        Assert.Equal("66", request.CameraId);
        Assert.NotNull(request.Provider);
    }

    [Fact]
    public async Task should_read_the_resolution_of_a_fixed_camera_too()
    {
        var directory = new FakeDirectory();
        directory.Cameras[5] = Camera(5, ptz: false);
        var control = new FakeControl();
        var channel = new HostMappingCameraVideo(null, directory, control: () => control);

        var result = await channel.ReadVideoResolutionAsync(5);

        Assert.True(result.IsSuccess);
        Assert.Single(control.Requests);
    }

    [Fact]
    public async Task should_fail_without_throwing_when_the_resolution_cannot_be_read()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var control = new FakeControl { Reply = r => CameraResponse.Fail(r, CameraErrorCodes.Timeout, "12000 ms") };
        var channel = new HostMappingCameraVideo(null, directory, control: () => control);

        var timeout = await channel.ReadVideoResolutionAsync(66);
        var unknownCamera = await channel.ReadVideoResolutionAsync(99);
        control.Reply = r => new CameraResponse { RequestId = r.RequestId, Kind = r.Kind, Success = true };   // 크기 없는 성공
        var empty = await channel.ReadVideoResolutionAsync(66);
        control.Reply = _ => throw new InvalidOperationException("pipe");
        var thrown = await channel.ReadVideoResolutionAsync(66);
        control.IsAvailable = false;
        var down = await channel.ReadVideoResolutionAsync(66);
        var noHost = await HostMappingCameraVideo.FromHost(null, directory, null, null).ReadVideoResolutionAsync(66);

        Assert.All(new[] { timeout, unknownCamera, empty, thrown, down, noHost }, r => Assert.False(r.IsSuccess));
        Assert.Equal("영상 기능을 쓸 수 없어 카메라 해상도를 읽지 못했습니다.", down.Message);
        Assert.Equal("영상 기능을 쓸 수 없어 카메라 해상도를 읽지 못했습니다.", noHost.Message);
    }
    #endregion
}

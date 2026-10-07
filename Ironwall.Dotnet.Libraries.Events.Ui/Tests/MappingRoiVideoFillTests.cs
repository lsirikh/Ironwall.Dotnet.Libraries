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
   Purpose      : ROI 설정 창 — 영상이 기준 해상도 틀을 꽉 채운다(서브 비율이 달라도 띠 없음)
   Created By   : Claude
   Created On   : 2026-10-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MappingRoiVideoFillTests
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

    /// <summary>꽉 채우기 여부까지 적는 영상 창구.</summary>
    private sealed class FillVideo : IMappingCameraVideo
    {
        public List<(string StreamId, int Width, int Height, bool Fill)> Opens { get; } = new();
        public MappingVideoHostState HostState => MappingVideoHostState.Running;
        public event EventHandler? HostStateChanged { add { } remove { } }

        public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height)
            => Record(streamId, width, height, false);

        public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height, bool fillBox)
            => Record(streamId, width, height, fillBox);

        private MappingCallResult<IFrameSource> Record(string streamId, int width, int height, bool fill)
        {
            Opens.Add((streamId, width, height, fill));
            return MappingCallResult<IFrameSource>.Ok(new FakeSource(streamId, width, height));
        }

        public bool CanGotoPreset(int cameraId) => true;
        public Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default)
            => Task.FromResult(MappingCallResult<bool>.Ok(true));
    }

    /// <summary>꽉 채우기를 모르는 옛 창구 — 기본 구현이 그냥 연다.</summary>
    private sealed class PlainVideo : IMappingCameraVideo
    {
        public List<(int Width, int Height)> Opens { get; } = new();
        public MappingVideoHostState HostState => MappingVideoHostState.Running;
        public event EventHandler? HostStateChanged { add { } remove { } }
        public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height)
        {
            Opens.Add((width, height));
            return MappingCallResult<IFrameSource>.Ok(new FakeSource(streamId, width, height));
        }
        public bool CanGotoPreset(int cameraId) => false;
        public Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default)
            => Task.FromResult(MappingCallResult<bool>.Ok(true));
    }

    private sealed class FakeHost : ICameraPopupHost
    {
        public CameraPopupHostState State => CameraPopupHostState.Running;
        public string? StateReason => null;
        public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged { add { } remove { } }
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
    }

    private sealed class FakeDirectory : IEventWindowDeviceDirectory
    {
        public Dictionary<int, ICameraDeviceModel> Cameras { get; } = new();
        public ICameraDeviceModel? FindCamera(int cameraId) => Cameras.TryGetValue(cameraId, out var c) ? c : null;
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => null;
        public string? GroupName(int groupId) => null;
    }

    private static CameraDeviceModel Camera(int id) => new(id)
    {
        DeviceName = $"카메라 {id}",
        DeviceType = EnumDeviceType.IpCamera,
        Category = EnumCameraType.PTZ,
        IpAddress = $"10.0.0.{id}",
        IpPort = 80,
        UserName = "admin",
        UserPassword = "pw",
        Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{id}/main", RtspSub = $"rtsp://10.0.0.{id}/sub" },
    };
    #endregion

    #region - 창 -
    [Fact]
    public void should_open_the_roi_window_video_filling_the_reference_frame()
    {
        var video = new FillVideo();
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, 370, 2);

        Assert.True(vm.Video!.FillBox);
        vm.Video.Start(960, 540);

        Assert.Equal(("mapping-roi-3702", 960, 540, true), Assert.Single(video.Opens));
    }

    [Fact]
    public async Task should_keep_filling_when_the_roi_window_reopens_at_a_new_size()
    {
        var video = new FillVideo();
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, 370, 2);

        vm.Video!.Start(960, 540);
        await vm.Video.Resize(960, 768);   // 기준 해상도를 1280×1024 로 바꿨다(5:4 상자)
        vm.Video.Retry();

        Assert.Equal(3, video.Opens.Count);
        Assert.All(video.Opens, o => Assert.True(o.Fill));
        Assert.Equal((960, 768), (video.Opens[^1].Width, video.Opens[^1].Height));
        vm.Video.Dispose();
    }

    [Fact]
    public void should_leave_the_preset_manager_video_aspect_as_it_was()
    {
        var video = new FillVideo();
        var manager = new MappingPresetManagerViewModel(new FakePresetRoiServer(), 370, "CAM-370", true, null, video);

        Assert.False(manager.Video!.FillBox);
        manager.Video.Start(640, 360);

        Assert.False(Assert.Single(video.Opens).Fill);
    }

    [Fact]
    public void should_still_open_on_an_old_channel_that_does_not_know_fill()
    {
        var video = new PlainVideo();
        var vm = new MappingRoiEditorViewModel(new FakeRoiServer(), 3702, "2. 후문", "CAM-370", true, video, 370, 2);

        vm.Video!.Start(960, 540);

        Assert.Equal((960, 540), Assert.Single(video.Opens));
        Assert.NotNull(vm.Video.FrameSource);
    }
    #endregion

    #region - 호스트 구현 -
    [Fact]
    public void should_send_fill_box_to_the_host_only_for_the_roi_window()
    {
        var host = new FakeHost();
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var channel = new HostMappingCameraVideo(host, directory);

        channel.OpenVideo(66, "mapping-roi-9", 960, 540, fillBox: true);
        channel.OpenVideo(66, "mapping-presets-66", 640, 360);
        ((IMappingCameraVideo)channel).OpenVideo(66, "mapping-presets-66", 640, 360, fillBox: false);

        Assert.Equal(3, host.Overlays.Count);
        Assert.True(host.Overlays[0].Provider.FillBox);
        Assert.Equal((960, 540), (host.Overlays[0].Width, host.Overlays[0].Height));
        // 꽉 채우기 사본도 제공자 정보(종류 · 주소 · 계정)는 그대로.
        var (a, b) = (host.Overlays[0].Provider, host.Overlays[1].Provider);
        Assert.Equal((b.Kind, b.Uri, b.Host, b.Port, b.Username, b.Password, b.PreferSubStream, b.FallbackUri),
                     (a.Kind, a.Uri, a.Host, a.Port, a.Username, a.Password, a.PreferSubStream, a.FallbackUri));
        Assert.False(host.Overlays[1].Provider.FillBox);
        Assert.False(host.Overlays[2].Provider.FillBox);
    }
    #endregion
}

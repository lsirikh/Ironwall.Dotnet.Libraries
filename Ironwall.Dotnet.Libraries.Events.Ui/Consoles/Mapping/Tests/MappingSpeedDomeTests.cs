using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.Tests;
/****************************************************************************
   Purpose      : G14 — 형태 코드 SPEED_DOME 카메라(Category=NONE)도 PTZ 로 다룬다(프리셋 읽기 · 이동 · 이벤트 창 타일)
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class MappingSpeedDomeTests
{
    private sealed class Directory : IEventWindowDeviceDirectory
    {
        public Dictionary<int, ICameraDeviceModel> Cameras { get; } = new();
        public ICameraDeviceModel? FindCamera(int cameraId) => Cameras.TryGetValue(cameraId, out var c) ? c : null;
        public string? DeviceName(int deviceId, EnumDeviceType deviceType) => null;
        public string? GroupName(int groupId) => null;
    }

    private sealed class Control : ICameraPopupControl
    {
        public bool IsAvailable => true;
        public List<CameraRequest> Requests { get; } = new();
        public Task<CameraResponse> RequestAsync(CameraRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new CameraResponse
            {
                RequestId = request.RequestId, Kind = request.Kind, Success = true,
                Presets = new() { new CameraPreset { Token = "1", Name = "정문" } },
            });
        }
        public bool Move(string cameraId, VideoProviderInfo provider, double pan, double tilt, double zoom) => true;
        public bool Stop(string cameraId, VideoProviderInfo provider) => true;
        public bool DragMove(string cameraId, VideoProviderInfo provider, double viewX, double viewY, double viewAspect) => true;
        public bool Focus(string cameraId, VideoProviderInfo provider, int direction) => true;
        public void Dispose() { }
    }

    private sealed class Host : ICameraPopupHost
    {
        public CameraPopupHostState State => CameraPopupHostState.Running;
        public string? StateReason => null;
        public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived { add { } remove { } }
        public void Start() { }
        public void Restart() { }
        public IFrameSource? OpenOverlay(OverlayStreamRequest request) => null;
        public void CloseOverlay(string streamId) { }
        public void OpenEventWindow(OpenEventWindow request) { }
        public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
        public void BringEventWindowToFront(string eventKey) { }
        public void SetTheme(string theme) { }
        public void SendPtz(PtzCommand command) { }
        public void Dispose() { }
    }

    /// <summary>7.0+ 서버가 보낸 스피드돔 — enum 에 값이 없어 Category 는 NONE, 형태 코드만 SPEED_DOME.</summary>
    private static CameraDeviceModel SpeedDome(int id, string code = "SPEED_DOME") => new(id)
    {
        DeviceName = $"스피드돔 {id}",
        DeviceType = EnumDeviceType.IpCamera,
        Category = EnumCameraType.NONE,
        TypeAxisCode = code,
        IpAddress = $"10.0.0.{id}",
        IpPort = 80,
        UserName = "admin",
        UserPassword = "pw",
        Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{id}/main", RtspSub = $"rtsp://10.0.0.{id}/sub" },
    };

    [Fact]
    public async Task 스피드돔은_카메라_프리셋을_읽는다()
    {
        var directory = new Directory();
        directory.Cameras[70] = SpeedDome(70);
        var control = new Control();
        var reader = new HostMappingCameraPresetReader(() => control, directory);

        var result = await reader.ReadCameraPresetsAsync(70);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Single(control.Requests);
    }

    [Fact]
    public async Task 스피드돔은_프리셋_자리로_이동할_수_있다()
    {
        var directory = new Directory();
        directory.Cameras[70] = SpeedDome(70, "speed_dome");
        var control = new Control();
        var channel = new HostMappingCameraVideo(new Host(), directory, control: () => control);

        Assert.True(channel.CanGotoPreset(70));
        var moved = await channel.GotoPresetAsync(70, 2);
        Assert.True(moved.IsSuccess, moved.Message);
        Assert.Equal("2", Assert.Single(control.Requests).PresetToken);
    }

    [Fact]
    public void 스피드돔_이벤트_창_타일은_PTZ_와_프리셋을_싣는다()
    {
        var entry = new MappingCameraEntry(70, 1, 11, 7, true, TargetPresetIndex: 2, TargetPresetName: "정문", HomePresetIndex: 1);

        var tile = EventWindowPlanning.BuildCamera(SpeedDome(70), entry, new CameraPopupSettings().Normalize(), ptzAllowed: true);

        Assert.True(tile.IsPtz);
        Assert.Equal(("2", "1"), (tile.TargetPresetToken, tile.HomePresetToken));
    }

    [Fact]
    public async Task 형태_코드가_고정이면_여전히_PTZ_가_아니다()
    {
        var directory = new Directory();
        directory.Cameras[71] = SpeedDome(71, "FIXED");
        var control = new Control();
        var channel = new HostMappingCameraVideo(new Host(), directory, control: () => control);
        var reader = new HostMappingCameraPresetReader(() => control, directory);

        Assert.False(channel.CanGotoPreset(71));
        Assert.False((await reader.ReadCameraPresetsAsync(71)).IsSuccess);
        Assert.Empty(control.Requests);
    }
}

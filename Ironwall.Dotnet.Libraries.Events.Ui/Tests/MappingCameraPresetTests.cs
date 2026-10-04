using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
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
   Purpose      : 프리셋 관리 창 — 카메라 ONVIF 프리셋 읽기 · 서버에 담기 · 불일치 경고
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MappingCameraPresetTests
{
    #region - 가짜 -
    private sealed class FakeReader : IMappingCameraPresetReader
    {
        public MappingCallResult<IReadOnlyList<MappingCameraPreset>> Result { get; set; }
            = MappingCallResult<IReadOnlyList<MappingCameraPreset>>.Ok(Array.Empty<MappingCameraPreset>());

        public int Calls;

        public FakeReader With(params (string Token, string Name)[] presets)
        {
            Result = MappingCallResult<IReadOnlyList<MappingCameraPreset>>.Ok(presets.Select(p => new MappingCameraPreset(p.Token, p.Name)).ToList());
            return this;
        }

        public async Task<MappingCallResult<IReadOnlyList<MappingCameraPreset>>> ReadCameraPresetsAsync(int cameraId, CancellationToken token = default)
        {
            Interlocked.Increment(ref Calls);
            await Task.Yield();
            return Result;
        }
    }

    private sealed class FakeControl : ICameraPopupControl
    {
        public bool IsAvailable { get; set; } = true;
        public List<CameraRequest> Requests { get; } = new();
        public Func<CameraRequest, CameraResponse> Reply { get; set; } = r => new CameraResponse { RequestId = r.RequestId, Kind = r.Kind, Success = true, Presets = new() };

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
        Urls = new CameraUrlsModel { RtspMain = $"rtsp://10.0.0.{id}/main" },
    };

    private static async Task<(MappingPresetManagerViewModel Vm, FakePresetServer Server, FakeReader Reader)> OpenAsync(
        FakeReader reader, bool canEdit = true)
    {
        var server = new FakePresetServer().With(370, (3701, 1, "정문"), (3702, 2, "후문"));
        var vm = new MappingPresetManagerViewModel(server, 370, "CAM-370", canEdit, reader);
        await vm.LoadAsync();
        await vm.ReadCameraPresetsAsync();
        return (vm, server, reader);
    }
    #endregion

    #region - 토큰 → 번호 -
    [Theory]
    [InlineData("1", 1)]
    [InlineData("12", 12)]
    [InlineData(" 3 ", 3)]
    [InlineData("0", null)]
    [InlineData("01", null)]
    [InlineData("Preset_1", null)]
    [InlineData("-2", null)]
    [InlineData("", null)]
    public void should_use_only_plain_positive_integer_tokens_as_server_index(string token, int? expected)
        => Assert.Equal(expected, new MappingCameraPreset(token, "x").Index);
    #endregion

    #region - 관리 창 -
    [Fact]
    public async Task should_list_camera_presets_and_mark_the_ones_already_on_the_server()
    {
        var (vm, _, _) = await OpenAsync(new FakeReader().With(("1", "정문"), ("3", "초소"), ("Preset_A", "옥상")));

        Assert.True(vm.HasCameraPresetReader);
        Assert.Equal(new[] { "1", "3", "Preset_A" }, vm.CameraPresets.Select(r => r.Preset.Token));
        Assert.Equal(new[] { true, false, false }, vm.CameraPresets.Select(r => r.IsRegistered));
        Assert.Equal("번호가 아닌 토큰 — 등록 불가", vm.CameraPresets[2].Subtitle);
    }

    [Fact]
    public async Task should_warn_about_server_presets_whose_index_is_not_on_the_camera()
    {
        var (vm, _, _) = await OpenAsync(new FakeReader().With(("1", "정문"), ("3", "초소")));

        Assert.True(vm.HasCameraMismatch);
        Assert.Contains("카메라에 없는 번호: 2", vm.CameraMismatchText);
    }

    [Fact]
    public async Task should_not_judge_mismatch_when_the_camera_could_not_be_read()
    {
        var reader = new FakeReader
        {
            Result = MappingCallResult<IReadOnlyList<MappingCameraPreset>>.Fail("카메라가 응답하지 않아 카메라 프리셋을 읽지 못했습니다."),
        };
        var (vm, _, _) = await OpenAsync(reader);

        Assert.False(vm.HasCameraMismatch);
        Assert.Empty(vm.CameraPresets);
        Assert.Equal("카메라가 응답하지 않아 카메라 프리셋을 읽지 못했습니다.", vm.CameraPresetStatus);
    }

    [Fact]
    public async Task should_say_the_camera_has_no_presets_rather_than_staying_silent()
    {
        var (vm, _, _) = await OpenAsync(new FakeReader());

        Assert.Equal("카메라에 기억된 프리셋이 없습니다.", vm.CameraPresetStatus);
        Assert.True(vm.HasCameraMismatch);   // 서버 1 · 2 번이 카메라에 없다
    }

    [Fact]
    public async Task should_create_a_server_preset_from_a_camera_preset_with_its_token_and_name()
    {
        var (vm, server, _) = await OpenAsync(new FakeReader().With(("1", "정문"), ("3", "초소")));

        vm.UseCameraPreset(vm.CameraPresets.Single(r => r.Preset.Token == "3"));
        Assert.True(vm.IsNew);
        Assert.Equal("3", vm.EditIndex);
        Assert.Equal("초소", vm.EditName);
        await vm.SaveAsync();

        Assert.Contains("create:370:3:초소", server.Calls);
        Assert.True(vm.CameraPresets.Single(r => r.Preset.Token == "3").IsRegistered);
    }

    [Fact]
    public async Task should_select_the_existing_server_preset_instead_of_duplicating_it()
    {
        var (vm, server, _) = await OpenAsync(new FakeReader().With(("1", "정문")));

        vm.UseCameraPreset(vm.CameraPresets.Single());

        Assert.False(vm.IsNew);
        Assert.Equal(3701, vm.SelectedPreset?.Id);
        Assert.DoesNotContain(server.Calls, c => c.StartsWith("create:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task should_refuse_a_non_numeric_camera_token_with_a_warning()
    {
        var (vm, _, _) = await OpenAsync(new FakeReader().With(("Preset_A", "옥상")));

        vm.UseCameraPreset(vm.CameraPresets.Single());

        Assert.False(vm.HasForm);
        Assert.Equal(DialogMessageSeverity.Warning, vm.MessageSeverity);
        Assert.Contains("Preset_A", vm.Message);
    }

    [Fact]
    public async Task should_not_fill_the_form_without_edit_permission()
    {
        var (vm, _, _) = await OpenAsync(new FakeReader().With(("3", "초소")), canEdit: false);

        vm.UseCameraPreset(vm.CameraPresets.Single());

        Assert.False(vm.HasForm);
    }

    [Fact]
    public void should_hide_the_camera_section_without_a_reader()
    {
        var vm = new MappingPresetManagerViewModel(new FakePresetServer(), 370, "CAM-370");

        Assert.False(vm.HasCameraPresetReader);
        Assert.False(vm.CanReadCameraPresets);
    }

    [Fact]
    public async Task should_pass_the_workbench_reader_to_the_preset_manager()
    {
        var reader = new FakeReader().With(("1", "정문"), ("4", "초소"));
        var gateway = new CountingGateway();
        gateway.Cameras.Add(new MappingCameraReadDto
        {
            ConfigId = 700, EventMappingId = 1, DelayTime = 3, IsEnable = true, UpdatedAt = "T0",
            Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        });
        var server = new FakePresetServer().With(370, (3701, 1, "정문"));
        var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: server, presetEditor: server)
        {
            CameraPresetReader = reader,
        };
        await vm.ReloadAsync();
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows[0]);
        vm.OnSelectionChanged();
        await vm.PresetLoadTask;

        MappingPresetManagerViewModel? opened = null;
        vm.ShowPresetManager = async manager =>
        {
            opened = manager;
            await manager.LoadAsync();
            await manager.ReadCameraPresetsAsync();
        };
        await vm.ManagePresetsAsync();

        Assert.True(opened?.HasCameraPresetReader);
        Assert.Equal(new[] { "1", "4" }, opened!.CameraPresets.Select(r => r.Preset.Token));
        Assert.Equal(1, reader.Calls);
    }
    #endregion

    #region - 호스트 리더 -
    [Fact]
    public async Task should_ask_the_host_for_presets_with_the_event_window_provider()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var control = new FakeControl
        {
            Reply = r => new CameraResponse
            {
                RequestId = r.RequestId, Kind = r.Kind, Success = true,
                Presets = new() { new CameraPreset { Token = "1", Name = "정문" }, new CameraPreset { Token = " ", Name = "빈 토큰" } },
            },
        };
        var reader = new HostMappingCameraPresetReader(() => control, directory);

        var result = await reader.ReadCameraPresetsAsync(66);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "1" }, result.Value!.Select(p => p.Token));
        var request = Assert.Single(control.Requests);
        Assert.Equal(CameraRequestKind.GetPresets, request.Kind);
        Assert.Equal("66", request.CameraId);
        Assert.NotNull(request.Provider);
    }

    [Fact]
    public async Task should_fail_with_a_reason_when_the_host_times_out()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var control = new FakeControl { Reply = r => CameraResponse.Fail(r, CameraErrorCodes.Timeout, "12000 ms") };
        var reader = new HostMappingCameraPresetReader(() => control, directory);

        var result = await reader.ReadCameraPresetsAsync(66);

        Assert.False(result.IsSuccess);
        Assert.Equal("카메라가 응답하지 않아 카메라 프리셋을 읽지 못했습니다.", result.Message);
    }

    [Fact]
    public async Task should_not_ask_the_host_for_a_fixed_camera_or_an_unknown_camera()
    {
        var directory = new FakeDirectory();
        directory.Cameras[5] = Camera(5, ptz: false);
        var control = new FakeControl();
        var reader = new HostMappingCameraPresetReader(() => control, directory);

        var fixedCam = await reader.ReadCameraPresetsAsync(5);
        var unknown = await reader.ReadCameraPresetsAsync(99);

        Assert.False(fixedCam.IsSuccess);
        Assert.False(unknown.IsSuccess);
        Assert.Empty(control.Requests);
    }

    [Fact]
    public async Task should_fail_without_throwing_when_there_is_no_host()
    {
        var directory = new FakeDirectory();
        directory.Cameras[66] = Camera(66);
        var reader = HostMappingCameraPresetReader.FromHost(null, directory, null, null);

        var result = await reader.ReadCameraPresetsAsync(66);

        Assert.False(result.IsSuccess);
        Assert.Equal("영상 기능을 쓸 수 없어 카메라 프리셋을 읽지 못했습니다.", result.Message);
    }
    #endregion
}

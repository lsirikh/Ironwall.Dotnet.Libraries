using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 스피커 방송서버(server_id) 매핑 비대칭(쓰기 server_id ↔ 읽기 nested server) + DeviceEquals 회귀 가드.
/// PRD SpeakerServerAssignment v1.1 P1 — BLOCKER B1/B3.
/// </summary>
public class SpeakerServerMappingTests
{
    private static SpeakerDeviceModel MakeSpeaker(int? serverId)
        => new()
        {
            DeviceNumber = 1,
            DeviceName = "스피커_01",
            SpeakerType = "NORMAL",
            Description = "테스트",
            Server = serverId.HasValue ? new ServerModel { Id = serverId.Value, Name = $"서버{serverId}" } : null,
        };

    [Fact]
    public void should_write_server_id_when_model_has_server()
    {
        var dto = MakeSpeaker(serverId: 5).ToSpeakerDeviceDto();

        Assert.Equal(5, dto.ServerId);
    }

    [Fact]
    public void should_have_null_server_id_when_model_has_no_server()
    {
        var dto = MakeSpeaker(serverId: null).ToSpeakerDeviceDto();

        Assert.Null(dto.ServerId);
    }

    [Fact]
    public void should_not_serialize_nested_server_on_write()
    {
        // BLOCKER-1: 쓰기 본문에는 server_id만 — nested server 객체(민감정보 포함) 미전송
        var dto = MakeSpeaker(serverId: 5).ToSpeakerDeviceDto();
        dto.Server = new ServerDto { Id = 5, Name = "서버5", UserPassword = "secret" };

        var json = JsonConvert.SerializeObject(dto);

        Assert.Contains("\"server_id\"", json);
        Assert.DoesNotContain("\"server\":", json);  // nested 객체 직렬화 차단(ShouldSerializeServer)
        Assert.DoesNotContain("secret", json);
    }

    [Fact]
    public void should_omit_server_id_from_json_when_null()
    {
        var dto = MakeSpeaker(serverId: null).ToSpeakerDeviceDto();

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("server_id", json);  // NullValueHandling.Ignore
    }

    [Fact]
    public void should_map_nested_server_to_model_on_read()
    {
        var dto = new SpeakerDeviceDto
        {
            Id = 10,
            NumberDevice = 1,
            NameDevice = "스피커_01",
            Server = new ServerDto { Id = 7, Name = "방송서버A", IpAddress = "10.0.0.7", Port = 8080 }
        };

        var model = dto.ToSpeakerDeviceModel();

        Assert.NotNull(model.Server);
        Assert.Equal(7, model.Server!.Id);
        Assert.Equal("방송서버A", model.Server.Name);
    }

    [Fact]
    public void should_map_server_dto_to_server_model()
    {
        var dto = new ServerDto { Id = 3, CategoryId = 1, Name = "서버3", IpAddress = "10.0.0.3", Port = 9000 };

        var model = dto.ToServerModel();

        Assert.Equal(3, model.Id);
        Assert.Equal("서버3", model.Name);
        Assert.Equal(9000, model.Port);
    }

    // ───────── DeviceEquals server 변경 감지 (B3) ─────────
    [Fact]
    public void should_detect_change_when_server_differs()
    {
        Assert.False(SpeakerDevicePanelViewModel.DeviceEquals(MakeSpeaker(5), MakeSpeaker(9)));
    }

    [Fact]
    public void should_detect_change_when_server_assigned_or_cleared()
    {
        Assert.False(SpeakerDevicePanelViewModel.DeviceEquals(MakeSpeaker(null), MakeSpeaker(5)));
        Assert.False(SpeakerDevicePanelViewModel.DeviceEquals(MakeSpeaker(5), MakeSpeaker(null)));
    }

    [Fact]
    public void should_be_equal_when_server_same()
    {
        Assert.True(SpeakerDevicePanelViewModel.DeviceEquals(MakeSpeaker(5), MakeSpeaker(5)));
        Assert.True(SpeakerDevicePanelViewModel.DeviceEquals(MakeSpeaker(null), MakeSpeaker(null)));
    }
}

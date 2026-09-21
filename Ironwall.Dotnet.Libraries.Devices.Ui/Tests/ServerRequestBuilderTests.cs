using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Newtonsoft.Json.Linq;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 편집 폼의 지역 검사 검증 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 본문 조립은 <c>Devices.Api</c> 의 계약 테스트가 잠근다(<c>ServerAxisContractTests</c>).
/// 여기서는 <b>보내기 전에 화면이 막는 것</b>만 본다.
/// </summary>
public class ServerRequestBuilderTests
{
    private static ServerAxisView Fetched() => new()
    {
        Id = 12,
        TypeServer = "SPEAKER_API",
        Name = "방송서버",
        Status = "ERROR",
        HasStatusKey = true,
        IpAddress = "10.0.0.5",
        Port = 8080,
        Hostname = "bcast-01",
        UserName = "admin",
        UnitId = 4,
        Thresholds = JObject.FromObject(new
        {
            cpu = new { warning = 70.0, critical = 90.0 },
            ram = new { warning = 75.0, critical = 92.0 },
        }),
        Modes = JObject.FromObject(new { operation_mode = "NORMAL", windy_mode = "wind0" }),
    };

    [Fact]
    public void should_accept_an_untouched_form_when_the_fetched_values_are_valid()
        => Assert.Empty(ServerRequestBuilder.Validate(new ServerWriteIntent(), Fetched()));

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void should_refuse_a_port_outside_the_valid_range(int port)
    {
        var errors = ServerRequestBuilder.Validate(new ServerWriteIntent { Port = port }, Fetched());
        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.PortKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void should_accept_a_port_at_the_boundary(int port)
        => Assert.Empty(ServerRequestBuilder.Validate(new ServerWriteIntent { Port = port }, Fetched()));

    [Fact]
    public void should_refuse_an_empty_name_or_address()
    {
        var errors = ServerRequestBuilder.Validate(new ServerWriteIntent { Name = "  ", IpAddress = string.Empty }, Fetched());

        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.NameKey);
        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.IpKey);
    }

    [Fact]
    public void should_refuse_a_create_form_that_is_still_empty()
    {
        var errors = ServerRequestBuilder.Validate(new ServerWriteIntent(), fetched: null);

        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.NameKey);
        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.IpKey);
        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.PortKey);
    }

    [Fact]
    public void should_refuse_clearing_a_field_when_the_contract_cannot_send_a_deletion()
    {
        var intent = new ServerWriteIntent { ClearHostname = true, ClearUserName = true };

        var legacy = ServerRequestBuilder.Validate(intent, Fetched(), EnumServerContract.V6_3);
        Assert.Contains(legacy, e => e.Key == ServerRequestBuilder.HostnameKey);
        Assert.Contains(legacy, e => e.Key == ServerRequestBuilder.UserNameKey);
        Assert.All(legacy, e => Assert.Contains("6.3", e.Message));

        Assert.Empty(ServerRequestBuilder.Validate(intent, Fetched(), EnumServerContract.V8_0));
    }

    [Fact]
    public void should_refuse_a_warning_that_is_not_below_the_critical_threshold()
    {
        // 서버도 같은 것을 본다(app/schemas/server.py:245-250 — 같아도 422).
        var errors = ServerRequestBuilder.Validate(new ServerWriteIntent { CpuWarning = 95 }, Fetched());
        Assert.Contains(errors, e => e.Key == "threshold.cpu");
    }

    [Fact]
    public void should_compare_a_touched_warning_against_the_fetched_critical()
    {
        Assert.Empty(ServerRequestBuilder.Validate(new ServerWriteIntent { CpuWarning = 65 }, Fetched()));
        Assert.Contains(
            ServerRequestBuilder.Validate(new ServerWriteIntent { RamCritical = 70 }, Fetched()),
            e => e.Key == "threshold.ram");
    }

    [Fact]
    public void should_refuse_a_network_warning_that_is_not_below_the_critical()
    {
        var errors = ServerRequestBuilder.Validate(
            new ServerWriteIntent { NetworkWarningMbps = 900, NetworkCriticalMbps = 500 }, Fetched());
        Assert.Contains(errors, e => e.Key == "threshold.network");
    }

    [Fact]
    public void should_read_a_threshold_value_when_the_shape_matches()
    {
        var thresholds = Fetched().Thresholds;
        Assert.Equal(70.0, ServerRequestBuilder.ReadThreshold(thresholds, "cpu", "warning"));
        Assert.Null(ServerRequestBuilder.ReadThreshold(thresholds, "disk", "warning"));
        Assert.Null(ServerRequestBuilder.ReadThreshold(null, "cpu", "warning"));
    }

    [Fact]
    public void should_read_a_mode_value_when_the_axis_sent_one()
    {
        Assert.Equal("NORMAL", ServerRequestBuilder.ReadMode(Fetched().Modes, "operation_mode"));
        Assert.Null(ServerRequestBuilder.ReadMode(Fetched().Modes, "no_such_key"));
        Assert.Null(ServerRequestBuilder.ReadMode(null, "operation_mode"));
    }
}

/// <summary>
/// 7.0+ 응답에는 중첩 <c>server</c> 가 없다 — <c>server_id</c> 만 와도 소속을 잃지 않는가
/// (서버 <c>app/routers/speakers.py</c> 머리말 D4).
/// </summary>
public class SpeakerServerIdMappingTests
{
    [Fact]
    public void should_seed_the_server_reference_when_only_the_id_arrived()
    {
        var dto = new Ironwall.Dotnet.Libraries.Messages.Dto.Devices.SpeakerDeviceDto
        {
            Id = 5,
            NumberDevice = 1,
            NameDevice = "스피커",
            ServerId = 12,
            Server = null,
        };

        var model = Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.DtoToModelHelper.ToSpeakerDeviceModel(dto);

        Assert.NotNull(model.Server);
        Assert.Equal(12, model.Server!.Id);
        Assert.Equal(12, ServerDropRules.ServerIdOf(model));
    }

    [Fact]
    public void should_prefer_the_nested_server_when_the_expansion_was_requested()
    {
        var dto = new Ironwall.Dotnet.Libraries.Messages.Dto.Devices.SpeakerDeviceDto
        {
            Id = 5,
            NameDevice = "스피커",
            ServerId = 12,
            Server = new Ironwall.Dotnet.Libraries.Messages.Dto.Devices.ServerDto { Id = 12, Name = "방송서버" },
        };

        var model = Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.DtoToModelHelper.ToSpeakerDeviceModel(dto);

        Assert.Equal("방송서버", model.Server!.Name);
    }

    [Fact]
    public void should_leave_the_server_empty_when_the_device_has_none()
    {
        var dto = new Ironwall.Dotnet.Libraries.Messages.Dto.Devices.SpeakerDeviceDto { Id = 5, NameDevice = "스피커" };

        Assert.Null(Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.DtoToModelHelper.ToSpeakerDeviceModel(dto).Server);
    }
}

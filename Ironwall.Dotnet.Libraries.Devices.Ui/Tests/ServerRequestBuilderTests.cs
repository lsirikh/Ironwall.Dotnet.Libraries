using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 쓰기 본문 검증 — 관측 필드 금지 · null 로 지우지 않기 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public class ServerRequestBuilderTests
{
    private static ServerDto Fetched() => new()
    {
        Id = 12,
        CategoryId = 3,
        Name = "방송서버",
        Status = "ERROR",
        IpAddress = "10.0.0.5",
        Port = 8080,
        Hostname = "bcast-01",
        UserName = "admin",
        UserPassword = "secret-from-server",
        UnitId = 4,
        CreatedAt = "2026-01-01T00:00:00+09:00",
        UpdatedAt = "2026-09-20T09:00:00+09:00",
        ThresholdConfig = JObject.FromObject(new
        {
            cpu = new { warning = 70.0, critical = 90.0 },
            ram = new { warning = 75.0, critical = 92.0 },
        }),
    };

    private static JObject Body(ServerPatchDto dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    [Fact]
    public void should_never_send_the_observed_status_when_a_patch_body_is_built()
    {
        var body = Body(ServerRequestBuilder.BuildPatch(Fetched(), new ServerEditDraft(), EnumServerContract.V8_0));

        // status 는 관측 필드다 — 실리는 순간 7.0 이 422(OBSERVED_FIELD) 로 거부한다.
        Assert.False(body.ContainsKey("status"));
        Assert.False(body.ContainsKey("id"));
        Assert.False(body.ContainsKey("created_at"));
        Assert.False(body.ContainsKey("updated_at"));
    }

    [Fact]
    public void should_never_null_or_reset_a_fetched_value_when_nothing_was_edited()
    {
        var fetched = Fetched();
        var body = Body(ServerRequestBuilder.BuildPatch(fetched, new ServerEditDraft(), EnumServerContract.V8_0));

        // PATCH 는 RFC 7396 이다: 키가 null 이면 서버가 그 값을 지운다. 하나도 null 이어서는 안 된다.
        Assert.DoesNotContain(body.Properties(), p => p.Value.Type == JTokenType.Null);

        Assert.Equal(fetched.Name, (string?)body["name"]);
        Assert.Equal(fetched.IpAddress, (string?)body["ip_address"]);
        Assert.Equal(fetched.Port, (int?)body["port"]);
        Assert.Equal(fetched.Hostname, (string?)body["hostname"]);
        Assert.Equal(fetched.UserName, (string?)body["user_name"]);
        Assert.Equal(fetched.CategoryId, (int?)body["category_id"]);
        Assert.Equal(90.0, (double?)body["threshold_config"]!["cpu"]!["critical"]);
    }

    [Fact]
    public void should_drop_the_key_instead_of_sending_null_when_the_server_never_sent_it()
    {
        // 목록 기본 프로필(basic)은 계정·임계를 아예 주지 않는다 — 그 상태로 저장해도 서버 값이 지워지면 안 된다.
        var sparse = new ServerDto { Id = 1, CategoryId = 2, Name = "a", IpAddress = "1.1.1.1", Port = 1 };
        var body = Body(ServerRequestBuilder.BuildPatch(sparse, new ServerEditDraft(), EnumServerContract.V8_0));

        Assert.False(body.ContainsKey("hostname"));
        Assert.False(body.ContainsKey("user_name"));
        Assert.False(body.ContainsKey("user_password"));
        Assert.False(body.ContainsKey("threshold_config"));
    }

    [Fact]
    public void should_audit_every_writable_property_when_a_patch_body_is_built()
    {
        // 감사: 받은 값이 있는 속성은 본문에도 같은 값으로 남거나, 의도적으로 빠진 것(관측·서버 소유)이어야 한다.
        var fetched = Fetched();
        var body = ServerRequestBuilder.BuildPatch(fetched, new ServerEditDraft(), EnumServerContract.V8_0);
        var intentionallyDropped = ServerPatchDtoConverter.IntentionallyDropped;

        foreach (var property in ServerRequestBuilder.WritableProperties(typeof(ServerDto)))
        {
            var original = property.GetValue(fetched);
            var written = property.GetValue(body);

            if (intentionallyDropped.Contains(property.Name)) continue;
            Assert.True(Equals(original?.ToString(), written?.ToString()),
                $"{property.Name} 이(가) 본문에서 바뀌었습니다: {original} → {written}");
        }
    }

    [Fact]
    public void should_apply_only_the_edited_fields_when_a_draft_is_given()
    {
        var draft = new ServerEditDraft { Name = "  새 이름  ", Port = 9090 };
        var body = Body(ServerRequestBuilder.BuildPatch(Fetched(), draft, EnumServerContract.V8_0));

        Assert.Equal("새 이름", (string?)body["name"]);
        Assert.Equal(9090, (int?)body["port"]);
        Assert.Equal("10.0.0.5", (string?)body["ip_address"]);   // 손대지 않은 칸은 그대로
    }

    [Fact]
    public void should_keep_the_server_password_when_the_user_typed_nothing()
    {
        var body = Body(ServerRequestBuilder.BuildPatch(Fetched(), new ServerEditDraft(), EnumServerContract.V8_0));
        Assert.Equal("secret-from-server", (string?)body["user_password"]);

        var changed = Body(ServerRequestBuilder.BuildPatch(Fetched(), new ServerEditDraft { NewPassword = "new-one" }, EnumServerContract.V8_0));
        Assert.Equal("new-one", (string?)changed["user_password"]);
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3, false)]
    [InlineData(EnumServerContract.V7_0, false)]
    [InlineData(EnumServerContract.V8_0, true)]
    public void should_send_unit_id_only_when_the_contract_has_the_unit_axis(EnumServerContract contract, bool expected)
    {
        var body = Body(ServerRequestBuilder.BuildPatch(Fetched(), new ServerEditDraft(), contract));
        Assert.Equal(expected, body.ContainsKey("unit_id"));
    }

    [Fact]
    public void should_merge_thresholds_into_the_fetched_object_when_one_value_is_edited()
    {
        var body = ServerRequestBuilder.BuildThresholds(Fetched().ThresholdConfig, new ServerEditDraft { CpuWarning = 55 });

        Assert.Equal(55.0, (double?)body!["cpu"]!["warning"]);
        Assert.Equal(90.0, (double?)body["cpu"]!["critical"]);    // 손대지 않은 값이 살아 있다
        Assert.Equal(92.0, (double?)body["ram"]!["critical"]);
    }

    [Fact]
    public void should_return_the_fetched_thresholds_untouched_when_nothing_was_edited()
    {
        var fetched = Fetched().ThresholdConfig;
        Assert.Same(fetched, ServerRequestBuilder.BuildThresholds(fetched, new ServerEditDraft()));
        Assert.Null(ServerRequestBuilder.BuildThresholds(null, new ServerEditDraft()));
    }

    [Fact]
    public void should_create_the_network_group_when_it_was_missing()
    {
        var body = ServerRequestBuilder.BuildThresholds(null, new ServerEditDraft { NetworkWarningMbps = 500 });
        Assert.Equal(500.0, (double?)body!["network"]!["warning_mbps"]);
    }

    [Fact]
    public void should_read_a_threshold_value_when_the_shape_matches()
    {
        var thresholds = Fetched().ThresholdConfig;
        Assert.Equal(70.0, ServerRequestBuilder.ReadThreshold(thresholds, "cpu", "warning"));
        Assert.Null(ServerRequestBuilder.ReadThreshold(thresholds, "disk", "warning"));
        Assert.Null(ServerRequestBuilder.ReadThreshold(null, "cpu", "warning"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void should_refuse_a_port_outside_the_valid_range(int port)
    {
        var errors = ServerRequestBuilder.Validate(new ServerEditDraft { Port = port }, Fetched());
        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.PortKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void should_accept_a_port_at_the_boundary(int port)
        => Assert.Empty(ServerRequestBuilder.Validate(new ServerEditDraft { Port = port }, Fetched()));

    [Fact]
    public void should_refuse_an_empty_name_or_address()
    {
        var errors = ServerRequestBuilder.Validate(new ServerEditDraft { Name = "  ", IpAddress = string.Empty }, Fetched());

        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.NameKey);
        Assert.Contains(errors, e => e.Key == ServerRequestBuilder.IpKey);
    }

    [Fact]
    public void should_copy_every_fetched_value_and_change_only_the_server_id_when_a_speaker_is_assigned()
    {
        var fetched = new SpeakerDeviceDto
        {
            Id = 5,
            NumberDevice = 101,
            NameDevice = "스피커1",
            Status = "ACTIVATED",
            IsEnable = true,
            Version = "1.2.3",
            SpeakerType = "NORMAL",
            Description = "정문",
            ServerId = 9,
            Server = new ServerDto { Id = 9, Name = "옛 서버" },
        };

        var body = ServerRequestBuilder.BuildSpeakerAssign(fetched, 12);

        Assert.Equal(12, body.ServerId);
        foreach (var property in ServerRequestBuilder.WritableProperties(typeof(SpeakerDeviceDto)))
        {
            if (property.Name == nameof(SpeakerDeviceDto.ServerId)) continue;
            Assert.True(Equals(property.GetValue(fetched)?.ToString(), property.GetValue(body)?.ToString()),
                $"{property.Name} 이(가) 배정 본문에서 바뀌었습니다");
        }
    }

    [Fact]
    public void should_throw_when_the_assign_target_is_not_saved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ServerRequestBuilder.BuildSpeakerAssign(new SpeakerDeviceDto(), 0));
        Assert.Throws<ArgumentNullException>(() => ServerRequestBuilder.BuildSpeakerAssign(null!, 1));
    }
}

using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// 서버 쓰기·읽기가 <b>판본마다 다른 계약</b>을 지키는가 — 서버에 붙지 않는 계약 테스트.
/// </summary>
/// <remarks>
/// 근거(읽기 전용 서버 소스 실측):
/// <list type="bullet">
/// <item><c>app/schemas/server.py:59-66</c> <c>SERVER_REMOVED_FIELDS</c> — <c>category_id→category_server</c> ·
///   <c>port→connection.ip_port</c> · <c>hostname→connection.hostname</c> ·
///   <c>threshold_config→server_config.thresholds</c> · <c>proxy_settings→server_config.modes</c></item>
/// <item><c>app/schemas/server.py:328-339</c> <c>_ServerWriteBase</c> — <c>extra="forbid"</c> +
///   <c>_legacy_fields = SERVER_REMOVED_FIELDS ∪ {ip_address, user_name, user_password}</c></item>
/// <item><c>app/schemas/server.py:352-354</c> — 본문의 <c>status</c> 는 <c>422 OBSERVED_FIELD</c></item>
/// <item><c>app/schemas/server.py:409-427</c> <c>ServerUpdate</c> — 축은 RFC 7396 병합(키 null = 그 키 삭제)</item>
/// <item><c>app/services/json_merge.py:28-40</c> — 병합 규칙 원문</item>
/// <item><c>app/schemas/server.py:759-800</c> <c>ServerResponse</c> — 7.0+ 응답은 축이다</item>
/// <item><c>app/schemas/device.py:85-93</c> <c>SERVER_CATEGORIES_BY_DEVICE</c> · <c>:568·683·740</c>
///   (<c>server_id: null = 관계 해제</c>)</item>
/// </list>
/// </remarks>
public class ServerAxisContractTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static (ServerAxisApiService Service, CapturingApiService Http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        return (new ServerAxisApiService(http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract)), http);
    }

    /// <summary>서비스와 <b>같은 방식</b>으로 읽는다(날짜를 DateTime 으로 바꾸지 않는다).</summary>
    private static JObject Payload(string json)
    {
        using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json))
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
        };
        return JObject.Load(reader);
    }

    private static ServerWriteIntent FullIntent() => new()
    {
        TypeServer = "SPEAKER_API",
        CategoryId = 3,
        Name = "방송서버",
        IpAddress = "10.0.0.5",
        Port = 8080,
        Hostname = "bcast-01",
        UserName = "admin",
        NewPassword = "secret",
        CpuWarning = 70,
        CpuCritical = 90,
        NetworkWarningMbps = 500,
        UnitId = 4,
    };

    #region - 6.3 (운영) 평면 본문 -
    [Fact]
    public void should_write_the_flat_body_when_contract_is_legacy()
    {
        var body = ServerAxisWriter.BuildCreate(FullIntent(), EnumServerContract.V6_3);

        Assert.Equal(3, (int?)body["category_id"]);
        Assert.Equal("방송서버", (string?)body["name"]);
        Assert.Equal("10.0.0.5", (string?)body["ip_address"]);
        Assert.Equal(8080, (int?)body["port"]);
        Assert.Equal("bcast-01", (string?)body["hostname"]);
        Assert.Equal(70.0, (double?)body["threshold_config"]!["cpu"]!["warning"]);

        // 축 키는 6.3 에 존재하지 않는다.
        Assert.False(body.ContainsKey("category_server"));
        Assert.False(body.ContainsKey("connection"));
        Assert.False(body.ContainsKey("server_config"));
        // 8.0 축인 unit_id 도 6.3 에는 없다.
        Assert.False(body.ContainsKey("unit_id"));
    }

    [Fact]
    public void should_not_offer_clearing_when_contract_is_legacy()
    {
        Assert.False(ServerAxisWriter.SupportsClearing(EnumServerContract.V6_3));
        Assert.True(ServerAxisWriter.SupportsClearing(EnumServerContract.V7_0));
    }
    #endregion

    #region - 7.0 / 8.0 축 본문 -
    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public void should_never_send_a_key_the_axis_contract_rejects(EnumServerContract contract)
    {
        foreach (var body in new[]
        {
            ServerAxisWriter.BuildCreate(FullIntent(), contract),
            ServerAxisWriter.BuildPatch(FullIntent(), contract),
        })
        {
            foreach (var rejected in ServerAxisWriter.LegacyRejectedOnAxis)
                Assert.False(body.ContainsKey(rejected), $"'{rejected}' 가 축 본문에 실렸습니다 — 7.0 은 422 입니다");
        }
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3)]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public void should_never_send_an_observed_or_server_owned_key_in_any_contract(EnumServerContract contract)
    {
        foreach (var body in new[]
        {
            ServerAxisWriter.BuildCreate(FullIntent(), contract),
            ServerAxisWriter.BuildPatch(FullIntent(), contract),
        })
        {
            foreach (var key in ServerAxisWriter.NeverSentKeys)
                Assert.False(body.ContainsKey(key), $"'{key}' 는 본문에 실리면 안 됩니다");
        }
    }

    [Fact]
    public void should_move_every_flat_value_into_its_axis_when_contract_is_axis()
    {
        var body = ServerAxisWriter.BuildCreate(FullIntent(), EnumServerContract.V8_0);

        Assert.Equal("SPEAKER_API", (string?)body["category_server"]);
        Assert.Equal("방송서버", (string?)body["name"]);
        Assert.Equal("10.0.0.5", (string?)body["connection"]!["ip_address"]);
        Assert.Equal(8080, (int?)body["connection"]!["ip_port"]);
        Assert.Equal("bcast-01", (string?)body["connection"]!["hostname"]);
        Assert.Equal("admin", (string?)body["connection"]!["credentials"]!["user_name"]);
        Assert.Equal("secret", (string?)body["connection"]!["credentials"]!["user_password"]);
        Assert.Equal(70.0, (double?)body["server_config"]!["thresholds"]!["cpu"]!["warning"]);
        Assert.Equal(500.0, (double?)body["server_config"]!["thresholds"]!["network"]!["warning_mbps"]);
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3, false)]
    [InlineData(EnumServerContract.V7_0, false)]
    [InlineData(EnumServerContract.V8_0, true)]
    public void should_send_unit_id_only_when_the_contract_has_the_unit_axis(EnumServerContract contract, bool expected)
        => Assert.Equal(expected, ServerAxisWriter.BuildCreate(FullIntent(), contract).ContainsKey("unit_id"));

    [Fact]
    public void should_not_send_the_immutable_discriminator_when_patching()
    {
        // category_server 는 등록 뒤 불변이고, PATCH 에는 같은 값만 허용이라 보낼 이유가 없다(server.py:421-422).
        Assert.False(ServerAxisWriter.BuildPatch(FullIntent(), EnumServerContract.V8_0).ContainsKey("category_server"));
        Assert.False(ServerAxisWriter.BuildPatch(FullIntent(), EnumServerContract.V6_3).ContainsKey("category_id"));
    }
    #endregion

    #region - PATCH 는 손댄 키만 -
    [Theory]
    [InlineData(EnumServerContract.V6_3)]
    [InlineData(EnumServerContract.V8_0)]
    public void should_send_nothing_when_nothing_was_touched(EnumServerContract contract)
        => Assert.Empty(ServerAxisWriter.BuildPatch(new ServerWriteIntent(), contract).Properties());

    [Fact]
    public void should_send_only_the_touched_key_inside_the_axis_when_patching()
    {
        var body = ServerAxisWriter.BuildPatch(new ServerWriteIntent { Port = 8101 }, EnumServerContract.V8_0);

        Assert.Single(body.Properties());
        Assert.Equal(8101, (int?)body["connection"]!["ip_port"]);
        Assert.Single((body["connection"] as JObject)!.Properties());
    }

    [Fact]
    public void should_send_only_the_touched_threshold_when_patching()
    {
        var body = ServerAxisWriter.BuildPatch(new ServerWriteIntent { CpuWarning = 65 }, EnumServerContract.V8_0);

        Assert.Equal(65.0, (double?)body["server_config"]!["thresholds"]!["cpu"]!["warning"]);
        Assert.Null(body["server_config"]!["thresholds"]!["cpu"]!["critical"]);
        Assert.Null(body["server_config"]!["thresholds"]!["ram"]);
        Assert.False(body.ContainsKey("connection"));
    }

    [Fact]
    public void should_send_an_explicit_null_to_clear_a_key_on_the_axis_contract()
    {
        var body = ServerAxisWriter.BuildPatch(new ServerWriteIntent { ClearHostname = true, ClearUserName = true }, EnumServerContract.V8_0);

        Assert.Equal(JTokenType.Null, body["connection"]!["hostname"]!.Type);
        Assert.Equal(JTokenType.Null, body["connection"]!["credentials"]!["user_name"]!.Type);
    }

    [Fact]
    public void should_keep_the_password_out_of_the_body_when_it_was_not_typed()
    {
        var body = ServerAxisWriter.BuildPatch(new ServerWriteIntent { UserName = "svc" }, EnumServerContract.V8_0);
        Assert.Null(body["connection"]!["credentials"]!["user_password"]);

        var typed = ServerAxisWriter.BuildPatch(new ServerWriteIntent { NewPassword = "new-one" }, EnumServerContract.V8_0);
        Assert.Equal("new-one", (string?)typed["connection"]!["credentials"]!["user_password"]);
    }

    [Fact]
    public void should_write_modes_into_server_config_when_operation_mode_is_edited()
    {
        var body = ServerAxisWriter.BuildPatch(new ServerWriteIntent { OperationMode = "REGISTER", WindyMode = "wind1" }, EnumServerContract.V8_0);

        Assert.Equal("REGISTER", (string?)body["server_config"]!["modes"]!["operation_mode"]);
        Assert.Equal("wind1", (string?)body["server_config"]!["modes"]!["windy_mode"]);
        Assert.False(body.ContainsKey("proxy_settings"));
    }
    #endregion

    #region - 읽기 투영 -
    [Fact]
    public void should_read_the_axis_payload_when_contract_is_axis()
    {
        var row = Payload("""
        {
          "id": 6, "category_server": "PROXY", "name": "PROXY-ab0001", "is_enable": true, "unit_id": 1,
          "status": "NORMAL", "status_observed_at": "2026-10-27T10:00:05.120000+09:00",
          "connection": { "schema": 1, "type": "IP_DIRECT", "ip_address": "192.168.1.100", "ip_port": 8100,
                          "hostname": "proxy-server-01",
                          "credentials": { "user_name": "svc", "user_password": "svc123" } },
          "server_config": { "schema": 1, "thresholds": { "cpu": { "warning": 80, "critical": 95 } },
                             "modes": { "operation_mode": "NORMAL", "windy_mode": "wind0" } },
          "created_at": "2026-10-27T09:00:00.000000+09:00",
          "updated_at": "2026-10-27T10:00:05.000000+09:00"
        }
        """);

        var view = ServerAxisReader.Parse(row, EnumServerContract.V8_0);

        Assert.Equal("PROXY", view.TypeServer);
        Assert.Equal("192.168.1.100", view.IpAddress);
        Assert.Equal(8100, view.Port);
        Assert.Equal("proxy-server-01", view.Hostname);
        Assert.Equal("svc", view.UserName);
        Assert.Equal(1, view.UnitId);
        Assert.True(view.HasConnectionSection);
        Assert.True(view.HasConfigSection);
        Assert.Equal(80.0, (double?)view.Thresholds!["cpu"]!["warning"]);
        Assert.Equal("NORMAL", (string?)view.Modes!["operation_mode"]);
        Assert.Equal("2026-10-27T10:00:05.120000+09:00", view.StatusObservedAt);
        Assert.True(view.HasStatusObservedAtKey);
    }

    [Fact]
    public void should_know_a_server_never_reported_when_status_observed_at_is_null()
    {
        var row = Payload("""
        { "id": 7, "category_server": "BACKUP", "name": "backup", "is_enable": true, "unit_id": 1,
          "status": "UNKNOWN", "status_observed_at": null,
          "created_at": "2026-10-27T09:00:00.000000+09:00", "updated_at": "2026-10-27T09:00:00.000000+09:00" }
        """);

        var view = ServerAxisReader.Parse(row, EnumServerContract.V8_0);

        Assert.True(view.HasStatusObservedAtKey);      // 키는 있다
        Assert.Null(view.StatusObservedAt);            // 값이 없다 = 보고 없음
        Assert.Equal("UNKNOWN", view.Status);
        Assert.False(view.HasConnectionSection);       // 저장된 축이 없으면 키째 빠진다
    }

    [Fact]
    public void should_read_the_flat_payload_and_know_an_absent_status_key_when_contract_is_legacy()
    {
        var row = Payload("""
        { "id": 3, "category_id": 2, "name": "old", "ip_address": "10.0.0.9", "port": 8000,
          "hostname": "h", "user_name": "admin",
          "threshold_config": { "cpu": { "warning": 70, "critical": 90 } },
          "created_at": "2026-01-01T00:00:00+09:00", "updated_at": "2026-01-02T00:00:00+09:00" }
        """);

        var view = ServerAxisReader.Parse(row, EnumServerContract.V6_3, id => id == 2 ? "NVR_API" : null);

        Assert.Equal("NVR_API", view.TypeServer);
        Assert.Equal("10.0.0.9", view.IpAddress);
        Assert.Equal(8000, view.Port);
        Assert.False(view.HasStatusKey);               // 키가 없었다 — 기본값 "NORMAL" 을 지어내지 않는다
        Assert.Null(view.Status);
        Assert.False(view.HasStatusObservedAtKey);     // 6.3 에는 전이 시각이 아예 없다
        Assert.Equal(70.0, (double?)view.Thresholds!["cpu"]!["warning"]);
    }

    [Fact]
    public void should_keep_a_present_status_value_when_contract_is_legacy()
    {
        var row = Payload("""{ "id": 3, "category_id": 2, "name": "old", "status": "ERROR", "ip_address": "1.1.1.1", "port": 1 }""");
        var view = ServerAxisReader.Parse(row, EnumServerContract.V6_3);

        Assert.True(view.HasStatusKey);
        Assert.Equal("ERROR", view.Status);
    }
    #endregion

    #region - 장비 배정 본문 -
    [Fact]
    public void should_send_only_server_id_when_assigning_a_device()
    {
        var body = ServerAxisWriter.BuildDeviceAssign(12, unitId: 4, EnumServerContract.V7_0);

        Assert.Single(body.Properties());
        Assert.Equal(12, (int?)body["server_id"]);
    }

    [Fact]
    public void should_send_an_explicit_null_when_detaching_a_device()
    {
        var body = ServerAxisWriter.BuildDeviceAssign(null, unitId: null, EnumServerContract.V7_0);
        Assert.Equal(JTokenType.Null, body["server_id"]!.Type);
    }

    [Fact]
    public void should_stamp_the_unit_only_on_the_unit_era_when_assigning()
    {
        Assert.True(ServerAxisWriter.BuildDeviceAssign(12, 4, EnumServerContract.V8_0).ContainsKey("unit_id"));
        Assert.False(ServerAxisWriter.BuildDeviceAssign(12, 4, EnumServerContract.V7_0).ContainsKey("unit_id"));
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Controller, "controllers")]
    [InlineData(EnumDeviceCategory.Camera, "cameras")]
    [InlineData(EnumDeviceCategory.Speaker, "speakers")]
    [InlineData(EnumDeviceCategory.Enclosure, "enclosures")]
    [InlineData(EnumDeviceCategory.Lamp, "lamps")]
    [InlineData(EnumDeviceCategory.Gate, "gates")]
    public void should_know_the_endpoint_of_every_category_that_has_a_managing_server(EnumDeviceCategory category, string expected)
        => Assert.Equal(expected, ServerAxisApiService.DevicePathOf(category));

    [Fact]
    public void should_have_no_endpoint_for_a_sensor()
        => Assert.Null(ServerAxisApiService.DevicePathOf(EnumDeviceCategory.Sensor));
    #endregion

    #region - 실제 서비스를 통과시킨다(HTTP 만 대역) -
    [Fact]
    public async Task should_post_the_axis_body_to_the_servers_path_when_creating()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.CreateServerAsync(FullIntent());

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/servers", http.Endpoint);
        Assert.Equal("SPEAKER_API", (string?)http.Body!["category_server"]);
        Assert.False(http.Body.ContainsKey("status"));
    }

    [Fact]
    public async Task should_patch_the_flat_body_to_the_servers_path_when_contract_is_legacy()
    {
        var (service, http) = Create(EnumServerContract.V6_3);

        await service.PatchServerAsync(9, new ServerWriteIntent { Port = 9000 });

        Assert.Equal("PATCH", http.Method);
        Assert.Equal($"{BaseUrl}/servers/9", http.Endpoint);
        Assert.Equal(9000, (int?)http.Body!["port"]);
        Assert.False(http.Body.ContainsKey("connection"));
    }

    [Fact]
    public async Task should_not_call_the_server_when_a_patch_has_nothing_to_send()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        var result = await service.PatchServerAsync(9, new ServerWriteIntent());

        Assert.True(result.IsSuccess);
        Assert.Null(http.Method);
    }

    [Fact]
    public async Task should_patch_the_device_category_path_when_assigning()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.AssignDeviceServerAsync(EnumDeviceCategory.Camera, 41, 12, 4);

        Assert.Equal("PATCH", http.Method);
        Assert.Equal($"{BaseUrl}/devices/cameras/41", http.Endpoint);
        Assert.Equal(12, (int?)http.Body!["server_id"]);
    }

    [Fact]
    public async Task should_refuse_a_sensor_without_calling_the_server()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        var result = await service.AssignDeviceServerAsync(EnumDeviceCategory.Sensor, 41, 12, null);

        Assert.False(result.IsSuccess);
        Assert.Null(http.Method);
        Assert.Contains("관리 서버가 없습니다", result.Message);
    }

    [Fact]
    public async Task should_refuse_a_detach_without_calling_the_server_when_contract_is_legacy()
    {
        var (service, http) = Create(EnumServerContract.V6_3);

        var result = await service.AssignDeviceServerAsync(EnumDeviceCategory.Speaker, 41, null, null);

        Assert.False(result.IsSuccess);
        Assert.Null(http.Method);
        Assert.Contains("6.3", result.Message);
    }

    [Fact]
    public async Task should_report_the_transport_failure_as_a_sentence_when_the_call_throws()
    {
        var (service, http) = Create(EnumServerContract.V8_0);
        http.Throw = true;

        var result = await service.PatchServerAsync(9, new ServerWriteIntent { Name = "x" });

        Assert.False(result.IsSuccess);
        Assert.Contains("닿지 못했습니다", result.Message);
    }
    #endregion
}

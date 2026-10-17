using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;
/****************************************************************************
   Purpose      : ServerDto 의 7.0+/8.0 축 응답 읽기 회귀 방지 (D-22)
   Created By   : Claude
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : 서버 app/schemas/server.py:59-66 SERVER_REMOVED_FIELDS 는 응답에도 그대로 적용된다
                  (ServerResponse, :759-800) — category_id 는 category_server(문자열)로, 평면
                  ip_address·port·hostname·threshold_config 는 connection{}·server_config{} 축으로
                  옮겨졌다. 이 평면 DTO 로 축 응답을 그대로 역직렬화하면 success:true 인데 내용이
                  전부 기본값(빈 문자열·0)으로 조용히 비는 결함이 있었다(레거시 IServerApiService.
                  GetServersAsync 가 이 DTO 로 역직렬화하며, DeviceProviderService.FetchServersAsync 의
                  스피커 서버 드롭다운이 그 소비자다). ServerDto.OnDeserializedMethod 의 역투영을 고정한다.
****************************************************************************/
public class ServerDtoAxisReadTests
{
    private const string AxisJson = """
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
    """;

    private const string FlatJson = """
    { "id": 3, "category_id": 2, "name": "old", "status": "NORMAL", "ip_address": "10.0.0.9", "port": 8000,
      "hostname": "h", "user_name": "admin",
      "threshold_config": { "cpu": { "warning": 70, "critical": 90 } },
      "created_at": "2026-01-01T00:00:00+09:00", "updated_at": "2026-01-02T00:00:00+09:00" }
    """;

    [Fact]
    public void should_backfill_flat_fields_from_the_axis_shape_when_response_is_7_0_or_later()
    {
        var dto = JsonConvert.DeserializeObject<ServerDto>(AxisJson);

        Assert.NotNull(dto);
        Assert.Equal("192.168.1.100", dto!.IpAddress);
        Assert.Equal(8100, dto.Port);
        Assert.Equal("proxy-server-01", dto.Hostname);
        Assert.Equal("svc", dto.UserName);
        Assert.Equal("svc123", dto.UserPassword);
        Assert.NotNull(dto.ThresholdConfig);
        Assert.Equal(80.0, (double?)dto.ThresholdConfig!["cpu"]!["warning"]);

        // 축 전용 정보도 원문 그대로 남는다 — 정본은 아니지만 소비자가 필요하면 쓸 수 있다.
        Assert.Equal("PROXY", dto.CategoryServer);
        Assert.True(dto.IsEnable);
        Assert.Equal(1, dto.UnitId);

        // 7.0+ 에는 category_id 가 없다 — 지어내지 않고 기본값(0) 그대로 남긴다.
        Assert.Equal(0, dto.CategoryId);
    }

    [Fact]
    public void should_read_the_flat_shape_unchanged_when_response_is_legacy_6_3()
    {
        var dto = JsonConvert.DeserializeObject<ServerDto>(FlatJson);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.CategoryId);
        Assert.Equal("10.0.0.9", dto.IpAddress);
        Assert.Equal(8000, dto.Port);
        Assert.Equal("h", dto.Hostname);
        Assert.Equal("admin", dto.UserName);
        Assert.NotNull(dto.ThresholdConfig);
        Assert.Equal(70.0, (double?)dto.ThresholdConfig!["cpu"]!["warning"]);

        // 6.3 응답에는 축 전용 키가 없다.
        Assert.Null(dto.CategoryServer);
        Assert.Null(dto.IsEnable);
        Assert.Null(dto.Connection);
        Assert.Null(dto.ServerConfig);
    }

    [Fact]
    public void should_prefer_the_flat_value_when_a_response_somehow_carries_both_shapes()
    {
        // 어느 판본이 채운 값이든 먼저 채워진 값이 이긴다 — 축 값이 평면 값을 덮어쓰지 않는다.
        const string mixed = """
        { "id": 1, "category_id": 9, "name": "n", "status": "NORMAL", "ip_address": "1.1.1.1", "port": 1,
          "connection": { "ip_address": "9.9.9.9", "ip_port": 9999 } }
        """;

        var dto = JsonConvert.DeserializeObject<ServerDto>(mixed);

        Assert.NotNull(dto);
        Assert.Equal("1.1.1.1", dto!.IpAddress);
        Assert.Equal(1, dto.Port);
    }

    [Fact]
    public void should_not_serialize_axis_only_properties_when_writing_the_legacy_flat_body()
    {
        // ServerDto 를 직접 POST 본문으로 쓰는 6.3 쓰기 경로(ServerApiService.CreateServerAsync)가
        // 손댄 적 없는 축 전용 필드를 실어 보내지 않는다는 것을 직렬화 결과로 고정한다.
        var dto = new ServerDto { CategoryId = 1, Name = "n", IpAddress = "1.1.1.1", Port = 1 };

        var json = JsonConvert.SerializeObject(dto);

        Assert.DoesNotContain("category_server", json);
        Assert.DoesNotContain("is_enable", json);
        Assert.DoesNotContain("\"connection\"", json);
        Assert.DoesNotContain("server_config", json);
    }
}

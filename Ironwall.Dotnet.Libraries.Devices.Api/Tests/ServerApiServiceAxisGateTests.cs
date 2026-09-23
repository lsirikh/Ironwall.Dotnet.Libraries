using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;
/****************************************************************************
   Purpose      : ServerApiService(레거시 평면 IServerApiService) 의 축 계약 거부 게이트 (D-22)
   Created By   : Claude
   Created On   : 9/23/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : ServerConsoleService/IServerAxisApiService 가 실제 UI 쓰기 통로가 된 뒤에도
                  레거시 IServerApiService.CreateServerAsync/PatchServerAsync/UpdateServerAsync 는
                  평면 ServerDto(category_id·ip_address·port·hostname·threshold_config)를 그대로
                  본문으로 보냈다 — 7.0+/8.0 축 계약(extra="forbid")에서 전부 422 다(D-22).
                  이 파일은 ① 축 계약에서 네트워크에 나가기 전에 거부하는가 ② 6.3 계약에서는
                  기존 평면 본문이 그대로 나가는가(회귀 없음) ③ ServerDto 의 "응답 전용" 축 필드가
                  리플렉션으로 봐도 legacy 쓰기 본문에 새지 않는가를 증명한다.
****************************************************************************/
public class ServerApiServiceAxisGateTests
{
    private static ServerApiService Create(EnumServerContract contract, out CapturingApiService http)
    {
        http = new CapturingApiService();
        return new ServerApiService(null, http, new ApiSetupModel { Url = "https://gop.test/api" },
            new FixedContractProbe(contract));
    }

    private static ServerDto FullFlatDto() => new()
    {
        CategoryId = 3,
        Name = "방송서버",
        IpAddress = "10.0.0.5",
        Port = 8080,
        Hostname = "bcast-01",
        UserName = "admin",
        UserPassword = "secret",
    };

    #region - 축 계약(7.0/8.0) 에서는 네트워크에 나가기 전에 거부한다 -
    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_refuse_create_without_calling_the_server_when_contract_is_axis_era(EnumServerContract contract)
    {
        var service = Create(contract, out var http);

        var result = await service.CreateServerAsync(FullFlatDto());

        Assert.False(result.Success);
        Assert.Equal("AXIS_SHAPE_REQUIRED", result.Error?.Code);
        Assert.Null(http.Method);
        Assert.Null(http.Body);
    }

    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_refuse_patch_without_calling_the_server_when_contract_is_axis_era(EnumServerContract contract)
    {
        var service = Create(contract, out var http);

        var result = await service.PatchServerAsync(9, new ServerDto { Port = 9000 });

        Assert.False(result.Success);
        Assert.Equal("AXIS_SHAPE_REQUIRED", result.Error?.Code);
        Assert.Null(http.Method);
    }

    [Theory]
    [InlineData(EnumServerContract.V7_0)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task should_refuse_update_without_calling_the_server_when_contract_is_axis_era(EnumServerContract contract)
    {
        var service = Create(contract, out var http);

        var result = await service.UpdateServerAsync(9, FullFlatDto());

        Assert.False(result.Success);
        Assert.Equal("AXIS_SHAPE_REQUIRED", result.Error?.Code);
        Assert.Null(http.Method);
    }

    [Fact]
    public async Task should_point_the_refusal_at_the_axis_channel_the_console_actually_uses()
    {
        var service = Create(EnumServerContract.V8_0, out _);

        var result = await service.CreateServerAsync(FullFlatDto());

        Assert.Contains("IServerAxisApiService", result.Message);
    }
    #endregion

    #region - 6.3(운영) 계약에서는 기존 평면 본문이 그대로 나간다 — 회귀 없음 -
    [Fact]
    public async Task should_still_post_the_flat_body_when_contract_is_legacy()
    {
        var service = Create(EnumServerContract.V6_3, out var http);

        await service.CreateServerAsync(FullFlatDto());

        Assert.Equal("POST", http.Method);
        Assert.Equal("https://gop.test/api/servers", http.Endpoint);
        Assert.Equal(3, (int?)http.Body!["category_id"]);
        Assert.Equal("10.0.0.5", (string?)http.Body["ip_address"]);
        Assert.Equal(8080, (int?)http.Body["port"]);

        // 축 전용(응답 전용) 필드는 손대지 않았으므로 legacy 본문에 새지 않는다.
        Assert.False(http.Body.ContainsKey("category_server"));
        Assert.False(http.Body.ContainsKey("connection"));
        Assert.False(http.Body.ContainsKey("server_config"));
        Assert.False(http.Body.ContainsKey("is_enable"));
    }

    [Fact]
    public async Task should_still_patch_the_flat_body_when_contract_is_legacy()
    {
        var service = Create(EnumServerContract.V6_3, out var http);

        await service.PatchServerAsync(9, new ServerDto { Port = 9000 });

        Assert.Equal("PATCH", http.Method);
        Assert.Equal(9000, (int?)http.Body!["port"]);
    }
    #endregion

    #region - 리플렉션 감사 — ServerDto 의 "응답 전용" 축 필드는 legacy 쓰기에 새지 않는다 -
    /// <summary>
    /// <c>ServerDto</c> 에 축 응답을 담는 새 프로퍼티(<c>category_server</c>·<c>is_enable</c>·
    /// <c>connection</c>·<c>server_config</c>)를 더 붙일 때, <c>NullValueHandling.Ignore</c> 를
    /// 빠뜨리면 그 프로퍼티가 <c>null</c> 이어도 <c>"connection": null</c> 처럼 legacy(6.3) 쓰기
    /// 본문에 키째 실린다 — 리플렉션으로 전 프로퍼티를 훑어 이 계약을 강제한다(회귀 조기 발견).
    /// </summary>
    [Theory]
    [InlineData(nameof(ServerDto.CategoryServer))]
    [InlineData(nameof(ServerDto.IsEnable))]
    [InlineData(nameof(ServerDto.Connection))]
    [InlineData(nameof(ServerDto.ServerConfig))]
    [InlineData(nameof(ServerDto.UnitId))]
    public void should_mark_every_axis_only_response_property_ignore_on_null(string propertyName)
    {
        var property = typeof(ServerDto).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);

        var attribute = property!.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(NullValueHandling.Ignore, attribute!.NullValueHandling);
    }

    /// <summary>
    /// <c>ServerAxisWriter.LegacyRejectedOnAxis</c>(축 계약이 거부하는 평면 키 목록, N-12)와
    /// <c>ServerDto</c> 가 실제로 선언한 <c>JsonProperty</c> 이름을 맞대본다 — 서버가 새 레거시 키를
    /// 더 제거해도(계약 갱신) 두 목록이 갈리면 이 테스트가 먼저 깨진다.
    /// </summary>
    [Fact]
    public void should_have_a_flat_json_property_for_every_key_the_axis_contract_rejects()
    {
        var declared = typeof(ServerDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName)
            .Where(name => name is not null)
            .ToHashSet();

        foreach (var rejected in Ironwall.Dotnet.Libraries.Devices.Api.Servers.ServerAxisWriter.LegacyRejectedOnAxis)
        {
            if (rejected is "proxy_settings") continue; // ServerDto 에는 프록시 설정이 없다(별도 DTO) — 대상 밖.
            Assert.True(declared.Contains(rejected),
                $"'{rejected}' 는 축 계약이 거부하는 평면 키인데 ServerDto 에 대응 JsonProperty 가 없습니다");
        }
    }
    #endregion
}

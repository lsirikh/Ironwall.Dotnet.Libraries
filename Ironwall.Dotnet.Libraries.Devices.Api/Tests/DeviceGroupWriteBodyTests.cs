using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// 그룹 쓰기 본문 · 장비 쓰기의 소속/결선 방식 보존 — 서버에 붙지 않는 계약 테스트.
/// </summary>
/// <remarks>
/// 라이브 실측(2026-09-24, 서버 8.0.2 · tools/live-api-roundtrip device-groups):
/// 그룹 POST/PUT 이 <c>device_count</c>·<c>created_at</c>(·<c>id</c>)를 실어 422 UNKNOWN_FIELD 였고,
/// 재조립 <c>connection</c> 이 저장된 <c>IP_CONVERTER</c> 를 <c>IP_DIRECT</c> 로 덮었다.
/// </remarks>
public class DeviceGroupWriteBodyTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static (DeviceApiService service, CapturingApiService http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        return (new DeviceApiService(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract)), http);
    }

    private static DeviceGroupDto ResponseShapedGroup() => new() { Id = 45, Name = "G", Description = "d", DeviceCount = 7 };

    [Fact]
    public async Task should_post_only_name_and_description_when_creating_group()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.CreateDeviceGroupAsync(ResponseShapedGroup());

        Assert.Equal("POST", http.Method);
        Assert.Equal(new[] { "name", "description" }, http.Body!.Properties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task should_put_only_name_and_description_when_updating_group()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.UpdateDeviceGroupAsync(45, ResponseShapedGroup());

        Assert.Equal("PUT", http.Method);
        Assert.Equal($"{BaseUrl}/devices/groups/45", http.Endpoint);
        Assert.Equal(new[] { "name", "description" }, http.Body!.Properties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task should_patch_only_name_and_description_when_patching_group()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.PatchDeviceGroupAsync(45, ResponseShapedGroup());

        Assert.Equal("PATCH", http.Method);
        Assert.Equal(new[] { "name", "description" }, http.Body!.Properties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task should_post_unit_id_when_creating_group_with_a_unit_on_unit_contract()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.CreateDeviceGroupAsync(new DeviceGroupDto { Name = "G", Description = "d", UnitId = 12 });

        Assert.Equal(new[] { "name", "description", "unit_id" }, http.Body!.Properties().Select(p => p.Name).ToArray());
        Assert.Equal(12, (int?)http.Body!["unit_id"]);
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3)]
    [InlineData(EnumServerContract.V7_0)]
    public async Task should_strip_unit_id_when_creating_group_below_unit_contract(EnumServerContract contract)
    {
        var (service, http) = Create(contract);

        await service.CreateDeviceGroupAsync(new DeviceGroupDto { Name = "G", Description = "d", UnitId = 12 });

        Assert.Equal(new[] { "name", "description" }, http.Body!.Properties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task should_keep_stored_connection_type_when_response_dto_is_patched_back()
    {
        var (service, http) = Create(EnumServerContract.V8_0);
        var dto = JsonConvert.DeserializeObject<ControllerDeviceDto>(
            "{ \"id\": 41, \"number_device\": 5, \"name_device\": \"c\", \"status\": \"ACTIVATED\", \"is_enable\": true, "
          + "\"category_device\": \"controller\", \"type_controller\": \"Controller\", "
          + "\"connection\": { \"type\": \"IP_CONVERTER\", \"ip_address\": \"10.0.0.7\", \"ip_port\": 9000 } }")!;

        await service.UpdateControllerAsync(41, dto);

        Assert.Equal("IP_CONVERTER", (string?)http.Body!.SelectToken("connection.type"));
    }

    [Fact]
    public async Task should_omit_group_ids_when_membership_unchanged_on_axis_contract()
    {
        var (service, http) = Create(EnumServerContract.V8_0);
        var dto = new SensorDeviceDto { Id = 41, NameDevice = "s", GroupIds = new() { 1, 2 }, GroupIdsUnchanged = true };

        await service.UpdateSensorAsync(41, dto);

        Assert.False(http.Body!.ContainsKey("group_ids"));
    }

    [Fact]
    public async Task should_send_group_ids_when_membership_unchanged_on_legacy_contract()
    {
        var (service, http) = Create(EnumServerContract.V6_3);
        var dto = new SensorDeviceDto { Id = 41, NameDevice = "s", GroupIds = new() { 1, 2 }, GroupIdsUnchanged = true };

        await service.UpdateSensorAsync(41, dto);

        Assert.Equal(new[] { 1, 2 }, http.Body!["group_ids"]!.Select(t => (int)t).ToArray());
    }
}

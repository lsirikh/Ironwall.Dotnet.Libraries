using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// 장비 상세의 축 값 편집(접속 · 형상 · 부대 · 설정)이 타는 좁은 PATCH — 서버에 붙지 않는 계약 테스트.
/// </summary>
/// <remarks>
/// 보낸 키만 바뀌어야 한다: 빈 DTO 로 만든 PATCH 는 null 키가 서버 값을 지우고, <c>components</c> 는 PATCH 에서도 통째 교체다.
/// </remarks>
public class DeviceAxesPatchTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static (IDeviceApiService service, CapturingApiService http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        return (new DeviceApiService(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract)), http);
    }

    [Fact]
    public async Task should_patch_only_the_given_keys_when_axis_contract()
    {
        var (service, http) = Create(EnumServerContract.V8_0);
        var body = JObject.Parse("{\"connection\":{\"channel\":3},\"hardware_spec\":{\"model\":\"M-1\",\"serial\":null},\"unit_id\":7}");

        var result = await service.PatchDeviceAxesAsync("sensors", 42, body);

        Assert.True(result.Success);
        Assert.Equal("PATCH", http.Method);
        Assert.Equal($"{BaseUrl}/devices/sensors/42", http.Endpoint);
        Assert.True(JToken.DeepEquals(body, http.Body), http.Body?.ToString());
    }

    [Fact]
    public async Task should_drop_unit_id_when_contract_is_below_8_0()
    {
        var (service, http) = Create(EnumServerContract.V7_0);

        await service.PatchDeviceAxesAsync("controllers", 5, JObject.Parse("{\"hardware_spec\":{\"model\":\"X\"},\"unit_id\":3}"));

        Assert.Equal(new[] { "hardware_spec" }, http.Body!.Properties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task should_not_send_when_contract_is_legacy()
    {
        var (service, http) = Create(EnumServerContract.V6_3);

        var result = await service.PatchDeviceAxesAsync("controllers", 5, JObject.Parse("{\"hardware_spec\":{\"model\":\"X\"}}"));

        Assert.False(result.Success);
        Assert.Null(http.Method);
    }

    [Theory]
    [InlineData("{\"hardware_spec\":{\"components\":[]}}")]
    [InlineData("{\"device_status\":{\"components\":{}}}")]
    [InlineData("{\"name_device\":\"x\"}")]
    [InlineData("{}")]
    public async Task should_block_the_body_when_it_carries_components_observed_or_unknown_keys(string json)
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        var result = await service.PatchDeviceAxesAsync("enclosures", 9, JObject.Parse(json));

        Assert.False(result.Success);
        Assert.Null(http.Method);
    }

    [Fact]
    public async Task should_reject_an_unknown_device_path_when_patching_axes()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        var result = await service.PatchDeviceAxesAsync("enclosure", 9, JObject.Parse("{\"unit_id\":1}"));

        Assert.False(result.Success);
        Assert.Null(http.Method);
    }

    #region - 명시적 unit_id null 은 보내지 않는다 (서버 회신 2026-09-28 Q-1 — PATCH/PUT 의 명시적 null 은 422) -
    [Fact]
    public async Task should_strip_an_explicit_null_unit_id_when_patching_axes_on_8_0()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        var result = await service.PatchDeviceAxesAsync("controllers", 5, JObject.Parse("{\"hardware_spec\":{\"model\":\"X\"},\"unit_id\":null}"));

        Assert.True(result.Success);
        Assert.Equal(new[] { "hardware_spec" }, http.Body!.Properties().Select(p => p.Name).ToArray());   // 키째 빠진다 — 서버는 지금 부대를 그대로 둔다
    }

    [Fact]
    public async Task should_send_nothing_when_the_only_key_is_a_null_unit_id_on_8_0()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        var result = await service.PatchDeviceAxesAsync("sensors", 9, JObject.Parse("{\"unit_id\":null}"));

        Assert.False(result.Success);
        Assert.Null(http.Method);                                // 빈 PATCH 를 만들지 않는다 — 422 왕복도 없다
    }

    [Fact]
    public async Task should_keep_a_real_unit_id_when_patching_axes_on_8_0()
    {
        var (service, http) = Create(EnumServerContract.V8_0);

        await service.PatchDeviceAxesAsync("sensors", 9, JObject.Parse("{\"unit_id\":4}"));

        Assert.Equal(4, (int?)http.Body!["unit_id"]);
    }
    #endregion

    [Fact]
    public async Task should_not_mutate_the_callers_body_when_dropping_unit_id()
    {
        var (service, _) = Create(EnumServerContract.V7_0);
        var body = JObject.Parse("{\"hardware_spec\":{\"model\":\"X\"},\"unit_id\":3}");

        await service.PatchDeviceAxesAsync("controllers", 5, body);

        Assert.NotNull(body["unit_id"]);
    }
}

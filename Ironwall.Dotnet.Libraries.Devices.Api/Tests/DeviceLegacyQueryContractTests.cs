using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// 7.0 에서 이름이 바뀌었거나 사라진 장비 조회 질의 · 경로를 판본에 맞춰 보내는가 — 서버에 붙지 않는 계약 시험.
/// </summary>
/// <remarks>
/// <para>8.0.9 라이브 시험(DEP-201 D04 · D08 · D09 · D13 · D14 · D16 · D19 · D23 · D42, DEP-204 09-1)이 422 · 410 으로 깨진 원인이다.
/// 서버 근거: 옛 불리언 <c>include_sensors</c>·<c>include_controller</c> 는 값과 무관하게 422(<c>device_axes_io.reject_legacy_include_booleans</c>),
/// <c>type_device</c>·<c>mode</c>·<c>category</c>·<c>speaker_type</c>·<c>door_status</c> 는 422 <c>REMOVED_FIELD</c>(<c>_legacy.py</c>),
/// 카메라 <c>/settings</c> 는 410(<c>camera_settings.py</c>), 함체 계측 삭제는 <c>data:null</c>(<c>enclosure_metrics.py:362-366</c>).</para>
/// <para>6.3 질의는 바이트 그대로여야 한다(운영 6.3.2 무회귀) — 판본마다 한 쌍씩 확인한다.</para>
/// </remarks>
[Trait("Scenario", "DEP-201")]
public class DeviceLegacyQueryContractTests
{
    private const string BaseUrl = "https://gop.test/api";

    private static (DeviceApiService service, CapturingApiService http) Create(EnumServerContract contract)
    {
        var http = new CapturingApiService();
        return (new DeviceApiService(null, http, new ApiSetupModel { Url = BaseUrl }, new FixedContractProbe(contract)), http);
    }

    #region - 관계 동반 조회(include) -
    [Fact]
    public async Task 축계약_제어기목록_센서동반은_include토큰으로_보낸다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetControllersAsync(includeSensors: true);

        Assert.Equal("sensors", http.Query!["include"]);
        Assert.False(http.Query.ContainsKey("include_sensors"));
    }

    [Fact]
    public async Task 축계약_제어기단건_센서동반은_호출부include를_보존하고_덧붙인다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetControllerByIdAsync(7, includeSensors: true, include: "connection");

        Assert.Equal($"{BaseUrl}/devices/controllers/7", http.Endpoint);
        Assert.Equal("connection,sensors", http.Query!["include"]);
        Assert.False(http.Query.ContainsKey("include_sensors"));
    }

    [Fact]
    public async Task 축계약_이미있는_토큰은_중복해서_붙이지_않는다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetControllersAsync(includeSensors: true, include: "sensors");

        Assert.Equal("sensors", http.Query!["include"]);
    }

    [Fact]
    public async Task 판본63_제어기_센서동반은_옛불리언_그대로()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);

        await svc.GetControllersAsync(includeSensors: true);
        Assert.Equal("true", http.Query!["include_sensors"]);
        Assert.False(http.Query.ContainsKey("include"));

        await svc.GetControllerByIdAsync(7, includeSensors: true);
        Assert.Equal("true", http.Query!["include_sensors"]);
    }

    [Fact]
    public async Task 축계약_센서_제어기동반과_종류필터는_새키로_보낸다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetSensorsAsync(typeDevice: "Fence", includeController: true);
        Assert.Equal("Fence", http.Query!["type_sensor"]);
        Assert.Equal("controller", http.Query["include"]);
        Assert.False(http.Query.ContainsKey("type_device"));
        Assert.False(http.Query.ContainsKey("include_controller"));

        await svc.GetSensorByIdAsync(9, includeController: true);
        Assert.Equal("controller", http.Query!["include"]);
        Assert.False(http.Query.ContainsKey("include_controller"));
    }

    [Fact]
    public async Task 축계약_센서_새키를_따로주면_새키가_이긴다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetSensorsAsync(typeDevice: "Fence", typeSensor: "Laser");

        Assert.Equal("Laser", http.Query!["type_sensor"]);
        Assert.False(http.Query.ContainsKey("type_device"));
    }

    [Fact]
    public async Task 판본63_센서는_옛키_그대로()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);

        await svc.GetSensorsAsync(typeDevice: "Fence", includeController: true);

        Assert.Equal("Fence", http.Query!["type_device"]);
        Assert.Equal("true", http.Query["include_controller"]);
        Assert.False(http.Query.ContainsKey("type_sensor"));
        Assert.False(http.Query.ContainsKey("include"));
    }
    #endregion

    #region - 이름이 바뀐 목록 필터 -
    [Fact]
    public async Task 축계약_카메라_mode는_protocol_category는_type_camera로()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetCamerasAsync(mode: "ONVIF", category: "PTZ");

        Assert.Equal("ONVIF", http.Query!["protocol"]);
        Assert.Equal("PTZ", http.Query["type_camera"]);
        Assert.False(http.Query.ContainsKey("mode"));
        Assert.False(http.Query.ContainsKey("category"));
    }

    [Fact]
    public async Task 판본63_카메라는_옛키_그대로()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);

        await svc.GetCamerasAsync(mode: "ONVIF", category: "PTZ");

        Assert.Equal("ONVIF", http.Query!["mode"]);
        Assert.Equal("PTZ", http.Query["category"]);
        Assert.False(http.Query.ContainsKey("protocol"));
        Assert.False(http.Query.ContainsKey("type_camera"));
    }

    [Fact]
    public async Task 축계약_스피커_speaker_type은_speaker_role로()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.GetSpeakersAsync(speakerType: "NORMAL");

        Assert.Equal("NORMAL", http.Query!["speaker_role"]);
        Assert.False(http.Query.ContainsKey("speaker_type"));
    }

    [Fact]
    public async Task 판본63_스피커는_옛키_그대로()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);

        await svc.GetSpeakersAsync(speakerType: "NORMAL");

        Assert.Equal("NORMAL", http.Query!["speaker_type"]);
        Assert.False(http.Query.ContainsKey("speaker_role"));
    }
    #endregion

    #region - 사라진 필터 · 경로는 네트워크 전에 막는다 -
    [Fact]
    public async Task 축계약_함체_door_status_필터는_보내지_않고_대체를_알린다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        var resp = await svc.GetEnclosuresAsync(doorStatus: "CLOSED");

        Assert.False(resp.Success);
        Assert.Equal("ENDPOINT_UNAVAILABLE", resp.Error?.Code);
        Assert.Contains("DOOR_SENSOR", resp.Error?.Details ?? resp.Message);
        Assert.Equal(0, http.CallCount);
    }

    [Fact]
    public async Task 축계약_함체목록_문필터_없으면_그대로_조회한다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        var resp = await svc.GetEnclosuresAsync(status: "ACTIVATED");

        Assert.True(resp.Success);
        Assert.Equal(1, http.CallCount);
        Assert.False(http.Query!.ContainsKey("door_status"));
    }

    [Fact]
    public async Task 판본63_함체_door_status는_그대로_보낸다()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);

        await svc.GetEnclosuresAsync(doorStatus: "CLOSED");

        Assert.Equal("CLOSED", http.Query!["door_status"]);
    }

    [Fact]
    public async Task 축계약_카메라설정_세경로는_410묘비라_네트워크전에_막는다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        var get = await svc.GetCameraSettingAsync(5);
        var patch = await svc.PatchCameraSettingAsync(5, new CameraSettingDto { WeatherMode = "FOG" });
        var put = await svc.UpdateCameraSettingAsync(5, new CameraSettingDto { WeatherMode = "SNOW" });

        foreach (var resp in new[] { get, patch, put })
        {
            Assert.False(resp.Success);
            Assert.Equal("ENDPOINT_REMOVED", resp.Error?.Code);
        }
        Assert.Equal(0, http.CallCount);
    }

    [Fact]
    public async Task 판본63_카메라설정은_그대로_호출한다()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);

        await svc.GetCameraSettingAsync(5);

        Assert.Equal("GET", http.Method);
        Assert.Equal($"{BaseUrl}/devices/cameras/5/settings", http.Endpoint);
    }
    #endregion

    #region - 서버 카테고리 응답 역투영 -
    [Fact]
    public void 카테고리_8점0_응답의_category_server를_TypeServer로_읽는다()
    {
        // 서버 8.0.9 ServerCategoryResponse(schemas/server.py:831-843) — type_server 키가 없다
        var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<CategoryDto>(
            "{\"id\":10,\"name\":\"프록시 서버\",\"category_server\":\"PROXY\",\"description\":null,\"sort_order\":10}")!;

        Assert.Equal("PROXY", dto.TypeServer);
        Assert.Equal("PROXY", dto.CategoryServer);
    }

    [Fact]
    public void 카테고리_6점3_응답과_직렬화는_type_server_그대로()
    {
        var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<CategoryDto>(
            "{\"id\":3,\"name\":\"VMS\",\"type_server\":\"VMS\",\"sort_order\":1}")!;
        Assert.Equal("VMS", dto.TypeServer);

        // 직렬화는 6.3 본문 그대로 — category_server 는 싣지 않는다(7.0+ 본문은 ServerApiService 가 따로 조립)
        var json = Newtonsoft.Json.Linq.JObject.FromObject(dto);
        Assert.Equal("VMS", (string?)json["type_server"]);
        Assert.False(json.ContainsKey("category_server"));
    }
    #endregion

    #region - 함체 계측 삭제 결과 -
    [Fact]
    public async Task 함체계측삭제_data가_null이면_문구의_삭제수로_합성한다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);
        http.ResponseJson = "{\"success\":true,\"message\":\"Deleted 3 metrics for enclosure 77\",\"data\":null}";

        var resp = await svc.DeleteEnclosureMetricsAsync(77);

        Assert.True(resp.Success);
        Assert.NotNull(resp.Data);
        Assert.Equal(3, resp.Data!.DeletedCount);
        Assert.Equal(77, resp.Data.EnclosureId);
        Assert.Equal("DELETE", http.Method);
        Assert.Equal($"{BaseUrl}/devices/enclosures/77/metrics", http.Endpoint);
    }

    [Fact]
    public async Task 함체계측삭제_data가_오면_그대로_쓴다()
    {
        var (svc, http) = Create(EnumServerContract.V6_3);
        http.ResponseJson = "{\"success\":true,\"message\":\"ok 9\",\"data\":{\"enclosure_id\":77,\"deleted_count\":2}}";

        var resp = await svc.DeleteEnclosureMetricsAsync(77);

        Assert.True(resp.Success);
        Assert.Equal(2, resp.Data!.DeletedCount);
    }

    [Fact]
    public async Task 함체계측삭제_실패응답은_합성하지_않는다()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);
        http.ResponseStatus = HttpStatusCode.NotFound;
        http.ResponseJson = "{\"success\":false,\"message\":\"Enclosure 77 not found\",\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"Enclosure 77 not found\"}}";

        var resp = await svc.DeleteEnclosureMetricsAsync(77);

        Assert.False(resp.Success);
        Assert.Null(resp.Data);
    }
    #endregion
}

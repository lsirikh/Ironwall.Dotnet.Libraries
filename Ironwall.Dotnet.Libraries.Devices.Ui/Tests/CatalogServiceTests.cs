using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-05 — 장비 어휘 카탈로그(<c>GET /api/devices/spec</c>) 캐시. 종류축 콤보·부품 어휘의 <b>단일 원천</b>이다
/// (코드 상수 금지, AD-10). 세션당 1회 읽고, <c>SYNC_CATALOG</c> 통지에만 다시 읽는다.
/// </summary>
public class CatalogServiceTests
{
    // 8.0.1 테스트 서버 실측 형태(2026-09-19)를 줄인 것.
    private const string CatalogJson = """
        {
          "vocabularies": {
            "component_type": [
              { "code": "DOOR_ACTUATOR", "label": "도어 구동부", "deprecated_at": null, "applies_to": ["enclosure", "gate"] },
              { "code": "HEATER", "label": "히터", "deprecated_at": null, "applies_to": ["enclosure", "camera"] },
              { "code": "OLD_PART", "label": "구형 부품", "deprecated_at": "2026-08-01T00:00:00+09:00", "applies_to": ["camera"] }
            ]
          },
          "strict_vocabularies": {
            "connection_type": [ { "code": "IP_DIRECT", "label": "IP_DIRECT" }, { "code": "CONTROLLER_CONTACT", "label": "CONTROLLER_CONTACT" } ]
          },
          "categories": [
            { "category_device": "camera",
              "type_axis": { "field": "type_camera", "required_on_create": true, "nullable": false,
                             "values": [ { "code": "FIXED", "label": "고정형" }, { "code": "PTZ", "label": "PTZ" }, { "code": "SPEED_DOME", "label": "SPEED_DOME" } ] },
              "extra_axes": [] },
            { "category_device": "speaker",
              "type_axis": { "field": "type_speaker", "required_on_create": false, "unknown_code": "Unknown", "default": "Unknown",
                             "values": [ { "code": "Horn", "label": "Horn" }, { "code": "Pillar", "label": "Pillar" }, { "code": "Unknown", "label": "Unknown" } ] },
              "extra_axes": [ { "field": "speaker_role", "label": "역할", "default": "NORMAL",
                                "values": [ { "code": "NORMAL", "label": "NORMAL" }, { "code": "ADMIN", "label": "ADMIN" } ] } ] }
          ]
        }
        """;

    private static ApiResponse<DeviceSpecCatalogDto> Ok()
        => ApiResponse<DeviceSpecCatalogDto>.CreateSuccess(JsonConvert.DeserializeObject<DeviceSpecCatalogDto>(CatalogJson)!);

    private static (CatalogService service, MockDeviceApiService api) Create(EnumServerContract contract)
    {
        var api = new MockDeviceApiService { CatalogResponseFactory = Ok };
        var policy = new DeviceQueryPolicy(new FixedProbe(contract));
        return (new CatalogService(api, policy, log: null), api);
    }

    [Fact]
    public async Task should_load_catalog_once_when_ensure_loaded_is_called_repeatedly()
    {
        var (service, api) = Create(EnumServerContract.V8_0);

        Assert.True(await service.EnsureLoadedAsync());
        Assert.True(await service.EnsureLoadedAsync());
        Assert.True(await service.EnsureLoadedAsync());

        Assert.Equal(1, api.CatalogCallCount);
        Assert.True(service.IsLoaded);
    }

    [Fact]
    public async Task should_share_one_request_when_callers_overlap()
    {
        // 7개 패널이 동시에 열리며 카탈로그를 청한다 — 요청은 한 번만 나가야 한다.
        var (service, api) = Create(EnumServerContract.V8_0);
        var gate = new TaskCompletionSource();
        api.CatalogGate = gate.Task;

        var first = service.EnsureLoadedAsync();
        var second = service.EnsureLoadedAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, api.CatalogCallCount);
    }

    [Fact]
    public async Task should_reload_and_notify_when_refresh_is_requested()
    {
        // SYNC_CATALOG: 판 번호를 비교하지 않는다 — 통지가 왔으면 전량 다시 읽는다.
        var (service, api) = Create(EnumServerContract.V8_0);
        var changed = 0;
        service.CatalogChanged += (_, _) => changed++;

        await service.EnsureLoadedAsync();
        await service.RefreshAsync();

        Assert.Equal(2, api.CatalogCallCount);
        Assert.Equal(2, changed);
    }

    [Fact]
    public async Task should_not_call_server_and_stay_empty_when_contract_is_legacy()
    {
        // 6.3 에는 /spec 이 없다 — 부르지도 않고, 화면은 옛 enum 콤보를 그대로 쓴다.
        var (service, api) = Create(EnumServerContract.V6_3);

        var loaded = await service.EnsureLoadedAsync();

        Assert.False(loaded);
        Assert.False(service.IsLoaded);
        Assert.Equal(0, api.CatalogCallCount);
        Assert.Empty(service.TypeAxisValues(EnumDeviceCategory.Camera));
    }

    [Fact]
    public async Task should_expose_type_axis_values_and_rules_per_category()
    {
        var (service, _) = Create(EnumServerContract.V8_0);
        await service.EnsureLoadedAsync();

        var camera = service.TypeAxisValues(EnumDeviceCategory.Camera);
        Assert.Equal(new[] { "FIXED", "PTZ", "SPEED_DOME" }, camera.Select(o => o.Code));
        Assert.Equal("고정형", camera[0].Label);

        Assert.True(service.TypeAxis(EnumDeviceCategory.Camera)!.RequiredOnCreate);
        Assert.Equal("type_camera", service.TypeAxis(EnumDeviceCategory.Camera)!.Field);

        var speaker = service.TypeAxis(EnumDeviceCategory.Speaker)!;
        Assert.False(speaker.RequiredOnCreate);
        Assert.Equal("Unknown", speaker.UnknownCode);
        Assert.Equal("Unknown", speaker.DefaultCode);

        Assert.Null(service.TypeAxis(EnumDeviceCategory.Lamp));              // 카탈로그에 없는 카테고리
        Assert.Empty(service.TypeAxisValues(EnumDeviceCategory.Lamp));
    }

    [Fact]
    public async Task should_expose_extra_axes_for_speaker_only()
    {
        var (service, _) = Create(EnumServerContract.V8_0);
        await service.EnsureLoadedAsync();

        var role = Assert.Single(service.ExtraAxes(EnumDeviceCategory.Speaker));
        Assert.Equal("speaker_role", role.Field);
        Assert.Equal("NORMAL", role.DefaultCode);
        Assert.Equal(new[] { "NORMAL", "ADMIN" }, role.Values.Select(o => o.Code));

        Assert.Empty(service.ExtraAxes(EnumDeviceCategory.Camera));
    }

    [Fact]
    public async Task should_hide_deprecated_vocabulary_from_new_selection_but_still_resolve_its_label()
    {
        var (service, _) = Create(EnumServerContract.V8_0);
        await service.EnsureLoadedAsync();

        var selectable = service.Vocabulary("component_type");
        var all = service.Vocabulary("component_type", includeDeprecated: true);

        Assert.DoesNotContain(selectable, o => o.Code == "OLD_PART");        // 새로 고를 수는 없다
        Assert.Contains(all, o => o.Code == "OLD_PART" && o.IsDeprecated);   // 이미 쓰는 장비의 표시는 된다
        Assert.Equal("구형 부품", service.LabelOf("component_type", "OLD_PART"));
        Assert.Equal(new[] { "DOOR_ACTUATOR" }, service.Vocabulary("component_type", appliesTo: EnumDeviceCategory.Gate).Select(o => o.Code));
    }

    [Fact]
    public async Task should_fall_back_to_code_when_label_is_unknown()
    {
        var (service, _) = Create(EnumServerContract.V8_0);
        await service.EnsureLoadedAsync();

        Assert.Equal("고정형", service.TypeAxisLabel(EnumDeviceCategory.Camera, "fixed"));   // 코드 비교는 대소문자 무시
        Assert.Equal("THERMAL", service.TypeAxisLabel(EnumDeviceCategory.Camera, "THERMAL")); // 카탈로그에 없는 값 — 원값 그대로
        Assert.Equal(string.Empty, service.TypeAxisLabel(EnumDeviceCategory.Camera, null));
        Assert.True(service.IsTypeAxisValue(EnumDeviceCategory.Camera, "SPEED_DOME"));
        Assert.False(service.IsTypeAxisValue(EnumDeviceCategory.Camera, "THERMAL"));
    }

    [Fact]
    public async Task should_keep_previous_catalog_when_refresh_fails()
    {
        var (service, api) = Create(EnumServerContract.V8_0);
        await service.EnsureLoadedAsync();
        api.CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateError("INTERNAL_ERROR", "down");

        var refreshed = await service.RefreshAsync();

        Assert.False(refreshed);
        Assert.True(service.IsLoaded);                                        // 옛 어휘로 계속 동작
        Assert.Equal(3, service.TypeAxisValues(EnumDeviceCategory.Camera).Count);
    }

    [Fact]
    public async Task should_retry_on_next_ensure_when_first_load_failed()
    {
        var (service, api) = Create(EnumServerContract.V8_0);
        api.CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateError("INTERNAL_ERROR", "down");

        Assert.False(await service.EnsureLoadedAsync());
        api.CatalogResponseFactory = Ok;
        Assert.True(await service.EnsureLoadedAsync());

        Assert.Equal(2, api.CatalogCallCount);
    }

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
}

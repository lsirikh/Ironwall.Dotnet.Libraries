using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 프리셋으로 등록 창(device-assembly-preset FR-12 ~ FR-14) — 개체 정보만 묻고, 문제가 있으면 보내지 않고, 미리보기가 곧 보낼 내용이다.
/// 본문의 모양(부품이 실리는가 · 금지 8개가 없는가)은 <c>PresetRegisterTests</c> 가 고정한다.
/// </summary>
public class RegisterFromPresetViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-reg-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private DevicePresetStore NewStore(params DevicePreset[] presets)
    {
        var store = new DevicePresetStore(Path.Combine(_directory, "presets.json"), () => new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero));
        store.Load();
        foreach (var preset in presets) Assert.True(store.Save(preset).IsSuccess);
        return store;
    }

    private static DevicePreset LampPreset(string name = "경광등 표준") => new()
    {
        Id = DevicePresetStore.NewId(),
        Name = name,
        Category = EnumDeviceCategory.Lamp,
        Components = new[] { new ComponentDefinitionModel { Key = "lamp", Type = "LAMP_UNIT" } },
    };

    private static RegisterFromPresetViewModel NewVm(DevicePresetStore store, EnumDeviceCategory category, IEnumerable<int>? used = null, DevicePreset? fixedPreset = null, IEnumerable<IControllerDeviceModel>? controllers = null)
        => new(store, new OneTypeCatalog(), new PresetRegistrar(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService()),
            controllers ?? Array.Empty<IControllerDeviceModel>(), _ => (used ?? Array.Empty<int>()).ToHashSet(), category, fixedPreset);

    [Fact]
    public void should_suggest_the_first_free_number_and_a_name_when_opened()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp, used: new[] { 1, 2, 4 });

        Assert.Equal("3", vm.DeviceNumber);
        Assert.Contains("경광등", vm.DeviceName);
    }

    [Fact]
    public void should_select_the_first_preset_of_the_category_and_show_what_will_be_sent()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        Assert.Equal("경광등 표준", vm.SelectedPreset!.Name);
        Assert.Contains("LAMP_UNIT", vm.PreviewJson);               // 부품이 본문에 실린다 — 보이는 것이 곧 나가는 것
        Assert.True(vm.CanRegister);
    }

    [Fact]
    public void should_refuse_a_number_that_is_already_in_use()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp, used: new[] { 1 });

        vm.DeviceNumber = "1";

        Assert.Contains(vm.Problems, p => p.Contains("이미 쓰고 있습니다"));
        Assert.False(vm.CanRegister);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public void should_refuse_a_number_that_is_not_a_number(string text)
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        vm.DeviceNumber = text;

        Assert.True(vm.HasProblems);
        Assert.False(vm.CanRegister);
    }

    [Fact]
    public void should_refuse_an_empty_name()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        vm.DeviceName = "   ";

        Assert.True(vm.HasProblems);
        Assert.False(vm.CanRegister);
    }

    /// <summary>
    /// ★ 이 창에 설명 입력칸이 없어 <c>ReadInfo()</c> 가 <c>Description</c> 을 채우지 않던 결함(e8fd8c1c 의 절반)
    /// — DTO 파이프는 7개 카테고리 전부에서 이미 동작하지만(<c>PresetRegisterTests</c>), 창이 아무것도 태우지 않았다.
    /// </summary>
    [Fact]
    public void should_carry_the_typed_description_into_the_request_body_when_registering_from_a_preset()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        vm.Description = "북측 9구간 경광등";

        var body = JObject.Parse(vm.PreviewJson);
        Assert.Equal("북측 9구간 경광등", (string?)body["description"]);
        Assert.True(vm.CanRegister);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void should_omit_the_description_key_when_left_blank_or_whitespace(string text)
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        vm.Description = text;

        var body = JObject.Parse(vm.PreviewJson);
        Assert.Null(body["description"]);   // null 이나 빈 문자열이 아니라 키 자체가 없어야 한다
        Assert.True(vm.CanRegister);        // 선택 항목 — 비워도 등록을 막지 않는다
    }

    [Fact]
    public void should_require_a_saved_controller_when_registering_a_sensor()
    {
        var preset = LampPreset("센서") with { Category = EnumDeviceCategory.Sensor };
        var saved = new ControllerDeviceModel { Id = 5, DeviceNumber = 1, DeviceName = "제어기 1" };
        var draft = new ControllerDeviceModel { Id = 0, DeviceNumber = 2, DeviceName = "새 제어기" };
        var vm = NewVm(NewStore(preset), EnumDeviceCategory.Sensor, controllers: new IControllerDeviceModel[] { saved, draft });

        Assert.True(vm.ShowsController);
        Assert.Equal(new[] { saved }, vm.Controllers.ToArray());     // 저장 전 제어기는 고를 수 없다
        Assert.False(vm.CanRegister);

        vm.Controller = saved;
        Assert.True(vm.CanRegister);
    }

    [Fact]
    public void should_hide_the_preset_list_when_the_assembly_handed_over_a_preset()
    {
        var handedOver = LampPreset("(조립기)");
        var vm = NewVm(NewStore(), EnumDeviceCategory.Enclosure, fixedPreset: handedOver);

        Assert.False(vm.ShowsPresetList);
        Assert.Same(handedOver, vm.SelectedPreset);
        Assert.Equal(EnumDeviceCategory.Lamp, vm.Category);           // 카테고리는 프리셋을 따른다

        vm.Category = EnumDeviceCategory.Camera;
        Assert.Equal(EnumDeviceCategory.Lamp, vm.Category);
    }

    [Fact]
    public void should_block_a_preset_whose_type_left_the_catalog()
    {
        var preset = LampPreset() with { Components = new[] { new ComponentDefinitionModel { Key = "old", Type = "RETIRED_TYPE" } } };
        var vm = NewVm(NewStore(preset), EnumDeviceCategory.Lamp);

        Assert.True(vm.HasProblems);                                  // 말없이 빼고 보내지 않는다
        Assert.False(vm.CanRegister);
    }

    [Fact]
    public void should_show_connection_fields_only_for_categories_that_have_them()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);
        Assert.True(vm.ShowsConnection);

        vm.Category = EnumDeviceCategory.Gate;
        Assert.False(vm.ShowsConnection);
        Assert.False(vm.ShowsController);
    }

    [Fact]
    public async Task should_load_the_catalog_before_validating_when_the_window_opens()
    {
        // 장비 창을 열자마자 이 창부터 열면 아직 아무도 카탈로그를 안 읽었다 — 안 읽힌 채면 "사라진 유형" 검사가 통째로 건너뛰어진다.
        var preset = LampPreset() with { Components = new[] { new ComponentDefinitionModel { Key = "old", Type = "RETIRED_TYPE" } } };
        var catalog = new LazyCatalog();
        var vm = new RegisterFromPresetViewModel(NewStore(preset), catalog,
            new PresetRegistrar(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService()),
            Array.Empty<IControllerDeviceModel>(), _ => new HashSet<int>(), EnumDeviceCategory.Lamp);

        await ((Caliburn.Micro.IActivate)vm).ActivateAsync();

        Assert.Equal(1, catalog.LoadCalls);
        Assert.True(vm.HasProblems);
        Assert.False(vm.CanRegister);
    }

    // ── 종류(type_<category>) — 조립기 [새로 조립] 프리셋은 종류가 비어 있다 ──

    // 실서버 /spec 모양을 줄인 것 — 카메라는 종류 필수, 함체(형상 축)는 선택 + 기본값 Unknown.
    private const string TypeCatalogJson = """
        {
          "vocabularies": { "component_type": [
            { "code": "HEATER", "label": "히터", "deprecated_at": null, "applies_to": ["enclosure", "camera"] } ] },
          "categories": [
            { "category_device": "camera",
              "type_axis": { "field": "type_camera", "required_on_create": true, "nullable": false,
                             "values": [ { "code": "FIXED", "label": "고정형" }, { "code": "PTZ", "label": "PTZ" } ] },
              "extra_axes": [] },
            { "category_device": "enclosure",
              "type_axis": { "field": "type_enclosure", "required_on_create": false, "unknown_code": "Unknown", "default": "Unknown",
                             "values": [ { "code": "Outdoor", "label": "옥외" }, { "code": "Unknown", "label": "미확인" } ] },
              "extra_axes": [] }
          ]
        }
        """;

    private static async Task<Services.CatalogService> LoadedTypeCatalogAsync()
    {
        var api = new MockDeviceApiService
        {
            CatalogResponseFactory = () => Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiResponse<Ironwall.Dotnet.Libraries.Devices.Api.Models.DeviceSpecCatalogDto>
                .CreateSuccess(Newtonsoft.Json.JsonConvert.DeserializeObject<Ironwall.Dotnet.Libraries.Devices.Api.Models.DeviceSpecCatalogDto>(TypeCatalogJson)!),
        };
        var catalog = new Services.CatalogService(api, new Helpers.DeviceQueryPolicy(new V8Probe()), log: null);
        Assert.True(await catalog.EnsureLoadedAsync());
        return catalog;
    }

    private RegisterFromPresetViewModel NewTypedVm(Services.CatalogService catalog, DevicePreset preset)
        => new(NewStore(preset), catalog, new PresetRegistrar(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService()),
            Array.Empty<IControllerDeviceModel>(), _ => new HashSet<int>(), preset.Category);

    private static DevicePreset CameraPreset(string? type) => new()
    {
        Id = DevicePresetStore.NewId(),
        Name = "조립기에서 만든 카메라",
        Category = EnumDeviceCategory.Camera,
        TypeAxisCode = type,
        Components = new[] { new ComponentDefinitionModel { Key = "heater", Type = "HEATER" } },
    };

    [Fact]
    public async Task should_ask_for_a_type_instead_of_sending_none_when_the_preset_has_no_type()
    {
        // 실창 SC-CS-sensor-05: 종류 없는 프리셋을 그대로 보내 서버가 type_sensor 'NONE' 으로 거절했다.
        var vm = NewTypedVm(await LoadedTypeCatalogAsync(), CameraPreset(type: null));

        Assert.True(vm.ShowsType);
        Assert.Equal("종류 *", vm.TypeHeader);
        Assert.Contains("종류를 고르세요.", vm.Problems);
        Assert.False(vm.CanRegister);

        vm.TypeCode = "PTZ";

        Assert.DoesNotContain("종류를 고르세요.", vm.Problems);
        Assert.True(vm.CanRegister);
        Assert.Equal("PTZ", (string?)JObject.Parse(vm.PreviewJson)["type_camera"]);
    }

    [Fact]
    public async Task should_fill_the_type_from_the_preset_with_the_catalog_spelling()
    {
        var vm = NewTypedVm(await LoadedTypeCatalogAsync(), CameraPreset(type: "ptz"));

        Assert.Equal("PTZ", vm.TypeCode);
        Assert.True(vm.CanRegister);
    }

    [Fact]
    public async Task should_use_the_catalog_default_when_an_optional_type_is_missing()
    {
        var preset = new DevicePreset
        {
            Id = DevicePresetStore.NewId(),
            Name = "조립기에서 만든 함체",
            Category = EnumDeviceCategory.Enclosure,
            Components = new[] { new ComponentDefinitionModel { Key = "heater", Type = "HEATER" } },
        };

        var vm = NewTypedVm(await LoadedTypeCatalogAsync(), preset);

        Assert.Equal("종류", vm.TypeHeader);
        Assert.Equal("Unknown", vm.TypeCode);
        Assert.True(vm.CanRegister);
    }

    private sealed class V8Probe : Ironwall.Dotnet.Libraries.Api.Services.IServerContractProbe
    {
        public Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract Contract => Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V8_0;
        public string? RawVersion => "8.0";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class LazyCatalog : IComponentCatalog
    {
        public int LoadCalls { get; private set; }
        public bool IsLoaded { get; private set; }
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) { LoadCalls++; IsLoaded = true; return Task.FromResult(true); }
        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => Array.Empty<ComponentTypeInfo>();
        public ComponentTypeInfo? Find(string? code) => null;
    }

    private sealed class OneTypeCatalog : IComponentCatalog
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => new[] { new ComponentTypeInfo { Code = "LAMP_UNIT", Label = "경광등" } };
        public ComponentTypeInfo? Find(string? code) => string.Equals(code, "LAMP_UNIT", StringComparison.OrdinalIgnoreCase) ? new ComponentTypeInfo { Code = "LAMP_UNIT", Label = "경광등" } : null;
    }
}

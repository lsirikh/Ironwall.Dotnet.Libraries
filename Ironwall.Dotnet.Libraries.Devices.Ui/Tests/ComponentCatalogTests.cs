using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Blocks;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Catalog;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-03 조립기의 <b>카탈로그 쪽</b> — 가족 규칙표(FR-04) · 카탈로그 읽기 · <c>IComponentCatalog</c> 팔레트 ·
/// 블록 도형 · 팔레트 키보드 폴백(FR-03).
/// </summary>
/// <remarks>
/// 전부 <b>헤드리스</b>다 — 도형은 시각트리 없이 <see cref="System.Windows.Media.StreamGeometry"/> 로 만들고,
/// 키 판정은 순수 함수로 뽑아 실제 창 없이 단언한다(UIA 에 드래그 패턴이 없어 제스처는 애초에 단언 대상이 아니다).
/// </remarks>
public class ComponentCatalogTests
{
    #region - 가족 규칙표 (목업 32종을 못으로 박는다) -

    // 목업 device-component-assembly-preset-storyboard.html 의 CAT 배열 32종 전수.
    // 이 표가 바뀌면 팔레트의 생김새가 바뀐다 — 바꾸려면 이 테스트를 먼저 고쳐야 한다.
    [Theory]
    // 감지 — 바깥을 읽는 것(11종)
    [InlineData("DOOR_SENSOR", ComponentFamily.Sensing)]
    [InlineData("LIMIT_SWITCH", ComponentFamily.Sensing)]
    [InlineData("VIBRATION_METER", ComponentFamily.Sensing)]
    [InlineData("PIR_SENSOR", ComponentFamily.Sensing)]
    [InlineData("ULTRASONIC_SENSOR", ComponentFamily.Sensing)]
    [InlineData("RADAR_UNIT", ComponentFamily.Sensing)]
    [InlineData("VIBRATION_SENSOR", ComponentFamily.Sensing)]
    [InlineData("OPTICAL_FIBER_SENSOR", ComponentFamily.Sensing)]
    [InlineData("THERMAL_SENSOR", ComponentFamily.Sensing)]
    [InlineData("CONTACT_INPUT", ComponentFamily.Sensing)]
    [InlineData("MIC", ComponentFamily.Sensing)]
    // 구동 — 바깥으로 내보내는 것(5종)
    [InlineData("DOOR_LOCK", ComponentFamily.Actuation)]
    [InlineData("DOOR_ACTUATOR", ComponentFamily.Actuation)]
    [InlineData("LAMP_LIGHT", ComponentFamily.Actuation)]
    [InlineData("BUZZER", ComponentFamily.Actuation)]
    [InlineData("AMPLIFIER", ComponentFamily.Actuation)]
    // 전원 · 환경 — 함체 살림(7종)
    [InlineData("UPS", ComponentFamily.PowerEnvironment)]
    [InlineData("VOLTAGE_SENSOR", ComponentFamily.PowerEnvironment)]
    [InlineData("CURRENT_SENSOR", ComponentFamily.PowerEnvironment)]
    [InlineData("TEMPERATURE_SENSOR", ComponentFamily.PowerEnvironment)]
    [InlineData("HUMIDITY_SENSOR", ComponentFamily.PowerEnvironment)]
    [InlineData("HEATER", ComponentFamily.PowerEnvironment)]
    [InlineData("FAN", ComponentFamily.PowerEnvironment)]
    // 광학 — 광축 위의 것(8종)
    [InlineData("THERMAL_CAMERA", ComponentFamily.Optics)]
    [InlineData("EO_CAMERA", ComponentFamily.Optics)]
    [InlineData("OPTICAL_LENS", ComponentFamily.Optics)]
    [InlineData("PTZ_UNIT", ComponentFamily.Optics)]
    [InlineData("IR_LED", ComponentFamily.Optics)]
    [InlineData("HEADLIGHT", ComponentFamily.Optics)]
    [InlineData("WIPER", ComponentFamily.Optics)]
    [InlineData("TRACKER", ComponentFamily.Optics)]
    // 네트워크 — 링크(1종, 보드에서 늘 맨 끝)
    [InlineData("NETWORK_INTERFACE", ComponentFamily.Network)]
    public void should_map_the_catalog_code_to_its_documented_family_when_rules_are_applied(string code, ComponentFamily expected)
    {
        Assert.Equal(expected, ComponentFamilyRules.FamilyOf(code));
    }

    [Fact]
    public void should_cover_all_thirty_two_mockup_codes_when_families_are_counted()
    {
        // 목업의 32종이 한 가족도 빠짐없이 갈린다 — 가족별 머릿수까지 못으로 박는다.
        var families = MockupCodes.Select(ComponentFamilyRules.FamilyOf).ToList();

        Assert.Equal(32, families.Count);
        Assert.Equal(11, families.Count(f => f == ComponentFamily.Sensing));
        Assert.Equal(5, families.Count(f => f == ComponentFamily.Actuation));
        Assert.Equal(7, families.Count(f => f == ComponentFamily.PowerEnvironment));
        Assert.Equal(8, families.Count(f => f == ComponentFamily.Optics));
        Assert.Single(families, f => f == ComponentFamily.Network);
        Assert.DoesNotContain(ComponentFamily.Other, families);   // 아는 코드가 '기타'로 새면 모양이 뭉개진다
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("WHATEVER_NEW_THING")]
    [InlineData("__")]
    public void should_return_other_when_code_is_unknown_or_blank(string? code)
    {
        Assert.Equal(ComponentFamily.Other, ComponentFamilyRules.FamilyOf(code));
    }

    [Theory]
    [InlineData("door_sensor", ComponentFamily.Sensing)]
    [InlineData("  Network_Interface  ", ComponentFamily.Network)]
    [InlineData("eo_camera", ComponentFamily.Optics)]
    public void should_match_case_insensitively_when_code_is_not_upper_cased(string code, ComponentFamily expected)
    {
        Assert.Equal(expected, ComponentFamilyRules.FamilyOf(code));
    }

    [Fact]
    public void should_expose_the_rule_table_when_documentation_asks_for_it()
    {
        var rules = ComponentFamilyRules.Rules;

        Assert.NotEmpty(rules);
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Pattern)));
        // '기타'는 규칙이 아니라 폴백이다 — 표에 들어 있으면 그 뒤의 줄이 영영 안 걸린다.
        Assert.DoesNotContain(rules, r => r.Family == ComponentFamily.Other);
        foreach (var family in new[] { ComponentFamily.Sensing, ComponentFamily.Actuation, ComponentFamily.PowerEnvironment, ComponentFamily.Optics, ComponentFamily.Network })
            Assert.Contains(rules, r => r.Family == family);
    }

    [Fact]
    public void should_put_network_last_when_families_are_ordered_for_the_palette()
    {
        var order = Enum.GetValues<ComponentFamily>().OrderBy(ComponentFamilyRules.PaletteOrder).ToArray();
        Assert.Equal(ComponentFamily.Network, order[^1]);
        Assert.Equal(ComponentFamily.Sensing, order[0]);
    }
    #endregion

    #region - 카탈로그 한 줄 읽기 (절대 던지지 않는다) -

    [Fact]
    public void should_read_every_list_when_definition_holds_string_arrays()
    {
        var entry = new VocabularyEntryDto
        {
            Code = "door_lock",
            Label = "도어 잠금",
            AppliesTo = new List<string> { "enclosure", "gate" },
            Definition = JObject.Parse("""
                { "states": ["LOCKED","UNLOCKED"], "commands": ["LOCK","UNLOCK"],
                  "produces": [], "override_params": ["hold_ms"] }
                """),
        };

        var info = ComponentCatalogReader.Read(entry);

        Assert.Equal("DOOR_LOCK", info.Code);                       // 코드는 대문자로 정규화한다
        Assert.Equal("도어 잠금", info.Label);
        Assert.Equal(ComponentFamily.Actuation, info.Family);
        Assert.Equal(new[] { "LOCKED", "UNLOCKED" }, info.States);
        Assert.Equal(new[] { "LOCK", "UNLOCK" }, info.Commands);
        Assert.Empty(info.Produces);
        Assert.Equal(new[] { "hold_ms" }, info.OverrideParams);
        Assert.True(info.ReportsState);
        Assert.False(info.IsDeprecated);
    }

    /// <summary>
    /// ★ 실제 서버 모양 — 로컬 8.0.2 의 <c>GET /api/devices/spec</c> 응답에서 그대로 옮긴 줄이다(라이브 하네스 asm.0, 2026-09-24).
    /// <c>override_params</c> 는 <b>이름을 키로 삼는 사전</b>이다(<c>component_definition.py</c>: <c>dict[str, ParameterDefinition]</c>).
    /// 배열 픽스처만 있던 동안 이 모양은 한 객체로 읽혀 이름이 0개였고, 조립기 속성 칸의 재정의 줄이 실서버에서 늘 비었다(asm.R1).
    /// </summary>
    [Theory]
    [InlineData("""{"deprecated_at":null,"code":"HEATER","label":"히터","applies_to":["enclosure","camera"],"definition":{"states":["ON","OFF"],"commands":["ON","OFF"],"readable":true,"controllable":true,"override_params":{"enabled":{"value_type":"bool"}}}}""", "enabled")]
    [InlineData("""{"deprecated_at":null,"code":"LAMP_LIGHT","label":"경광등","applies_to":["lamp"],"definition":{"states":["ON","OFF"],"commands":["ON","OFF","SET_COLOR","SET_MODE"],"readable":true,"controllable":true,"command_params":{"SET_MODE":{"mode":{"required":true,"value_type":"enum","enum_values":["steady","blinking"]}},"SET_COLOR":{"color":{"required":true,"value_type":"enum","enum_values":["Red","Orange","Green","Blue","White"]}}},"override_params":{"color":{"value_type":"enum","enum_values":["Red","Orange","Green","Blue","White"]}}}}""", "color")]
    public void should_read_override_param_names_when_the_live_server_sends_them_as_a_dictionary(string entryJson, string expected)
    {
        var entry = Newtonsoft.Json.JsonConvert.DeserializeObject<VocabularyEntryDto>(entryJson)!;

        var info = ComponentCatalogReader.Read(entry);

        Assert.Equal(new[] { expected }, info.OverrideParams);
        Assert.Equal(new[] { "ON", "OFF" }, info.States);       // 배열 칸은 예전 그대로
    }

    [Fact]
    public void should_read_every_key_in_order_when_a_definition_list_is_a_dictionary()
    {
        var names = ComponentCatalogReader.ReadNames(
            JObject.Parse("""{ "override_params": { "enabled": {"value_type":"bool"}, "on_below_c": {"value_type":"number"} } }"""), "override_params");

        Assert.Equal(new[] { "enabled", "on_below_c" }, names);
    }

    [Theory]
    [InlineData("""{ "states": ["OPEN","CLOSED"] }""")]                                   // 문자열 배열
    [InlineData("""{ "states": [{"code":"OPEN"},{"code":"CLOSED"}] }""")]                 // {code} 객체 배열
    [InlineData("""{ "states": [{"name":"OPEN"},{"name":"CLOSED"}] }""")]                 // {name} 객체 배열
    [InlineData("""{ "STATES": ["OPEN","CLOSED"] }""")]                                   // 키 대소문자가 달라도
    public void should_read_the_same_names_when_definition_shape_varies(string json)
    {
        var names = ComponentCatalogReader.ReadNames(JObject.Parse(json), "states");
        Assert.Equal(new[] { "OPEN", "CLOSED" }, names);
    }

    [Fact]
    public void should_read_one_name_when_definition_holds_a_single_string()
    {
        var names = ComponentCatalogReader.ReadNames(JObject.Parse("""{ "states": "OPEN" }"""), "states");
        Assert.Equal(new[] { "OPEN" }, names);
    }

    [Theory]
    [InlineData("""{ }""")]                                    // 키 없음
    [InlineData("""{ "states": null }""")]
    [InlineData("""{ "states": [] }""")]
    [InlineData("""{ "states": {} }""")]                       // 객체인데 code/name 이 없다
    [InlineData("""{ "states": true }""")]
    [InlineData("""{ "states": [null, "  "] }""")]
    [InlineData("""{ "states": [[["A"]]] }""")]                // 너무 깊은 중첩은 접는다
    public void should_return_empty_and_never_throw_when_definition_is_odd(string json)
    {
        var names = ComponentCatalogReader.ReadNames(JObject.Parse(json), "states");
        Assert.Empty(names);
    }

    [Fact]
    public void should_return_empty_when_definition_or_name_is_missing()
    {
        Assert.Empty(ComponentCatalogReader.ReadNames(null, "states"));
        Assert.Empty(ComponentCatalogReader.ReadNames(JObject.Parse("""{ "states": ["A"] }"""), "   "));
    }

    [Fact]
    public void should_report_no_state_when_states_are_absent()
    {
        var info = ComponentCatalogReader.Read(new VocabularyEntryDto
        {
            Code = "OPTICAL_LENS",
            Label = "광학 렌즈",
            AppliesTo = new List<string> { "camera" },
            Definition = JObject.Parse("""{ "commands": [] }"""),
        });

        Assert.False(info.ReportsState);          // ⊘ 표지가 붙는 유형이다
        Assert.Empty(info.States);
        Assert.Equal(ComponentFamily.Optics, info.Family);
    }

    [Fact]
    public void should_map_applies_to_and_drop_unknown_names_when_reading()
    {
        var info = ComponentCatalogReader.Read(new VocabularyEntryDto
        {
            Code = "DOOR_SENSOR",
            Label = "도어 센서",
            AppliesTo = new List<string> { "Enclosure", "gate", "warp_core", "" },
        });

        Assert.NotNull(info.AppliesTo);
        Assert.Equal(2, info.AppliesTo!.Count);
        Assert.True(info.AppliesToCategory(EnumDeviceCategory.Enclosure));
        Assert.True(info.AppliesToCategory(EnumDeviceCategory.Gate));
        Assert.False(info.AppliesToCategory(EnumDeviceCategory.Camera));
    }

    [Theory]
    [InlineData(true)]     // applies_to 가 아예 없음
    [InlineData(false)]    // applies_to 가 빈 목록
    public void should_treat_applies_to_as_all_categories_when_it_is_absent_or_empty(bool absent)
    {
        var info = ComponentCatalogReader.Read(new VocabularyEntryDto
        {
            Code = "NETWORK_INTERFACE",
            Label = "네트워크 인터페이스",
            AppliesTo = absent ? null : new List<string>(),
        });

        Assert.Null(info.AppliesTo);                                            // null = 전 카테고리
        Assert.True(info.AppliesToCategory(EnumDeviceCategory.Controller));
        Assert.True(info.AppliesToCategory(EnumDeviceCategory.Lamp));
    }

    [Fact]
    public void should_flag_deprecated_when_deprecated_at_is_present()
    {
        var info = ComponentCatalogReader.Read(new VocabularyEntryDto
        {
            Code = "OLD_PART",
            Label = "구형 부품",
            DeprecatedAt = "2026-08-01T00:00:00+09:00",
        });

        Assert.True(info.IsDeprecated);
        Assert.Equal(ComponentFamily.Other, info.Family);
    }

    [Fact]
    public void should_fall_back_to_the_code_when_label_is_blank()
    {
        var info = ComponentCatalogReader.Read(new VocabularyEntryDto { Code = "buzzer", Label = "   " });
        Assert.Equal("BUZZER", info.Label);
        Assert.Equal("BUZZER", info.Display);      // 라벨 = 코드면 코드를 따로 보이지 않는다
    }

    [Fact]
    public void should_not_throw_when_entry_is_null()
    {
        var info = ComponentCatalogReader.Read(null!);
        Assert.Equal(string.Empty, info.Code);
        Assert.Equal(ComponentFamily.Other, info.Family);
        Assert.Empty(info.States);
    }
    #endregion

    #region - CatalogService 를 IComponentCatalog 로 (팔레트) -

    [Fact]
    public async Task should_filter_the_palette_by_category_when_component_types_are_requested()
    {
        IComponentCatalog catalog = Create(EnumServerContract.V8_0).service;
        await catalog.EnsureLoadedAsync();

        // 목업 §1 의 카테고리 표 그대로 — 제어기는 카탈로그상 2종뿐이다.
        Assert.Equal(new[] { "CONTACT_INPUT", "NETWORK_INTERFACE" },
                     catalog.ComponentTypes(EnumDeviceCategory.Controller).Select(t => t.Code));
        Assert.Equal(new[] { "AMPLIFIER", "MIC", "NETWORK_INTERFACE" },
                     catalog.ComponentTypes(EnumDeviceCategory.Speaker).Select(t => t.Code).OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(6, catalog.ComponentTypes(EnumDeviceCategory.Gate).Count);
        Assert.Equal(15, catalog.ComponentTypes(EnumDeviceCategory.Enclosure).Count);
        Assert.Equal(12, catalog.ComponentTypes(EnumDeviceCategory.Camera).Count);
        Assert.Equal(8, catalog.ComponentTypes(EnumDeviceCategory.Sensor).Count);
        Assert.Equal(3, catalog.ComponentTypes(EnumDeviceCategory.Lamp).Count);
    }

    [Fact]
    public async Task should_order_by_family_and_keep_network_last_when_palette_is_built()
    {
        IComponentCatalog catalog = Create(EnumServerContract.V8_0).service;
        await catalog.EnsureLoadedAsync();

        var palette = catalog.ComponentTypes(EnumDeviceCategory.Enclosure);

        var orders = palette.Select(t => ComponentFamilyRules.PaletteOrder(t.Family)).ToList();
        Assert.Equal(orders.OrderBy(o => o), orders);                  // 가족 순으로 묶여 있다
        Assert.Equal("NETWORK_INTERFACE", palette[^1].Code);           // 링크는 늘 맨 끝
        Assert.Equal(ComponentFamily.Sensing, palette[0].Family);
    }

    [Fact]
    public async Task should_hide_deprecated_types_unless_they_are_asked_for()
    {
        IComponentCatalog catalog = Create(EnumServerContract.V8_0).service;
        await catalog.EnsureLoadedAsync();

        Assert.DoesNotContain(catalog.ComponentTypes(EnumDeviceCategory.Camera), t => t.Code == "OLD_PART");
        Assert.Contains(catalog.ComponentTypes(EnumDeviceCategory.Camera, includeDeprecated: true), t => t.Code == "OLD_PART");
    }

    [Fact]
    public async Task should_find_by_code_case_insensitively_including_deprecated_types()
    {
        IComponentCatalog catalog = Create(EnumServerContract.V8_0).service;
        await catalog.EnsureLoadedAsync();

        Assert.Equal("DOOR_SENSOR", catalog.Find("door_sensor")!.Code);
        Assert.Equal("DOOR_SENSOR", catalog.Find("  DOOR_SENSOR ")!.Code);
        Assert.True(catalog.Find("old_part")!.IsDeprecated);            // 사라진 유형도 이름은 보여야 한다
        Assert.Null(catalog.Find("NO_SUCH_TYPE"));
        Assert.Null(catalog.Find(null));
        Assert.Null(catalog.Find("   "));
    }

    [Fact]
    public async Task should_stay_empty_and_not_call_server_when_contract_is_legacy()
    {
        // 6.3.2 에는 부품 모델 자체가 없다 — 조립기는 창을 감추고, 카탈로그는 부르지도 않는다.
        var (service, api) = Create(EnumServerContract.V6_3);
        IComponentCatalog catalog = service;

        Assert.False(await catalog.EnsureLoadedAsync());
        Assert.False(catalog.IsLoaded);
        Assert.Equal(0, api.CatalogCallCount);
        Assert.Empty(catalog.ComponentTypes(EnumDeviceCategory.Enclosure));
        Assert.Null(catalog.Find("DOOR_SENSOR"));
    }

    [Fact]
    public async Task should_serve_both_interfaces_from_one_load_when_either_is_used()
    {
        var (service, api) = Create(EnumServerContract.V8_0);

        Assert.True(await ((IComponentCatalog)service).EnsureLoadedAsync());
        Assert.True(await ((ICatalogService)service).EnsureLoadedAsync());

        Assert.Equal(1, api.CatalogCallCount);                                          // 적재는 하나다
        Assert.NotEmpty(service.Vocabulary("component_type"));                          // 옛 통로(코드 · 라벨만)
        Assert.NotEmpty(service.ComponentTypes(EnumDeviceCategory.Enclosure));          // 새 통로(definition 까지)
        Assert.Equal(33, service.Vocabulary("component_type", includeDeprecated: true).Count);   // 32종 + 폐기 1

        // 같은 판을 본다 — 옛 통로에 있는 코드는 새 통로에서도 찾힌다.
        foreach (var option in service.Vocabulary("component_type", includeDeprecated: true))
            Assert.NotNull(service.Find(option.Code));
    }

    [Fact]
    public async Task should_stay_empty_when_catalog_has_no_component_vocabulary()
    {
        var api = new MockDeviceApiService
        {
            CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateSuccess(new DeviceSpecCatalogDto()),
        };
        var service = new CatalogService(api, new DeviceQueryPolicy(new FixedProbe(EnumServerContract.V8_0)), log: null);

        Assert.True(await service.EnsureLoadedAsync());
        Assert.True(service.IsLoaded);
        Assert.Empty(service.ComponentTypes(EnumDeviceCategory.Enclosure));
        Assert.Null(service.Find("DOOR_SENSOR"));
    }
    #endregion

    #region - 블록 도형 -

    [Theory]
    [InlineData(ComponentFamily.Sensing)]
    [InlineData(ComponentFamily.Actuation)]
    [InlineData(ComponentFamily.PowerEnvironment)]
    [InlineData(ComponentFamily.Network)]
    [InlineData(ComponentFamily.Optics)]
    [InlineData(ComponentFamily.Other)]
    public void should_fit_a_closed_outline_inside_the_box_when_family_and_size_vary(ComponentFamily family)
    {
        var sizes = new[] { (140.0, 46.0), (120.0, 30.0), (220.0, 64.0), (40.0, 40.0), (16.0, 12.0) };

        foreach (var (w, h) in sizes)
        {
            var geometry = ComponentBlockGeometry.Outline(family, w, h);

            Assert.False(geometry.IsEmpty());
            var bounds = geometry.Bounds;
            Assert.True(bounds.Left >= -0.5, $"{family} {w}x{h} left={bounds.Left}");
            Assert.True(bounds.Top >= -0.5, $"{family} {w}x{h} top={bounds.Top}");
            Assert.True(bounds.Right <= w + 0.5, $"{family} {w}x{h} right={bounds.Right}");
            Assert.True(bounds.Bottom <= h + 0.5, $"{family} {w}x{h} bottom={bounds.Bottom}");

            // 네 변에 닿는다(상자를 채운다) — 안 닿으면 블록이 줄에서 뜬다.
            Assert.True(bounds.Width >= w - 0.5 && bounds.Height >= h - 0.5, $"{family} {w}x{h} bounds={bounds}");

            // 닫힌 도형이다 — 가운데가 칠해진다.
            Assert.True(geometry.FillContains(new Point(w / 2.0, h / 2.0)), $"{family} {w}x{h} not closed");
            Assert.True(geometry.IsFrozen);
        }
    }

    [Fact]
    public void should_return_an_empty_geometry_when_size_is_not_usable()
    {
        Assert.True(ComponentBlockGeometry.Outline(ComponentFamily.Sensing, 0, 40).IsEmpty());
        Assert.True(ComponentBlockGeometry.Outline(ComponentFamily.Sensing, 140, -3).IsEmpty());
        Assert.True(ComponentBlockGeometry.Outline(ComponentFamily.Sensing, double.NaN, 40).IsEmpty());
    }

    [Fact]
    public void should_expose_the_circle_mark_only_for_the_optics_family()
    {
        var mark = ComponentBlockGeometry.OpticsMark(ComponentFamily.Optics, 140, 46);

        Assert.NotNull(mark);
        Assert.True(mark!.Value.Radius > 0);
        Assert.InRange(mark.Value.Center.X, mark.Value.Radius, 140 - mark.Value.Radius);
        Assert.InRange(mark.Value.Center.Y, mark.Value.Radius, 46 - mark.Value.Radius);

        foreach (var family in Enum.GetValues<ComponentFamily>().Where(f => f != ComponentFamily.Optics))
            Assert.Null(ComponentBlockGeometry.OpticsMark(family, 140, 46));
    }

    [Fact]
    public void should_give_the_same_frozen_instance_when_family_and_size_repeat()
    {
        var first = ComponentBlockGeometry.Outline(ComponentFamily.Network, 137.0, 44.0);
        var again = ComponentBlockGeometry.Outline(ComponentFamily.Network, 137.0, 44.0);

        Assert.Same(first, again);      // 얼린 도형만 캐싱한다 — 브러시는 절대 캐싱하지 않는다
        Assert.True(first.IsFrozen);
    }
    #endregion

    #region - 팔레트 키보드 폴백 (FR-03) -

    [Theory]
    [InlineData(Key.Enter, false, true)]
    [InlineData(Key.Space, false, true)]
    [InlineData(Key.Enter, true, false)]     // 글 입력기가 포커스를 쥐면 양보한다
    [InlineData(Key.Space, true, false)]
    [InlineData(Key.Tab, false, false)]
    [InlineData(Key.Down, false, false)]
    [InlineData(Key.System, false, false)]   // Alt+↑/↓ 는 커널의 순서 이동 몫이다
    [InlineData(Key.Escape, false, false)]
    public void should_execute_only_on_enter_or_space_when_no_text_editor_has_focus(Key key, bool isTextInputFocused, bool expected)
    {
        Assert.Equal(expected, PaletteAddKeyboardBehavior.ShouldExecute(key, isTextInputFocused));
    }

    [Fact]
    public void should_treat_text_boxes_and_combos_as_editors_when_focus_is_checked()
    {
        Assert.False(PaletteAddKeyboardBehavior.IsTextInputFocused(null));

        // WPF 컨트롤 생성은 STA 를 요구한다(xUnit 은 MTA 풀 스레드에서 돈다) — 이 판정만 STA 로 잠깐 나간다.
        RunSta(() =>
        {
            Assert.True(PaletteAddKeyboardBehavior.IsTextInputFocused(new System.Windows.Controls.TextBox()));
            Assert.True(PaletteAddKeyboardBehavior.IsTextInputFocused(new System.Windows.Controls.ComboBox()));
            Assert.False(PaletteAddKeyboardBehavior.IsTextInputFocused(new System.Windows.Controls.Border()));
        });
    }

    [Fact]
    public void should_load_its_default_style_from_the_assembly_when_the_block_is_created()
    {
        // 호스트 앱이 아무 사전도 병합하지 않은 상태에서 Themes/Generic.xaml 이 스스로 실리는지 — 이게 깨지면
        // 블록이 아무 데서도 안 그려진다(증상은 "빈 네모"라 원인이 안 보인다).
        RunSta(() =>
        {
            var block = new ComponentBlock { Family = ComponentFamily.Optics, Title = "광학 렌즈", Subtitle = "lens" };

            // 코드로 만든 요소는 초기화 표시가 안 서고, 그러면 테마 스타일이 아예 조회되지 않는다.
            // XAML 파서가 하는 일을 그대로 해 준다(실제 뷰에서는 파서가 EndInit 을 부른다).
            block.BeginInit();
            block.EndInit();

            block.Measure(new Size(200, 60));
            block.Arrange(new Rect(0, 0, 200, 60));
            block.UpdateLayout();

            Assert.NotNull(block.Template);                     // Generic.xaml → ComponentBlock.xaml 이 실렸다
            Assert.NotNull(block.OutlineGeometry);
            Assert.False(block.OutlineGeometry!.IsEmpty());
            Assert.NotNull(block.OpticsMarkGeometry);           // 광학 가족만 원 표지를 갖는다

            block.Family = ComponentFamily.Network;
            Assert.Null(block.OpticsMarkGeometry);

            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(block);
            Assert.IsType<ComponentBlock.ComponentBlockAutomationPeer>(peer);
            Assert.Equal("광학 렌즈", peer.GetName());           // UIA 이름 = Title
        });
    }

    /// <summary>STA 전용 코드를 전용 스레드에서 돌리고 예외를 그대로 넘긴다(앱도 Dispatcher 도 띄우지 않는다).</summary>
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void should_not_execute_when_command_is_missing_or_refuses()
    {
        var behavior = new PaletteAddKeyboardBehavior();
        Assert.False(behavior.TryExecute("DOOR_SENSOR"));               // 명령이 없다

        behavior.Command = new StubCommand(canExecute: false);
        Assert.False(behavior.TryExecute("DOOR_SENSOR"));               // CanExecute 가 거절

        var command = new StubCommand(canExecute: true);
        behavior.Command = command;
        Assert.False(behavior.TryExecute(null));                        // 대상이 없다
        Assert.True(behavior.TryExecute("DOOR_SENSOR"));
        Assert.Equal("DOOR_SENSOR", command.LastParameter);
    }

    private sealed class StubCommand : ICommand
    {
        private readonly bool _canExecute;
        public StubCommand(bool canExecute) => _canExecute = canExecute;
        public object? LastParameter { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute;
        public void Execute(object? parameter) => LastParameter = parameter;
        private void Unused() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region - Fixture -

    /// <summary>빈 목록. <b>Catalog32 보다 먼저</b> 선언한다 — 정적 초기화는 선언 순서대로 돈다.</summary>
    private static readonly string[] Empty = Array.Empty<string>();

    /// <summary>목업 CAT 32종(코드 · 라벨 · applies_to · states · commands · produces).</summary>
    private static readonly (string Code, string Label, string[]? AppliesTo, string[] States, string[] Commands, string[] Produces)[] Catalog32 =
    {
        ("DOOR_SENSOR", "도어 센서", new[]{ "enclosure","gate" }, new[]{ "OPEN","CLOSED" }, Empty, Empty),
        ("DOOR_LOCK", "도어 잠금", new[]{ "enclosure","gate" }, new[]{ "LOCKED","UNLOCKED" }, new[]{ "LOCK","UNLOCK" }, Empty),
        ("DOOR_ACTUATOR", "문 구동부", new[]{ "gate","enclosure" }, new[]{ "OPEN","CLOSED","RUNNING" }, new[]{ "OPEN","CLOSE" }, Empty),
        ("LIMIT_SWITCH", "리미트 스위치", new[]{ "gate" }, Empty, Empty, Empty),
        ("TEMPERATURE_SENSOR", "온도 센서", new[]{ "enclosure" }, Empty, Empty, new[]{ "temperature" }),
        ("HUMIDITY_SENSOR", "습도 센서", new[]{ "enclosure" }, Empty, Empty, new[]{ "humidity" }),
        ("VOLTAGE_SENSOR", "전압 센서", new[]{ "enclosure" }, Empty, Empty, new[]{ "voltage" }),
        ("CURRENT_SENSOR", "전류 센서", new[]{ "enclosure" }, Empty, Empty, new[]{ "current" }),
        ("VIBRATION_METER", "진동 계측기", new[]{ "enclosure" }, Empty, Empty, new[]{ "vibration" }),
        ("UPS", "무정전 전원장치", new[]{ "enclosure" }, Empty, Empty, new[]{ "ups_battery_level","ups_charging" }),
        ("HEATER", "히터", new[]{ "enclosure","camera" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("FAN", "팬", new[]{ "enclosure","camera" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("HEADLIGHT", "전조등", new[]{ "camera","enclosure" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("PIR_SENSOR", "PIR 센서", new[]{ "sensor" }, new[]{ "IDLE","RUNNING" }, Empty, Empty),
        ("ULTRASONIC_SENSOR", "초음파 센서", new[]{ "sensor" }, new[]{ "IDLE","RUNNING" }, Empty, Empty),
        ("RADAR_UNIT", "레이더", new[]{ "sensor" }, new[]{ "IDLE","RUNNING" }, Empty, Empty),
        ("VIBRATION_SENSOR", "진동 센서", new[]{ "sensor" }, new[]{ "IDLE","RUNNING" }, Empty, Empty),
        ("OPTICAL_FIBER_SENSOR", "광케이블 감지부", new[]{ "sensor" }, new[]{ "IDLE","RUNNING" }, Empty, Empty),
        ("THERMAL_CAMERA", "열영상 카메라", new[]{ "sensor","camera" }, Empty, Empty, Empty),
        ("EO_CAMERA", "EO 카메라", new[]{ "sensor","camera" }, Empty, Empty, Empty),
        ("THERMAL_SENSOR", "열화상 센서", new[]{ "camera" }, Empty, Empty, Empty),
        ("CONTACT_INPUT", "접점 입력", new[]{ "controller","enclosure","gate" }, new[]{ "ON","OFF" }, Empty, Empty),
        ("LAMP_LIGHT", "경광등", new[]{ "lamp" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF","SET_COLOR","SET_MODE" }, Empty),
        ("BUZZER", "부저", new[]{ "lamp","enclosure" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF","SET_MODE" }, Empty),
        ("AMPLIFIER", "앰프", new[]{ "speaker" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("MIC", "마이크", new[]{ "speaker" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("PTZ_UNIT", "PTZ 구동부", new[]{ "camera" }, new[]{ "IDLE","RUNNING" }, new[]{ "RESET","CALIBRATE" }, Empty),
        ("IR_LED", "적외선 LED", new[]{ "camera" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("WIPER", "와이퍼", new[]{ "camera" }, new[]{ "ON","OFF" }, new[]{ "ON","OFF" }, Empty),
        ("OPTICAL_LENS", "광학 렌즈", new[]{ "camera" }, Empty, Empty, Empty),
        ("TRACKER", "트래커", new[]{ "camera" }, new[]{ "ACTIVE","LOST","IDLE" }, new[]{ "ON","OFF" }, Empty),
        ("NETWORK_INTERFACE", "네트워크 인터페이스", null, Empty, Empty, Empty),
    };

    private static IEnumerable<string> MockupCodes => Catalog32.Select(c => c.Code);

    private static DeviceSpecCatalogDto BuildCatalog()
    {
        var entries = Catalog32.Select(row => new VocabularyEntryDto
        {
            Code = row.Code,
            Label = row.Label,
            AppliesTo = row.AppliesTo?.ToList(),
            Definition = new JObject
            {
                ["states"] = new JArray(row.States),
                ["commands"] = new JArray(row.Commands),
                ["produces"] = new JArray(row.Produces),
            },
        }).ToList();

        // 카탈로그에서 폐기된 유형 — 새로 고를 수는 없지만 이미 쓰는 장비의 표시는 되어야 한다.
        entries.Add(new VocabularyEntryDto
        {
            Code = "OLD_PART",
            Label = "구형 부품",
            DeprecatedAt = "2026-08-01T00:00:00+09:00",
            AppliesTo = new List<string> { "camera" },
        });

        return new DeviceSpecCatalogDto
        {
            Vocabularies = new Dictionary<string, List<VocabularyEntryDto>> { ["component_type"] = entries },
        };
    }

    private static (CatalogService service, MockDeviceApiService api) Create(EnumServerContract contract)
    {
        var api = new MockDeviceApiService
        {
            CatalogResponseFactory = () => ApiResponse<DeviceSpecCatalogDto>.CreateSuccess(BuildCatalog()),
        };
        var policy = new DeviceQueryPolicy(new FixedProbe(contract));
        return (new CatalogService(api, policy, log: null), api);
    }

    /// <summary>계약 판본을 고정하는 프로브(CatalogServiceTests 의 같은 이름 짝을 이 파일에도 둔다 — 그쪽은 private 이다).</summary>
    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }
    #endregion
}

using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 상세 폼의 축 값 편집(접속 · 하드웨어 · 부대 · 운용 설정) — 2026-09-27 완성도 수정 D-2
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 전에는 13칸 아래에 "이번 판에서는 읽기 전용 — 모델에서 서버로 보내는 경로가 아직 없다" 가 붙어 있었고 고칠 수 없었다.
/// 이제 그 칸들은 좁은 PATCH(보낸 키만 바뀐다)로 저장된다 — 본문 모양 · 부대 보존 · 패널 저장과의 순서 · 문구를 못 박는다.
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceAxisEditTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);

    #region - 본문 만들기(순수 함수) -
    private static DevicePropertySpec Spec(string key, EnumDeviceCategory category = EnumDeviceCategory.Lamp)
        => DevicePropertyCatalog.For(category, isAxisContract: true, isUnitEra: true).Single(s => s.Key == key);

    [Fact]
    public void should_build_nested_body_with_only_the_touched_keys_when_axis_fields_are_edited()
    {
        var body = DeviceAxisPatchBuilder.Build(new[]
        {
            (Spec("connection.channel"), "3"),
            (Spec("hardware_spec.model"), "M-2"),
            (Spec("hardware_spec.serial"), ""),
            (Spec("unit_id"), "7"),
        });

        var expected = JObject.Parse("{\"connection\":{\"channel\":3},\"hardware_spec\":{\"model\":\"M-2\",\"serial\":null},\"unit_id\":7}");
        Assert.True(JToken.DeepEquals(expected, body), body.ToString());
    }

    [Fact]
    public void should_write_threshold_as_number_and_clear_as_null_when_enclosure_threshold_is_edited()
    {
        var high = Spec("device_config.thresholds.temperature.high", EnumDeviceCategory.Enclosure);
        var low = Spec("device_config.thresholds.temperature.low", EnumDeviceCategory.Enclosure);

        var body = DeviceAxisPatchBuilder.Build(new[] { (high, "45.5"), (low, "") });

        Assert.Equal(45.5, (double)body.SelectToken("device_config.thresholds.temperature.high")!);
        Assert.Equal(JTokenType.Null, body.SelectToken("device_config.thresholds.temperature.low")!.Type);
    }

    [Theory]
    [InlineData("connection.type")]
    [InlineData("unit_id")]
    public void should_refuse_empty_value_when_server_field_is_not_nullable(string key)
    {
        Assert.False(DeviceAxisPatchBuilder.TryConvert(Spec(key), "  ", out _, out var error));
        Assert.Contains("비울 수 없습니다", error);
    }

    [Fact]
    public void should_send_camera_record_mode_as_boolean_when_edited()
    {
        var body = DeviceAxisPatchBuilder.Build(new[] { (Spec("device_config.modes.is_record", EnumDeviceCategory.Camera), "true") });

        Assert.Equal(JTokenType.Boolean, body.SelectToken("device_config.modes.is_record")!.Type);
    }
    #endregion

    #region - 명세 -
    [Theory]
    [MemberData(nameof(Categories))]
    public void should_make_connection_hardware_unit_fields_editable_when_axis_contract(EnumDeviceCategory category)
    {
        var specs = DevicePropertyCatalog.For(category, isAxisContract: true, isUnitEra: true);

        foreach (var key in new[] { "connection.type", "connection.channel", "hardware_spec.manufacturer", "hardware_spec.model", "hardware_spec.firmware", "unit_id" })
        {
            var spec = specs.Single(s => s.Key == key);
            Assert.Equal(DevicePropertyWritable.Yes, spec.Writable);
            Assert.NotNull(spec.AxisWritePath);
        }
    }

    [Fact]
    public void should_not_offer_parent_device_to_sensors_when_axis_contract()
    {
        // 센서의 상위는 소속 제어기 하나 — parent_device_id 를 보내면 서버가 거부한다(D13).
        Assert.DoesNotContain(DevicePropertyCatalog.For(EnumDeviceCategory.Sensor, true), s => s.Key == "connection.parent_device_id");
        Assert.Contains(DevicePropertyCatalog.For(EnumDeviceCategory.Gate, true), s => s.Key == "connection.parent_device_id");
    }

    [Fact]
    public void should_offer_thresholds_only_for_enclosures_in_both_contracts()
    {
        foreach (var category in Categories().Select(c => (EnumDeviceCategory)c[0]))
        {
            var axis = DevicePropertyCatalog.For(category, true).Where(s => s.Key.StartsWith("device_config.thresholds.")).ToList();
            var legacy = DevicePropertyCatalog.For(category, false).Where(s => s.Key.StartsWith("device_config.thresholds.")).ToList();
            if (category == EnumDeviceCategory.Enclosure)
            {
                Assert.Equal(7, axis.Count);                                  // UPS 배터리 하한은 7.0+ 에만
                Assert.Equal(6, legacy.Count);
                Assert.All(legacy, s => Assert.NotNull(s.ViewModelPath));     // 6.3 은 패널 저장(PUT)이 보낸다
            }
            else
            {
                Assert.Empty(axis);
                Assert.Empty(legacy);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void should_name_the_type_axis_field_per_category_when_axis_contract(EnumDeviceCategory category)
    {
        var spec = DevicePropertyCatalog.For(category, true).Single(s => s.Key == "type_axis");

        Assert.Equal("type_" + category.ToString().ToLowerInvariant(), spec.ApiPath);
        Assert.DoesNotContain("<", spec.ApiPath);
    }

    [Fact]
    public void should_hide_response_metadata_rows_when_listing_properties()
    {
        Assert.DoesNotContain(DevicePropertyCatalog.All, s => s.Key.StartsWith("meta."));
    }

    public static IEnumerable<object[]> Categories() => new[]
    {
        EnumDeviceCategory.Controller, EnumDeviceCategory.Sensor, EnumDeviceCategory.Camera, EnumDeviceCategory.Speaker,
        EnumDeviceCategory.Enclosure, EnumDeviceCategory.Lamp, EnumDeviceCategory.Gate,
    }.Select(c => new object[] { c });
    #endregion

    #region - 운영자 문구 -
    /// <summary>운영자 화면에 나오면 안 되는 개발 용어(브리프 문구 규칙).</summary>
    private static readonly string[] BannedWords =
    {
        "이번 판", "다음 판", "판본", "422", "OBSERVED", "PATCH", "PUT", "POST", "Draft", "서버 호출", "통째", "축 ",
        "관측값이다", "읽기 전용 —", "경로가 정본", "메타데이터",
    };

    [Fact]
    public void should_keep_developer_terms_out_of_every_visible_lock_reason_and_label()
    {
        var visible = DevicePropertyCatalog.All
            .SelectMany(s => new[] { s.Label, s.ShowLockReason ? s.LockReason : null })
            .Concat(Enum.GetValues<DevicePropertySection>().Select(DevicePropertyCatalog.SectionTitle))
            .Concat(new[] { PropertyFieldViewModel.MultiIdentityReason, PropertyFieldViewModel.ReadOnlyReason, PropertyFieldViewModel.CreateOnlyReason, PropertyFieldViewModel.AfterCreateReason })
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var hits = visible.SelectMany(t => BannedWords.Where(w => t!.Contains(w, StringComparison.Ordinal)).Select(w => $"'{w}' in '{t}'")).ToList();
        Assert.True(hits.Count == 0, string.Join("\n", hits));
    }

    [Fact]
    public void should_end_every_visible_lock_reason_in_polite_form()
    {
        var plain = DevicePropertyCatalog.All
            .Where(s => s.ShowLockReason && !string.IsNullOrWhiteSpace(s.LockReason))
            .Select(s => s.LockReason!)
            .Where(t => !(t.EndsWith("니다.") || t.EndsWith("세요.")))
            .ToList();
        Assert.True(plain.Count == 0, string.Join("\n", plain));
    }

    [Theory]
    [InlineData("Fence", "펜스")]
    [InlineData("SmartSensor2", "스마트2")]
    [InlineData("Outdoor", "옥외")]
    [InlineData("PIR", "PIR")]
    [InlineData("Unknown", "미지정")]
    public void should_show_korean_type_label_when_server_label_is_the_code(string code, string expected)
        => Assert.Equal(expected, DeviceEnumDisplay.TypeAxisKorean(code, code));

    [Fact]
    public void should_prefer_the_server_label_when_it_is_already_korean()
        => Assert.Equal("펜스형", DeviceEnumDisplay.TypeAxisKorean("Fence", "펜스형"));

    [Theory]
    [InlineData("OPEN", "열림")]
    [InlineData("CLOSED", "닫힘")]
    [InlineData("RUNNING", "구동 중")]
    [InlineData("", "미상")]
    [InlineData("WEIRD", "알 수 없음")]
    public void should_translate_door_state_when_displayed(string code, string expected)
        => Assert.Equal(expected, DeviceEnumDisplay.DoorStateKorean(code));

    [Fact]
    public void should_show_door_status_in_korean_when_enclosure_reports_closed()
    {
        var vm = new EnclosureDeviceViewModel(new EnclosureDeviceModel { DoorStatus = "CLOSED" });
        Assert.Equal("닫힘", vm.DoorStatusDisplay);
    }

    [Fact]
    public void should_show_connection_type_in_korean_and_hide_unknown_codes_when_field_is_read_only()
    {
        var spec = Spec("connection.type");
        var field = new PropertyFieldViewModel(spec, new DirtyFieldTracker());
        var options = spec.FixedOptions!.Select(o => new PropertyOption(o.Display, o.Code)).ToList();

        field.Load(new object[] { Lamp(11, connectionType: "CONTROLLER_CONTACT") }, options, false, isReadOnly: true, false);
        Assert.Equal("제어기 접점", field.DisplayText);

        field.Load(new object[] { Lamp(11, connectionType: "FUTURE_BUS") }, options, false, isReadOnly: true, false);
        Assert.Equal(DeviceEnumDisplay.UnknownValue, field.DisplayText);
        Assert.Contains("FUTURE_BUS", field.RawToolTip);
    }

    [Fact]
    public void should_show_status_without_the_code_when_enum_is_displayed()
        => Assert.Equal("운영", DeviceEnumDisplay.EnumBilingual(EnumDeviceStatus.ACTIVATED));

    [Fact]
    public void should_pick_subject_particle_by_final_consonant()
    {
        Assert.Equal("이", DeviceDashboardViewModel.SubjectParticle("경광등"));
        Assert.Equal("가", DeviceDashboardViewModel.SubjectParticle("센서"));
    }

    [Theory]
    [InlineData("그룹", "이")]
    [InlineData("센서 3", "이")]      // 삼
    [InlineData("arm2", "가")]        // 이
    [InlineData("GATE", "가")]        // 이
    [InlineData("L", "이")]           // 엘
    [InlineData("'카메라'", "가")]    // 따옴표 뒤의 낱말로 본다
    [InlineData("", "이(가)")]
    [InlineData("#", "이(가)")]
    public void should_read_digits_and_latin_letters_aloud_when_subject_particle_is_picked(string word, string expected)
        => Assert.Equal(expected, DeviceDashboardViewModel.SubjectParticle(word));
    #endregion

    #region - 폼 -
    [Fact]
    public void should_not_touch_the_row_but_return_axis_edits_when_only_axis_fields_change()
    {
        var row = Lamp(11, model: "M-1");
        var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter());
        form.Load(new object[] { row }, EnumDeviceCategory.Lamp, isAxisContract: true, isCreating: false, isReadOnly: false, isUnitEra: true);

        form.Fields.Single(f => f.Key == "hardware_spec.model").Text = "M-2";
        var commit = form.Commit();

        Assert.True(commit.IsWritten);
        Assert.False(commit.HasRowWrites);
        var edit = Assert.Single(commit.AxisEdits!);
        Assert.Equal(("hardware_spec.model", "M-2"), (edit.Spec.Key, edit.Text));
        Assert.Equal("M-1", row.Model.Axes!.HardwareSpec!.Model);   // 행(모델)은 그대로 — 서버가 받은 뒤 재조회로 바뀐다
    }

    [Fact]
    public void should_lock_axis_fields_when_creating_a_device()
    {
        var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter());
        form.Load(new object[] { new LampDeviceViewModel(new LampDeviceModel()) }, EnumDeviceCategory.Lamp, true, isCreating: true, isReadOnly: false, isUnitEra: true);

        var field = form.Fields.Single(f => f.Key == "hardware_spec.model");
        Assert.True(field.IsLocked);
        Assert.Equal(PropertyFieldViewModel.AfterCreateReason, field.LockReason);
    }

    [Fact]
    public void should_block_commit_when_a_required_axis_field_is_emptied()
    {
        var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter());
        form.Load(new object[] { Lamp(11, connectionType: "IP_DIRECT") }, EnumDeviceCategory.Lamp, true, false, false, true);

        form.Fields.Single(f => f.Key == "connection.type").Text = "";
        var commit = form.Commit();

        Assert.False(commit.IsWritten);
        Assert.Contains("비울 수 없습니다", commit.Message);
    }
    #endregion

    #region - 보내기(서비스) -
    [Fact]
    public async Task should_patch_each_device_once_and_keep_its_unit_when_unit_is_not_edited()
    {
        var api = new AxisCaptureApi();
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V8_0)));
        var devices = new IBaseDeviceModel[] { LampModel(11, unitId: 4), LampModel(12, unitId: 5) };

        var result = await writer.ApplyAsync(devices, new[] { (Spec("hardware_spec.firmware"), "2.0") });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { ("lamps", 11, 4), ("lamps", 12, 5) },
            api.Calls.Select(c => (c.Path, c.Id, (int)c.Body["unit_id"]!)).ToArray());
        Assert.All(api.Calls, c => Assert.Equal("2.0", (string?)c.Body.SelectToken("hardware_spec.firmware")));
        Assert.All(api.Calls, c => Assert.Null(c.Body.SelectToken("hardware_spec.components")));
    }

    [Fact]
    public async Task should_carry_the_devices_current_unit_instead_of_null_when_an_edit_clears_the_unit_on_8_0()
    {
        // 서버 회신 2026-09-28 Q-1 — PATCH 의 명시적 "unit_id": null 은 422(무소속 장비는 없다). 칸 명세가 비우기를 허용하도록
        // 바뀌는 회귀가 생겨도 null 을 보내지 않고, 부대를 고치지 않은 편집과 같게 그 장비의 지금 부대를 싣는다.
        var api = new AxisCaptureApi();
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V8_0)));
        var clearable = Spec("unit_id") with { AxisAllowsClear = true };

        var result = await writer.ApplyAsync(new IBaseDeviceModel[] { LampModel(11, unitId: 4) }, new[] { (clearable, "") });

        Assert.True(result.IsSuccess);
        Assert.Equal(4, (int?)api.Calls.Single().Body["unit_id"]);
    }

    [Fact]
    public async Task should_send_nothing_when_server_is_legacy()
    {
        var api = new AxisCaptureApi();
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V6_3)));

        var result = await writer.ApplyAsync(new IBaseDeviceModel[] { LampModel(11) }, new[] { (Spec("hardware_spec.model"), "X") });

        Assert.False(result.IsSuccess);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public async Task should_report_a_fixed_korean_line_without_server_text_when_server_refuses()
    {
        var api = new AxisCaptureApi { Refuse = true };
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V7_0)));

        var result = await writer.ApplyAsync(new IBaseDeviceModel[] { LampModel(11) }, new[] { (Spec("hardware_spec.model"), "X") });

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("VALIDATION", result.Message);
        Assert.EndsWith("[적용]하세요.", result.Message);
    }
    #endregion

    #region - 콘솔 -
    [Fact]
    public async Task should_patch_axis_fields_then_reload_and_confirm_when_only_axis_fields_are_applied()
    {
        var api = new AxisCaptureApi();
        var (console, source) = await OpenAsync(api, Lamp(11, model: "M-1", unitId: 3));
        console.OnRowsSelected(new List<object> { source.Items[0] });

        console.Form.Fields.Single(f => f.Key == "hardware_spec.model").Text = "M-2";
        console.Apply();

        var call = Assert.Single(api.Calls);
        Assert.Equal(("lamps", 11), (call.Path, call.Id));
        Assert.Equal("M-2", (string?)call.Body.SelectToken("hardware_spec.model"));
        Assert.Equal(3, (int)call.Body["unit_id"]!);
        Assert.Equal(0, source.SaveCalls);            // 행 칸이 없으니 패널 저장은 부르지 않는다
        Assert.Equal(1, source.ReloadCalls);          // 보낸 값을 다시 읽어 맞춰 본다

        // 서버가 새 값을 돌려준 재조회 — 행을 갈아 끼우는 대신 같은 행의 모델 값을 바꾼다(시험 스레드에서 목록 보기를 건드리지 않는다).
        source.Items[0].Model.Axes!.HardwareSpec!.Model = "M-2";
        source.Finish();

        Assert.Equal(ConsoleDetailStateMachine.AppliedMessage(1, 1), console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_warn_in_korean_when_the_reread_value_differs_from_what_was_sent()
    {
        var api = new AxisCaptureApi();
        var (console, source) = await OpenAsync(api, Lamp(11, model: "M-1"));
        console.OnRowsSelected(new List<object> { source.Items[0] });

        console.Form.Fields.Single(f => f.Key == "hardware_spec.model").Text = "M-2";
        console.Apply();
        source.Finish();                              // 재조회가 옛 값(M-1)을 돌려줬다

        Assert.Equal("'모델' 값이 저장되지 않았습니다. 다시 확인한 뒤 [적용]하세요.", console.Detail.LastMessage);
    }

    [Fact]
    public async Task should_save_row_fields_first_and_patch_axis_fields_after_when_both_are_applied()
    {
        var api = new AxisCaptureApi();
        var (console, source) = await OpenAsync(api, Lamp(11, model: "M-1"));
        console.OnRowsSelected(new List<object> { source.Items[0] });

        console.Form.Fields.Single(f => f.Key == "name_device").Text = "경광등 새이름";
        console.Form.Fields.Single(f => f.Key == "hardware_spec.model").Text = "M-2";
        console.Apply();

        Assert.Equal(1, source.SaveCalls);
        Assert.Empty(api.Calls);                      // 패널 저장이 끝나기 전에는 보내지 않는다(패널 저장이 받은 축을 되싣는다)

        source.Finish();                              // 패널 저장 끝남 — 이름이 행에 남아 있다

        var call = Assert.Single(api.Calls);
        Assert.Equal("M-2", (string?)call.Body.SelectToken("hardware_spec.model"));
        Assert.Null(call.Body["name_device"]);
    }

    [Fact]
    public async Task should_show_wiring_only_on_the_controller_rail_when_wiring_is_available()
    {
        var api = new AxisCaptureApi();
        var (console, _) = await OpenAsync(api, Lamp(11), wiring: true);

        Assert.False(console.IsWiringVisible);        // 경광등 레일 — 늘 꺼진 채 자리만 차지하던 버튼
        await console.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Controller));
        Assert.True(console.IsWiringVisible);
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3, true)]      // 6.3 — 카메라 설정 · 링크 창이 유일한 입구
    [InlineData(EnumServerContract.V8_0, false)]     // 7.0+ — 같은 값을 상세 칸에서 바로 고친다(그 창의 설정 탭은 서버에 없는 입구)
    public async Task should_offer_camera_detail_button_only_on_legacy_camera_rail(EnumServerContract contract, bool expected)
    {
        var api = new AxisCaptureApi();
        var (console, _) = await OpenAsync(api, Lamp(11), contract: contract);
        var raised = new List<string?>();
        console.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.False(console.IsCameraDetailVisible);    // 경광등 레일
        await console.SelectRailAsync(DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Camera));

        Assert.Equal(expected, console.IsCameraDetailVisible);
        Assert.Contains(nameof(DeviceDashboardViewModel.IsCameraDetailVisible), raised);
    }

    [Fact]
    public async Task should_explain_an_empty_list_when_search_hides_every_row()
    {
        var api = new AxisCaptureApi();
        var (console, _) = await OpenAsync(api, Lamp(11));

        console.SearchText = "없는 이름";

        Assert.True(console.IsListEmpty);
        Assert.Equal("검색 결과가 없습니다", console.EmptyTitle);
    }

    private static async Task<(DeviceDashboardViewModel Console, AxisSource Source)> OpenAsync(AxisCaptureApi api, LampDeviceViewModel row, bool wiring = false,
        EnumServerContract contract = EnumServerContract.V8_0)
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var providerService = new MockDeviceProviderService();
        var policy = new DeviceQueryPolicy(new Probe(contract));

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var lamps = new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            lamps,
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new NoCatalog(), policy,
            wiringLauncher: wiring ? new Lazy<Consoles.Wiring.IWiringLauncher>(() => new AvailableWiring()) : null);

        var source = new AxisSource(lamps, row);
        console.UseSource(LampRail, source);
        await ((IActivate)console).ActivateAsync();
        await console.SelectRailAsync(LampRail);
        return (console, source);
    }
    #endregion

    #region - Fakes -
    private static LampDeviceModel LampModel(int id, string? model = null, string? connectionType = null, int? unitId = null) => new()
    {
        Id = id, DeviceNumber = id, DeviceName = $"경광등 {id}", Status = EnumDeviceStatus.ACTIVATED, UnitId = unitId,
        Axes = new DeviceAxesModel
        {
            Connection = new ConnectionAxisModel { Type = connectionType ?? "IP_DIRECT" },
            HardwareSpec = new HardwareSpecModel { Model = model },
            Meta = new ResponseMeta("full", new[] { "connection", "hardware_spec", "components", "device_status", "device_config" }),
        },
    };

    private static LampDeviceViewModel Lamp(int id, string? model = null, string? connectionType = null, int? unitId = null)
        => new(LampModel(id, model, connectionType, unitId));

    /// <summary>좁은 PATCH 를 잡는 API 대역 — 인터페이스 기본 구현을 다시 구현해 이 멤버만 가로챈다.</summary>
    private sealed class AxisCaptureApi : MockDeviceApiService, IDeviceApiService
    {
        public List<(string Path, int Id, JObject Body)> Calls { get; } = new();
        public bool Refuse { get; set; }

        Task<ApiResponse<object>> IDeviceApiService.PatchDeviceAxesAsync(string deviceTypePath, int deviceId, JObject body, CancellationToken token)
        {
            Calls.Add((deviceTypePath, deviceId, (JObject)body.DeepClone()));
            return Task.FromResult(Refuse
                ? ApiResponse<object>.CreateError("VALIDATION_ERROR", "server said no")
                : ApiResponse<object>.CreateSuccess(new object()));
        }
    }

    private sealed class AxisSource : IDeviceConsoleSource
    {
        public AxisSource(BasePanelViewModel panel, params LampDeviceViewModel[] rows)
        {
            Panel = panel;
            foreach (var row in rows) Items.Add(row);
        }

        public ObservableCollection<LampDeviceViewModel> Items { get; } = new();
        public int SaveCalls { get; private set; }
        public int ReloadCalls { get; private set; }

        public BasePanelViewModel Panel { get; }
        public IEnumerable Rows => Items;
        public INotifyCollectionChanged RowsChanged => Items;
        public int RowCount => Items.Count;
        public bool IsBusy => false;
        public event EventHandler? BusyEnded;

        public void Select(IReadOnlyList<object> rows) { }
        public object? CreateDraft() => null;
        public void AdoptDraft(object draft) { }
        public void ReleaseDraft(object draft) { }
        public bool Save() { SaveCalls++; return true; }
        public void Delete() { }
        public bool Reload() { ReloadCalls++; return true; }

        public void ReplaceWith(params LampDeviceViewModel[] rows)
        {
            Items.Clear();
            foreach (var row in rows) Items.Add(row);
        }

        public void Finish() => BusyEnded?.Invoke(this, EventArgs.Empty);
    }

    private sealed class AvailableWiring : Consoles.Wiring.IWiringLauncher
    {
        public bool IsAvailable => true;
        public Task<bool> OpenAsync(IBaseDeviceModel controller) => Task.FromResult(false);
    }

    private sealed class Probe : IServerContractProbe
    {
        public Probe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class NoCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => false;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(false);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(false);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => true;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
    #endregion
}

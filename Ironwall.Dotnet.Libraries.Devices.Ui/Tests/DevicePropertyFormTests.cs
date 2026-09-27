using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 명세에서 만들어지는 상세 폼(device-console-redesign FR-08 · FR-10 ~ FR-13).
/// 핵심 계약: 칸 입력은 행을 건드리지 않는다 · 손댄 칸만 쓴다 · 검증에 걸리면 아무 행에도 쓰지 않는다.
/// </summary>
[Collection("CaliburnIoC")]
public class DevicePropertyFormTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static ControllerDeviceViewModel Controller(int id, int number, string name, string ip = "10.0.0.1", int port = 80)
        => new(new ControllerDeviceModel { Id = id, DeviceNumber = number, DeviceName = name, IpAddress = ip, Port = port });

    private static DevicePropertyFormViewModel NewForm(out ConsoleDetailPresenter presenter)
    {
        presenter = new ConsoleDetailPresenter();
        return new DevicePropertyFormViewModel(presenter);
    }

    private static PropertyFieldViewModel Field(DevicePropertyFormViewModel form, string key) => form.Fields.Single(f => f.Key == key);

    [Fact]
    public void should_build_sections_in_catalog_order_when_loaded()
    {
        var form = NewForm(out _);

        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, isAxisContract: true, isCreating: false, isReadOnly: false);

        var order = form.Sections.Select(s => s.Section).ToList();
        Assert.Equal(order.OrderBy(s => DevicePropertyCatalog.SectionOrder.ToList().IndexOf(s)).ToList(), order);
        Assert.Equal(DevicePropertySection.Common, order[0]);
        Assert.Equal("category_device", form.Sections[0].Fields[0].Key);
    }

    [Fact]
    public void should_not_touch_row_when_field_is_edited_before_commit()
    {
        var row = Controller(1, 1, "A");
        var form = NewForm(out var presenter);
        form.Load(new object[] { row }, EnumDeviceCategory.Controller, true, false, false);
        presenter.SelectedCount = 1;   // 선택 수는 콘솔이 알려 준다 — 폼은 손댄 칸만 센다

        Field(form, "name_device").Text = "B";

        Assert.Equal("A", row.DeviceName);
        Assert.Equal(1, presenter.Tracker.Count);
        Assert.Equal(ConsoleDetailState.Dirty, presenter.State);
    }

    [Fact]
    public void should_write_only_touched_fields_when_committed()
    {
        var row = Controller(1, 1, "A", ip: "10.0.0.1");
        var form = NewForm(out _);
        form.Load(new object[] { row }, EnumDeviceCategory.Controller, true, false, false);

        Field(form, "name_device").Text = "B";
        var commit = form.Commit();

        Assert.True(commit.IsWritten);
        Assert.Equal(1, commit.FieldCount);
        Assert.Equal("B", row.DeviceName);
        Assert.Equal("10.0.0.1", row.IpAddress);
    }

    [Fact]
    public void should_treat_field_as_untouched_when_restored_to_original()
    {
        var form = NewForm(out var presenter);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        var name = Field(form, "name_device");
        name.Text = "B";
        name.Text = "A";

        Assert.False(name.IsTouched);
        Assert.Equal(0, presenter.Tracker.Count);
    }

    [Fact]
    public void should_show_mixed_and_lock_identity_when_multiple_rows_differ()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A"), Controller(2, 2, "B") }, EnumDeviceCategory.Controller, true, false, false);

        var name = Field(form, "name_device");
        var number = Field(form, "number_device");

        Assert.True(name.IsMixed);
        Assert.Equal(ConsoleDetailStateMachine.MixedValuesText, name.DisplayText);
        Assert.True(number.IsLocked);
        Assert.Equal(PropertyFieldViewModel.MultiIdentityReason, number.LockReason);
    }

    [Fact]
    public void should_apply_touched_field_to_every_row_when_multiple_selected()
    {
        var a = Controller(1, 1, "A", port: 80);
        var b = Controller(2, 2, "B", port: 81);
        var form = NewForm(out _);
        form.Load(new object[] { a, b }, EnumDeviceCategory.Controller, true, false, false);

        Field(form, "connection.ip_port").Text = "8080";
        var commit = form.Commit();

        Assert.True(commit.IsWritten);
        Assert.Equal(2, commit.RowCount);
        Assert.Equal(8080, a.Port);
        Assert.Equal(8080, b.Port);
        Assert.Equal("A", a.DeviceName);
        Assert.Equal("B", b.DeviceName);
    }

    [Fact]
    public void should_write_nothing_when_any_touched_field_is_invalid()
    {
        var row = Controller(1, 1, "A", port: 80);
        var form = NewForm(out _);
        form.Load(new object[] { row }, EnumDeviceCategory.Controller, true, false, false);

        Field(form, "name_device").Text = "B";
        Field(form, "connection.ip_port").Text = "70000";
        var commit = form.Commit();

        Assert.False(commit.IsWritten);
        Assert.NotNull(commit.Message);
        Assert.Equal("A", row.DeviceName);          // 앞 칸이 멀쩡해도 쓰지 않는다 — 절반만 고친 행이 저장 경로로 새면 안 된다
        Assert.Equal(80, row.Port);
        Assert.True(Field(form, "connection.ip_port").HasError);
    }

    [Fact]
    public void should_roll_back_earlier_fields_when_a_later_field_is_rejected_at_write_time()
    {
        // 빈 IP 는 글자 검증을 지난다(받을 수 있는지는 속성의 형이 정한다) — 쓰는 순간에 거절된다.
        var a = Controller(1, 1, "A", ip: "10.0.0.1");
        var b = Controller(2, 2, "A", ip: "10.0.0.1");
        var form = NewForm(out _);
        form.Load(new object[] { a, b }, EnumDeviceCategory.Controller, true, false, false);

        Field(form, "name_device").Text = "B";
        Field(form, "connection.ip_address").Text = "";
        var commit = form.Commit();

        Assert.False(commit.IsWritten);
        Assert.Equal("A", a.DeviceName);       // 앞서 쓴 이름이 남으면 반쯤 고친 행이 다음 저장에 실려 나간다
        Assert.Equal("A", b.DeviceName);
        Assert.Equal("10.0.0.1", a.IpAddress);
    }

    [Fact]
    public void should_restore_texts_without_touching_row_when_reverted()
    {
        var row = Controller(1, 1, "A");
        var form = NewForm(out var presenter);
        form.Load(new object[] { row }, EnumDeviceCategory.Controller, true, false, false);

        var name = Field(form, "name_device");
        name.Text = "B";
        bool? seenByView = null;   // 화면은 알림을 받은 순간의 값을 읽는다 — 그 순간에 아직 '손댄 칸'이면 표지가 남는다(미리보기에서 발견)
        name.PropertyChanged += (_, e) => { if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(PropertyFieldViewModel.IsTouched)) seenByView = name.IsTouched; };
        form.Revert();

        Assert.Equal("A", name.Text);
        Assert.False(seenByView);
        Assert.Equal("A", row.DeviceName);
        Assert.Equal(0, presenter.Tracker.Count);
    }

    [Fact]
    public void should_lock_every_field_with_reason_when_read_only()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, isReadOnly: true);

        Assert.All(form.Fields, f => Assert.True(f.IsLocked));
        // 표시 전용 요약(장비 링크 · 부품별 설정)은 자물쇠만 보이고 까닭 문장은 띄우지 않는다 — 나머지는 까닭이 있다.
        Assert.All(form.Fields.Where(f => f.Spec.ShowLockReason), f => Assert.False(string.IsNullOrWhiteSpace(f.LockReason)));
    }

    [Fact]
    public void should_keep_lock_reason_from_spec_when_field_is_not_writable()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        var category = Field(form, "category_device");

        Assert.True(category.IsLocked);
        Assert.Equal(category.Spec.LockReason, category.LockReason);
        Assert.Equal(DevicePropertyEditor.ReadOnly, category.EffectiveEditor);
    }

    [Fact]
    public void should_hide_axis_only_fields_when_contract_is_legacy()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, isAxisContract: false, false, false);

        Assert.DoesNotContain(form.Fields, f => f.Spec.AxisContractOnly);
        Assert.DoesNotContain(form.Fields, f => f.IsNotReceived);   // 6.3 에는 축 자체가 없다 — "미수신"이 아니다
    }

    [Fact]
    public void should_reject_required_empty_field_when_creating()
    {
        var draft = Controller(0, 5, "새 제어기 5");
        var form = NewForm(out _);
        form.Load(new object[] { draft }, EnumDeviceCategory.Controller, true, isCreating: true, false);

        Field(form, "name_device").Text = "   ";
        var commit = form.Commit();

        Assert.False(commit.IsWritten);
        Assert.Equal("새 제어기 5", draft.DeviceName);
    }

    [Fact]
    public void should_allow_commit_without_touched_fields_when_creating_with_valid_defaults()
    {
        // 경광등은 포트가 등록 필수다(WP-2 SC-DEV-014) — 포트까지 채운 초안이면 손대지 않아도 기본값만으로 등록된다.
        var draft = new LampDeviceViewModel(new LampDeviceModel { DeviceNumber = 3, DeviceName = "새 경고등 3", IpPort = 8080 });
        var form = NewForm(out _);
        form.Load(new object[] { draft }, EnumDeviceCategory.Lamp, true, isCreating: true, false);

        var commit = form.Commit();

        Assert.True(commit.IsWritten);   // 종류가 필수가 아닌 카테고리 — 기본값만으로 등록할 수 있다
        Assert.Equal(0, commit.FieldCount);
    }

    [Fact]
    public void should_refuse_commit_when_nothing_touched_and_not_creating()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        Assert.False(form.Commit().IsWritten);
    }

    [Fact]
    public void should_renotify_bool_value_when_indeterminate_state_is_pushed_back()
    {
        // 여러 값이면 상자가 세 상태다 — 사용자가 "가운데"로 돌리면 null 이 온다. 받지 않되, 알려서 상자를 제자리로 돌린다.
        var on = Controller(1, 1, "A"); on.IsEnable = true;
        var off = Controller(2, 2, "B"); off.IsEnable = false;
        var form = NewForm(out var presenter);
        form.Load(new object[] { on, off }, EnumDeviceCategory.Controller, true, false, false);
        var field = Field(form, "is_enable");
        field.BoolValue = true;
        var notified = 0;
        field.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PropertyFieldViewModel.BoolValue)) notified++; };

        field.BoolValue = null;

        Assert.Equal(1, notified);
        Assert.True(field.BoolValue);
        Assert.Equal(1, presenter.Tracker.Count);
    }

    [Fact]
    public void should_offer_enum_names_when_option_source_is_clr_enum()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        var status = Field(form, "status");

        // Text 는 wire value(원문 enum 이름) — 저장 때 Enum.TryParse 가 읽는 값이라 바뀌면 안 된다.
        Assert.Equal(Enum.GetNames(typeof(EnumDeviceStatus)), status.Options.Select(o => o.Text).ToArray());
        Assert.NotNull(status.SelectedOption);
    }

    /// <summary>
    /// "운영 상태" 콤보의 Display 는 한글이어야 한다(구 버그: raw enum 이름 ACTIVATED/DEACTIVATED 그대로 노출).
    /// Text(wire value)는 그대로 원문이라 저장 로직은 전혀 바뀌지 않는다.
    /// </summary>
    [Fact]
    public void should_show_korean_display_when_status_combo_options_are_built()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        var status = Field(form, "status");

        var activated = status.Options.Single(o => o.Text == nameof(EnumDeviceStatus.ACTIVATED));
        var error = status.Options.Single(o => o.Text == nameof(EnumDeviceStatus.ERROR));
        var deactivated = status.Options.Single(o => o.Text == nameof(EnumDeviceStatus.DEACTIVATED));

        Assert.Equal("운영", activated.Display);
        Assert.Equal("오류", error.Display);
        Assert.Equal("중지", deactivated.Display);
    }

    /// <summary>"종류(레거시)" 콤보(EnumDeviceType)도 같은 규칙으로 한글을 보인다 — 그리드·상세 폼이 갈리지 않는다.</summary>
    [Fact]
    public void should_show_korean_display_for_legacy_device_type_options()
    {
        var form = NewForm(out _);
        var camera = new CameraDeviceViewModel(new CameraDeviceModel { Id = 1, DeviceNumber = 1, DeviceName = "C1" });
        form.Load(new object[] { camera }, EnumDeviceCategory.Camera, isAxisContract: false, isCreating: false, isReadOnly: false);

        var type = Field(form, "device_type");
        var ipCamera = type.Options.Single(o => o.Text == nameof(EnumDeviceType.IpCamera));

        Assert.Equal("카메라", ipCamera.Display);
    }

    /// <summary>읽기 전용 "카테고리" 칸도 raw enum 이름("Controller") 대신 한글을 보인다.</summary>
    [Fact]
    public void should_show_korean_category_text_when_category_field_is_read_only()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        var category = Field(form, "category_device");

        Assert.Equal("제어기", category.DisplayText);
    }

    [Fact]
    public void should_write_object_value_when_controller_option_is_chosen()
    {
        var controller = new ControllerDeviceModel { Id = 7, DeviceNumber = 7, DeviceName = "C7" };
        var sensor = new SensorDeviceViewModel(new SensorDeviceModel { Id = 1, DeviceNumber = 1, DeviceName = "S1" });
        var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter(), new FixedOptions(new PropertyOption("C7 (#7)", null, controller)));
        form.Load(new object[] { sensor }, EnumDeviceCategory.Sensor, true, false, false);

        var field = Field(form, "controller_id");
        field.SelectedOption = field.Options.Single();
        var commit = form.Commit();

        Assert.True(commit.IsWritten);
        Assert.Same(controller, sensor.Controller);
    }

    [Fact]
    public void should_edit_group_with_same_form_when_group_specs_are_given()
    {
        var group = new DeviceGroupViewModel(new DeviceGroupModel { Id = 3, Name = "동측" });
        var form = NewForm(out _);
        form.Load(new object[] { group }, DeviceGroupPropertySpecs.All, default, false, false);

        Field(form, "group.name").Text = "동측 울타리";
        var commit = form.Commit();

        Assert.True(commit.IsWritten);
        Assert.Equal("동측 울타리", group.Name);
        Assert.True(Field(form, "group.device_count").IsLocked);
    }

    [Fact]
    public void should_clear_sections_and_tracker_when_cleared()
    {
        var form = NewForm(out var presenter);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);
        Field(form, "name_device").Text = "B";

        form.Clear();

        Assert.Empty(form.Sections);
        Assert.Equal(0, presenter.Tracker.Count);
    }

    private sealed class FixedOptions : IDevicePropertyOptions
    {
        private readonly PropertyOption[] _options;
        public FixedOptions(params PropertyOption[] options) => _options = options;

        public IReadOnlyList<PropertyOption> OptionsFor(DevicePropertySpec spec, EnumDeviceCategory category)
            => spec.OptionSource == DevicePropertyOptionSource.Controllers ? _options : Array.Empty<PropertyOption>();
    }
}

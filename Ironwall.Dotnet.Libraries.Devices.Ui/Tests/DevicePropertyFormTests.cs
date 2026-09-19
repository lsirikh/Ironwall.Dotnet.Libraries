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
    public void should_restore_texts_without_touching_row_when_reverted()
    {
        var row = Controller(1, 1, "A");
        var form = NewForm(out var presenter);
        form.Load(new object[] { row }, EnumDeviceCategory.Controller, true, false, false);

        Field(form, "name_device").Text = "B";
        form.Revert();

        Assert.Equal("A", Field(form, "name_device").Text);
        Assert.Equal("A", row.DeviceName);
        Assert.Equal(0, presenter.Tracker.Count);
    }

    [Fact]
    public void should_lock_every_field_with_reason_when_read_only()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, isReadOnly: true);

        Assert.All(form.Fields, f => Assert.True(f.IsLocked));
        Assert.All(form.Fields, f => Assert.False(string.IsNullOrWhiteSpace(f.LockReason)));
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
        var draft = new LampDeviceViewModel(new LampDeviceModel { DeviceNumber = 3, DeviceName = "새 경고등 3" });
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
    public void should_offer_enum_names_when_option_source_is_clr_enum()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "A") }, EnumDeviceCategory.Controller, true, false, false);

        var status = Field(form, "status");

        Assert.Equal(Enum.GetNames(typeof(EnumDeviceStatus)), status.Options.Select(o => o.Text).ToArray());
        Assert.NotNull(status.SelectedOption);
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

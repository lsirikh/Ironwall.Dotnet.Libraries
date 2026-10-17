using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : DevicePropertyCatalog · DevicePropertyAccessor 회귀 가드 (FR-07~FR-09, FR-13)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 명세와 실제 행 뷰모델의 <b>어긋남을 시끄럽게</b> 잡는다 — 명세가 "이 속성 있다"고 선언했는데
/// 뷰모델에 없으면(리팩터로 속성이 빠지는 등) 화면은 조용히 예외를 던지거나 빈 칸을 보인다.
/// 이 테스트는 그 드리프트를 빌드 단계에서 잡는다.
/// </summary>
/// <remarks>
/// 행 뷰모델(<c>ControllerDeviceViewModel</c> 등)은 <c>BaseDeviceViewModel</c> → … → <c>BasePanelViewModel()</c>
/// 로 이어져 매개변수 없는 생성자가 <c>Caliburn.Micro.IoC.Get&lt;IEventAggregator&gt;()</c> 를 부른다 —
/// <see cref="TestIoCScope"/> 로 정적 IoC 델리게이트를 테스트 동안만 채워야 한다(<c>TypeAxisSupportTests</c> 정본).
/// </remarks>
[Collection("CaliburnIoC")]
public class DevicePropertyCatalogTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();


    private static readonly IReadOnlyDictionary<EnumDeviceCategory, Type> RowVmTypes = new Dictionary<EnumDeviceCategory, Type>
    {
        [EnumDeviceCategory.Controller] = typeof(ControllerDeviceViewModel),
        [EnumDeviceCategory.Sensor] = typeof(SensorDeviceViewModel),
        [EnumDeviceCategory.Camera] = typeof(CameraDeviceViewModel),
        [EnumDeviceCategory.Speaker] = typeof(SpeakerDeviceViewModel),
        [EnumDeviceCategory.Enclosure] = typeof(EnclosureDeviceViewModel),
        [EnumDeviceCategory.Lamp] = typeof(LampDeviceViewModel),
        [EnumDeviceCategory.Gate] = typeof(GateDeviceViewModel),
    };

    private static readonly EnumDeviceCategory[] AllCategories = RowVmTypes.Keys.ToArray();

    #region - Rule 1: 리플렉션 가드 -
    [Fact]
    public void should_match_row_view_model_properties_when_view_model_path_declared()
    {
        var failures = new List<string>();

        foreach (var spec in DevicePropertyCatalog.All)
        {
            if (spec.ViewModelPath == null) continue;

            foreach (var category in spec.Categories)
            {
                if (!RowVmTypes.TryGetValue(category, out var vmType)) continue;

                var property = vmType.GetProperty(spec.ViewModelPath);
                if (property == null)
                {
                    failures.Add($"{spec.Key}/{category}: {vmType.Name} 에 '{spec.ViewModelPath}' 공개 속성이 없습니다.");
                    continue;
                }

                if (spec.Writable == DevicePropertyWritable.Yes && property.SetMethod is not { IsPublic: true })
                    failures.Add($"{spec.Key}/{category}: {vmType.Name}.{spec.ViewModelPath} 에 public setter 가 없습니다.");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    #endregion

    #region - Rule 2: LockReason · ViewModelPath 필수 짝 -
    [Fact]
    public void should_have_lock_reason_when_not_fully_writable()
    {
        var missing = DevicePropertyCatalog.All
            .Where(s => s.Writable != DevicePropertyWritable.Yes && string.IsNullOrWhiteSpace(s.LockReason))
            .Select(s => s.Key)
            .ToList();

        Assert.True(missing.Count == 0, $"LockReason 누락: {string.Join(", ", missing)}");
    }

    [Fact]
    public void should_have_view_model_path_when_fully_writable()
    {
        var missing = DevicePropertyCatalog.All
            // 축 값 칸은 행 뷰모델이 아니라 축 값 부분 수정(AxisWritePath)으로 쓴다 — 둘 중 하나는 있어야 한다.
            .Where(s => s.Writable == DevicePropertyWritable.Yes && string.IsNullOrWhiteSpace(s.ViewModelPath) && string.IsNullOrWhiteSpace(s.AxisWritePath))
            .Select(s => s.Key)
            .ToList();

        Assert.True(missing.Count == 0, $"ViewModelPath 누락: {string.Join(", ", missing)}");
    }
    #endregion

    #region - Rule 3: 키 유일성 -
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_have_unique_keys_per_category_when_filtered_by_contract(bool isAxisContract)
    {
        foreach (var category in AllCategories)
        {
            var keys = DevicePropertyCatalog.For(category, isAxisContract).Select(s => s.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }
    }
    #endregion

    #region - Rule 4: 절 순서 · 계약 분리 · category_device 선두 -
    [Theory]
    [MemberData(nameof(CategoryAndContractCases))]
    public void should_order_sections_and_hide_other_contract_when_queried(EnumDeviceCategory category, bool isAxisContract)
    {
        var specs = DevicePropertyCatalog.For(category, isAxisContract);
        var order = DevicePropertyCatalog.SectionOrder.ToList();
        var indices = specs.Select(s => order.IndexOf(s.Section)).ToList();

        Assert.Equal(indices, indices.OrderBy(i => i).ToList());
        Assert.DoesNotContain(specs, s => isAxisContract ? s.LegacyContractOnly : s.AxisContractOnly);
        Assert.Equal("category_device", specs[0].Key);
    }

    public static IEnumerable<object[]> CategoryAndContractCases()
    {
        foreach (var category in AllCategories)
        {
            yield return new object[] { category, true };
            yield return new object[] { category, false };
        }
    }
    #endregion

    #region - Rule 4b: 소속 부대(unit_id) — v8.0 경계(D-14) -
    [Theory]
    [MemberData(nameof(CategoryAndContractCases))]
    public void should_hide_unit_id_field_when_not_unit_era(EnumDeviceCategory category, bool isAxisContract)
    {
        var specs = DevicePropertyCatalog.For(category, isAxisContract, isUnitEra: false);

        Assert.DoesNotContain(specs, s => s.Key == "unit_id");
    }

    [Theory]
    [MemberData(nameof(CategoryAndContractCases))]
    public void should_show_unit_id_field_in_common_section_when_unit_era(EnumDeviceCategory category, bool isAxisContract)
    {
        var specs = DevicePropertyCatalog.For(category, isAxisContract, isUnitEra: true);

        var unit = Assert.Single(specs, s => s.Key == "unit_id");
        Assert.Equal(DevicePropertySection.Common, unit.Section);
        // 2026-09-27: 소속 부대는 고를 수 있다(부대 목록 콤보 → 축 값 부분 수정). 전에는 "다부대 편집은 다음 판" 으로 잠겨 있었다.
        Assert.Equal(DevicePropertyEditor.Choice, unit.Editor);
        Assert.Equal(DevicePropertyWritable.Yes, unit.Writable);
        Assert.Equal("unit_id", unit.AxisWritePath);
    }

    [Fact]
    public void should_read_unassigned_text_when_unit_id_is_null()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel { UnitId = null });
        var spec = Spec("unit_id", EnumDeviceCategory.Controller);

        // 저장 값은 비어 있고, 화면 글은 명세의 빈 값 표시("미배치")가 맡는다.
        Assert.Equal(string.Empty, DevicePropertyAccessor.ReadText(vm, spec));
        Assert.Equal(Ironwall.Dotnet.Libraries.Devices.Ui.Services.UnitNameDirectory.Unassigned, spec.EmptyDisplay);
    }

    [Fact]
    public void should_read_raw_id_text_when_unit_directory_is_unavailable()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel { UnitId = 12 });
        var spec = Spec("unit_id", EnumDeviceCategory.Controller);

        Assert.Equal("12", DevicePropertyAccessor.ReadText(vm, spec));
    }
    #endregion

    #region - Rule 5: 필수 절 커버리지(축 계약) -
    [Theory]
    [MemberData(nameof(CategoriesOnly))]
    public void should_cover_required_sections_when_axis_contract(EnumDeviceCategory category)
    {
        var specs = DevicePropertyCatalog.For(category, isAxisContract: true);

        Assert.Contains(specs, s => s.Section == DevicePropertySection.Common);
        Assert.Contains(specs, s => s.Section == DevicePropertySection.Location);
        Assert.Contains(specs, s => s.Section == DevicePropertySection.Connection);
        Assert.Contains(specs, s => s.Section == DevicePropertySection.HardwareSpec);
        Assert.Contains(specs, s => s.Section == DevicePropertySection.Components);
        Assert.Contains(specs, s => s.Section == DevicePropertySection.DeviceStatus);
        Assert.Contains(specs, s => s.Section == DevicePropertySection.DeviceConfig);
    }

    public static IEnumerable<object[]> CategoriesOnly() => AllCategories.Select(c => new object[] { c });
    #endregion

    #region - Rule 6: 접근기 왕복 -
    [Fact]
    public void should_round_trip_name_when_written_through_accessor()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("name_device", EnumDeviceCategory.Controller);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "정문 제어기", out var error));
        Assert.Null(error);
        Assert.Equal("정문 제어기", vm.DeviceName);
        Assert.Equal("정문 제어기", DevicePropertyAccessor.ReadText(vm, spec));
    }

    [Fact]
    public void should_reject_port_when_out_of_range_or_not_numeric()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("connection.ip_port", EnumDeviceCategory.Controller);

        Assert.False(DevicePropertyAccessor.TryWrite(vm, spec, "0", out var e1));
        Assert.NotNull(e1);
        Assert.False(DevicePropertyAccessor.TryWrite(vm, spec, "70000", out var e2));
        Assert.NotNull(e2);
        Assert.False(DevicePropertyAccessor.TryWrite(vm, spec, "abc", out var e3));
        Assert.NotNull(e3);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "8080", out var error));
        Assert.Null(error);
        Assert.Equal(8080, vm.Port);
    }

    [Fact]
    public void should_reject_latitude_when_out_of_range()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("latitude", EnumDeviceCategory.Controller);

        Assert.False(DevicePropertyAccessor.TryWrite(vm, spec, "91", out var error));
        Assert.NotNull(error);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "45.5", out var noError));
        Assert.Null(noError);
        Assert.Equal(45.5, vm.Latitude);
    }

    [Fact]
    public void should_round_trip_boolean_when_written_through_accessor()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("is_enable", EnumDeviceCategory.Controller);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "true", out var error));
        Assert.Null(error);
        Assert.True(vm.IsEnable);
        Assert.Equal("true", DevicePropertyAccessor.ReadText(vm, spec));
    }

    [Fact]
    public void should_clear_nullable_double_when_written_empty()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("altitude", EnumDeviceCategory.Controller);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "12.5", out _));
        Assert.Equal(12.5, vm.Altitude);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "", out var error));
        Assert.Null(error);
        Assert.Null(vm.Altitude);
    }

    [Fact]
    public void should_reject_empty_text_when_property_is_non_nullable_string()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("connection.ip_address", EnumDeviceCategory.Controller);

        Assert.True(DevicePropertyAccessor.TryWrite(vm, spec, "10.0.0.5", out _));
        Assert.Equal("10.0.0.5", vm.IpAddress);

        Assert.False(DevicePropertyAccessor.TryWrite(vm, spec, "", out var error));
        Assert.NotNull(error);
        Assert.Equal("10.0.0.5", vm.IpAddress);   // 거부됐으니 그대로 남는다
    }

    [Fact]
    public void should_reject_write_when_spec_is_locked()
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        var spec = Spec("category_device", EnumDeviceCategory.Controller);
        var before = vm.CategoryDevice;

        Assert.False(DevicePropertyAccessor.TryWrite(vm, spec, "Camera", out var error));
        Assert.NotNull(error);
        Assert.Equal(before, vm.CategoryDevice);
    }

    [Fact]
    public void should_set_controller_reference_when_written_through_try_write_object()
    {
        var vm = new SensorDeviceViewModel(new SensorDeviceModel());
        var spec = Spec("controller_id", EnumDeviceCategory.Sensor);
        var controller = new ControllerDeviceModel { DeviceName = "제어기-1" };

        Assert.True(DevicePropertyAccessor.TryWriteObject(vm, spec, controller, out var error));
        Assert.Null(error);
        Assert.Same(controller, vm.Controller);
    }

    [Fact]
    public void should_reject_try_write_object_when_value_type_mismatches()
    {
        var vm = new SensorDeviceViewModel(new SensorDeviceModel());
        var spec = Spec("controller_id", EnumDeviceCategory.Sensor);

        Assert.False(DevicePropertyAccessor.TryWriteObject(vm, spec, "not-a-controller", out var error));
        Assert.NotNull(error);
    }

    private static DevicePropertySpec Spec(string key, EnumDeviceCategory category)
        => DevicePropertyCatalog.All.First(s => s.Key == key && s.Categories.Contains(category));
    #endregion

    #region - Rule 7: Validate -
    [Fact]
    public void should_report_required_message_only_when_creating()
    {
        var spec = Spec("name_device", EnumDeviceCategory.Controller);

        Assert.NotNull(DevicePropertyAccessor.Validate(spec, "", isCreating: true));
        Assert.Null(DevicePropertyAccessor.Validate(spec, "", isCreating: false));
    }

    [Fact]
    public void should_reject_whitespace_only_text()
    {
        var spec = Spec("name_device", EnumDeviceCategory.Controller);

        Assert.NotNull(DevicePropertyAccessor.Validate(spec, "   ", isCreating: false));
    }
    #endregion
}

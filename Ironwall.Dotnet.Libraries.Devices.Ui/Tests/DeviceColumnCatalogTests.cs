using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="DeviceColumnCatalog"/> 회귀 가드 — 콘솔 목록 그리드(N03)는 이 타입만 보고 열을 조립하므로,
/// 열 정의가 실제 행 뷰모델과 어긋나면(리네임·삭제) 여기서 먼저 터져야 한다(device-console-v8 N02-B).
/// </summary>
/// <remarks>
/// 표시 속성(<c>StatusDisplay</c>·<c>AddressDisplay</c> 등) 검증은 실제 <c>*DeviceViewModel</c> 을 만든다.
/// 그 생성자 체인이 <c>Caliburn.Micro.IoC</c> 정적 델리게이트를 부르므로(<see cref="TestIoCScope"/> 참고)
/// 이 클래스도 <c>[Collection("CaliburnIoC")]</c> 로 직렬화한다.
/// </remarks>
[Collection("CaliburnIoC")]
public class DeviceColumnCatalogTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    /// <summary>카테고리 → 목록 행이 실제로 바인딩하는 뷰모델 타입.</summary>
    private static readonly Dictionary<EnumDeviceCategory, Type> RowViewModelTypes = new()
    {
        [EnumDeviceCategory.Controller] = typeof(ControllerDeviceViewModel),
        [EnumDeviceCategory.Sensor] = typeof(SensorDeviceViewModel),
        [EnumDeviceCategory.Camera] = typeof(CameraDeviceViewModel),
        [EnumDeviceCategory.Speaker] = typeof(SpeakerDeviceViewModel),
        [EnumDeviceCategory.Enclosure] = typeof(EnclosureDeviceViewModel),
        [EnumDeviceCategory.Lamp] = typeof(LampDeviceViewModel),
        [EnumDeviceCategory.Gate] = typeof(GateDeviceViewModel),
    };

    public static IEnumerable<object[]> AllCategoryContractCombinations()
    {
        foreach (var category in RowViewModelTypes.Keys)
        foreach (var isAxisContract in new[] { true, false })
            yield return new object[] { category, isAxisContract };
    }

    /// <summary>
    /// 점(.)으로 이어진 바인딩 경로를 타입 위로 한 단계씩 따라간다 — 지금은 전부 단일 속성이지만,
    /// N03 이 나중에 중첩 경로(예: <c>Server.Name</c>)를 추가해도 이 가드가 그대로 잡아주도록 일반화해 둔다.
    /// </summary>
    private static bool TryResolvePropertyType(Type rootType, string bindingPath, out Type? resolvedType)
    {
        var current = rootType;
        foreach (var segment in bindingPath.Split('.'))
        {
            var property = current.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                resolvedType = null;
                return false;
            }
            current = property.PropertyType;
        }
        resolvedType = current;
        return true;
    }

    [Theory]
    [MemberData(nameof(AllCategoryContractCombinations))]
    public void should_resolve_every_binding_path_when_category_and_contract_given(EnumDeviceCategory category, bool isAxisContract)
    {
        var rowType = RowViewModelTypes[category];
        var columns = DeviceColumnCatalog.For(category, isAxisContract);

        Assert.NotEmpty(columns);
        foreach (var column in columns)
        {
            var resolved = TryResolvePropertyType(rowType, column.BindingPath, out _);
            Assert.True(resolved,
                $"{category}/{(isAxisContract ? "axis" : "legacy")}: 열 '{column.Key}' 의 BindingPath '{column.BindingPath}' 가 {rowType.Name} 에 없다 — 드리프트 발생.");
        }
    }

    [Fact]
    public void should_resolve_every_binding_path_when_groups_requested()
    {
        var columns = DeviceColumnCatalog.ForGroups();

        Assert.NotEmpty(columns);
        foreach (var column in columns)
        {
            var resolved = TryResolvePropertyType(typeof(DeviceGroupViewModel), column.BindingPath, out _);
            Assert.True(resolved, $"그룹 열 '{column.Key}' 의 BindingPath '{column.BindingPath}' 가 {nameof(DeviceGroupViewModel)} 에 없다.");
        }
    }

    [Theory]
    [MemberData(nameof(AllCategoryContractCombinations))]
    public void should_have_exactly_six_default_columns_with_status_first_and_one_star_column(EnumDeviceCategory category, bool isAxisContract)
    {
        var columns = DeviceColumnCatalog.For(category, isAxisContract);
        var defaults = columns.Where(c => c.IsDefault).ToList();

        Assert.Equal(6, defaults.Count);
        Assert.Equal("status", columns[0].Key);

        var starColumns = defaults.Where(c => c.Width == 0).ToList();
        var star = Assert.Single(starColumns);
        Assert.Equal("name", star.Key);
    }

    [Theory]
    [MemberData(nameof(AllCategoryContractCombinations))]
    public void should_have_unique_keys_when_category_and_contract_given(EnumDeviceCategory category, bool isAxisContract)
    {
        var columns = DeviceColumnCatalog.For(category, isAxisContract);

        Assert.Equal(columns.Count, columns.Select(c => c.Key).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllCategoryContractCombinations))]
    public void should_not_leak_the_other_contracts_columns_when_category_and_contract_given(EnumDeviceCategory category, bool isAxisContract)
    {
        var columns = DeviceColumnCatalog.For(category, isAxisContract);

        Assert.DoesNotContain(columns, c => isAxisContract ? c.LegacyContractOnly : c.AxisContractOnly);
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Controller, "address")]
    [InlineData(EnumDeviceCategory.Camera, "address")]
    [InlineData(EnumDeviceCategory.Lamp, "address")]
    [InlineData(EnumDeviceCategory.Sensor, "controller")]
    [InlineData(EnumDeviceCategory.Speaker, "server")]
    [InlineData(EnumDeviceCategory.Enclosure, "door")]
    [InlineData(EnumDeviceCategory.Gate, "door")]
    public void should_place_category_specific_column_fifth_when_default_columns_selected(EnumDeviceCategory category, string expectedKey)
    {
        var defaults = DeviceColumnCatalog.For(category, isAxisContract: true).Where(c => c.IsDefault).ToList();

        // 순서 계약: status · number · name · kind · {카테고리별 5번째} · enabled
        Assert.Equal(expectedKey, defaults[4].Key);
    }

    [Fact]
    public void should_bind_kind_column_to_type_axis_display_when_axis_contract()
    {
        var kind = DeviceColumnCatalog.For(EnumDeviceCategory.Controller, isAxisContract: true).Single(c => c.Key == "kind");

        Assert.Equal(nameof(DeviceViewModel.TypeAxisDisplay), kind.BindingPath);
        Assert.True(kind.AxisContractOnly);
        Assert.False(kind.LegacyContractOnly);
    }

    /// <summary>
    /// raw enum 이름("IpCamera" 등)이 그리드에 새지 않도록, 레거시 "종류" 열은 <see cref="DeviceViewModel.DeviceType"/>
    /// 이 아니라 한글 병기 표시 전용 프로퍼티(device-console enum-korean-consistency)에 묶인다.
    /// </summary>
    [Fact]
    public void should_bind_kind_column_to_device_type_display_when_legacy_contract()
    {
        var kind = DeviceColumnCatalog.For(EnumDeviceCategory.Controller, isAxisContract: false).Single(c => c.Key == "kind");

        Assert.Equal(nameof(DeviceViewModel.DeviceTypeDisplay), kind.BindingPath);
        Assert.True(kind.LegacyContractOnly);
        Assert.False(kind.AxisContractOnly);
    }

    [Fact]
    public void should_expose_removed_v7_camera_fields_only_under_legacy_contract()
    {
        var axis = DeviceColumnCatalog.For(EnumDeviceCategory.Camera, isAxisContract: true);
        var legacy = DeviceColumnCatalog.For(EnumDeviceCategory.Camera, isAxisContract: false);

        Assert.DoesNotContain(axis, c => c.Key is "mode" or "category" or "is_record");
        Assert.Contains(legacy, c => c.Key == "mode");
        Assert.Contains(legacy, c => c.Key == "category");
        Assert.Contains(legacy, c => c.Key == "is_record");
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceCategory.Gate)]
    public void should_expose_version_column_only_under_legacy_contract(EnumDeviceCategory category)
    {
        var axis = DeviceColumnCatalog.For(category, isAxisContract: true);
        var legacy = DeviceColumnCatalog.For(category, isAxisContract: false);

        Assert.DoesNotContain(axis, c => c.Key == "version");
        Assert.Contains(legacy, c => c.Key == "version");
    }

    [Theory]
    [InlineData(EnumDeviceCategory.None)]
    [InlineData(EnumDeviceCategory.Etc)]
    public void should_return_empty_when_category_has_no_console_panel(EnumDeviceCategory category)
    {
        Assert.Empty(DeviceColumnCatalog.For(category, isAxisContract: true));
        Assert.Empty(DeviceColumnCatalog.For(category, isAxisContract: false));
    }

    [Fact]
    public void should_return_three_columns_with_name_star_when_groups_requested()
    {
        var columns = DeviceColumnCatalog.ForGroups();

        Assert.Equal(3, columns.Count);
        Assert.Equal("name", columns[0].Key);
        Assert.Equal(0, columns[0].Width);
        Assert.DoesNotContain(columns.Skip(1), c => c.Width == 0);
        Assert.Equal(columns.Count, columns.Select(c => c.Key).Distinct().Count());
    }

    // ── 열이 실제로 바인딩하는 표시 속성 — StatusDisplay/AddressDisplay/ControllerDisplay/ServerDisplay ──
    // (device-console-v8 N02-B: BaseDeviceViewModel·Controller/Camera/Lamp/Sensor/SpeakerDeviceViewModel 에 추가)

    public static IEnumerable<object[]> AllDeviceStatuses() =>
        Enum.GetValues(typeof(EnumDeviceStatus)).Cast<EnumDeviceStatus>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(AllDeviceStatuses))]
    public void should_display_korean_status_label_when_status_given(EnumDeviceStatus status)
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel { Status = status });

        var expected = status switch
        {
            EnumDeviceStatus.ACTIVATED => "운영",
            EnumDeviceStatus.ERROR => "오류",
            EnumDeviceStatus.DEACTIVATED => "중지",
            _ => status.ToString(),
        };
        Assert.Equal(expected, vm.StatusDisplay);
    }

    [Theory]
    [InlineData("", 0, "—")]
    [InlineData("", 8080, "—")]
    [InlineData("10.20.1.11", 0, "10.20.1.11")]
    [InlineData("10.20.1.11", 4001, "10.20.1.11:4001")]
    public void should_format_address_display_when_controller_ip_and_port_given(string ip, int port, string expected)
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel { IpAddress = ip, Port = port });

        Assert.Equal(expected, vm.AddressDisplay);
    }

    [Theory]
    [InlineData("", 0, "—")]
    [InlineData("10.20.4.12", 0, "10.20.4.12")]
    [InlineData("10.20.4.12", 554, "10.20.4.12:554")]
    public void should_format_address_display_when_camera_ip_and_port_given(string ip, int port, string expected)
    {
        var vm = new CameraDeviceViewModel(new CameraDeviceModel { IpAddress = ip, IpPort = port });

        Assert.Equal(expected, vm.AddressDisplay);
    }

    [Theory]
    [InlineData("", 0, "—")]
    [InlineData("10.20.7.1", 0, "10.20.7.1")]
    [InlineData("10.20.7.1", 8080, "10.20.7.1:8080")]
    public void should_format_address_display_when_lamp_ip_and_port_given(string ip, int port, string expected)
    {
        var vm = new LampDeviceViewModel(new LampDeviceModel { IpAddress = ip, IpPort = port });

        Assert.Equal(expected, vm.AddressDisplay);
    }

    [Fact]
    public void should_return_dash_when_sensor_controller_is_null()
    {
        var vm = new SensorDeviceViewModel(new SensorDeviceModel { Controller = null! });

        Assert.Equal("—", vm.ControllerDisplay);
    }

    [Fact]
    public void should_return_controller_name_when_sensor_controller_is_set()
    {
        var vm = new SensorDeviceViewModel(new SensorDeviceModel { Controller = new ControllerDeviceModel { DeviceName = "정문 제어기" } });

        Assert.Equal("정문 제어기", vm.ControllerDisplay);
    }

    [Fact]
    public void should_return_dash_when_speaker_server_is_null()
    {
        var vm = new SpeakerDeviceViewModel(new SpeakerDeviceModel { Server = null });

        Assert.Equal("—", vm.ServerDisplay);
    }

    [Fact]
    public void should_return_server_name_when_speaker_server_is_set()
    {
        var vm = new SpeakerDeviceViewModel(new SpeakerDeviceModel { Server = new ServerModel { Name = "BRD-01" } });

        Assert.Equal("BRD-01", vm.ServerDisplay);
    }
}

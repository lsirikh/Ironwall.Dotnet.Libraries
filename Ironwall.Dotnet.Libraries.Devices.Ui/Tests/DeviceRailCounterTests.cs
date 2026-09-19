using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
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
/// <see cref="DeviceRailCounter"/> — 좌측 레일 배지 집계 순수 함수 검증(device-console-v8 N02-B).
/// </summary>
/// <remarks>
/// <c>IsFault</c> 교차검증(<c>should_match_rail_counter_fault_rule_when_row_view_model_status_given</c>)만
/// 실제 <c>ControllerDeviceViewModel</c> 을 만든다 — 그 생성자 체인이 <c>Caliburn.Micro.IoC</c> 정적
/// 델리게이트를 부르므로(<see cref="TestIoCScope"/> 참고) 클래스 전체를 <c>[Collection("CaliburnIoC")]</c> 로 직렬화한다.
/// </remarks>
[Collection("CaliburnIoC")]
public class DeviceRailCounterTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly EnumDeviceCategory[] ExpectedRailOrder =
    {
        EnumDeviceCategory.Controller,
        EnumDeviceCategory.Sensor,
        EnumDeviceCategory.Camera,
        EnumDeviceCategory.Speaker,
        EnumDeviceCategory.Enclosure,
        EnumDeviceCategory.Lamp,
        EnumDeviceCategory.Gate,
    };

    [Fact]
    public void should_return_seven_zeroed_entries_in_rail_order_when_no_devices_given()
    {
        var counts = DeviceRailCounter.Count(Array.Empty<IBaseDeviceModel>());

        Assert.Equal(ExpectedRailOrder, counts.Select(c => c.Category));
        Assert.All(counts, c => Assert.Equal(0, c.Total));
        Assert.All(counts, c => Assert.Equal(0, c.Fault));
    }

    [Fact]
    public void should_count_totals_and_faults_per_category_when_category_device_is_set()
    {
        var devices = new IBaseDeviceModel[]
        {
            new ControllerDeviceModel { Id = 1, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.ACTIVATED },
            new ControllerDeviceModel { Id = 2, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.ERROR },
            new ControllerDeviceModel { Id = 3, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.DEACTIVATED },
            new CameraDeviceModel(4) { CategoryDevice = EnumDeviceCategory.Camera, Status = EnumDeviceStatus.ACTIVATED },
        };

        var counts = DeviceRailCounter.Count(devices).ToDictionary(c => c.Category);

        Assert.Equal(3, counts[EnumDeviceCategory.Controller].Total);
        Assert.Equal(1, counts[EnumDeviceCategory.Controller].Fault);   // ERROR 하나만
        Assert.Equal(1, counts[EnumDeviceCategory.Camera].Total);
        Assert.Equal(0, counts[EnumDeviceCategory.Camera].Fault);
    }

    [Fact]
    public void should_report_zero_when_category_has_no_devices()
    {
        var devices = new IBaseDeviceModel[]
        {
            new ControllerDeviceModel { Id = 1, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.ACTIVATED },
        };

        var counts = DeviceRailCounter.Count(devices).ToDictionary(c => c.Category);

        Assert.Equal(0, counts[EnumDeviceCategory.Sensor].Total);
        Assert.Equal(0, counts[EnumDeviceCategory.Camera].Total);
        Assert.Equal(0, counts[EnumDeviceCategory.Speaker].Total);
        Assert.Equal(0, counts[EnumDeviceCategory.Enclosure].Total);
        Assert.Equal(0, counts[EnumDeviceCategory.Lamp].Total);
        Assert.Equal(0, counts[EnumDeviceCategory.Gate].Total);
    }

    [Fact]
    public void should_fall_back_to_clr_type_when_category_device_is_none()
    {
        // 6.3 스타일 — 경로 매핑을 안 거친 인스턴스는 category_device 가 채워지지 않는다.
        var device = new LampDeviceModel { Id = 9, Status = EnumDeviceStatus.ERROR };

        Assert.Equal(EnumDeviceCategory.None, device.CategoryDevice);
        Assert.Equal(EnumDeviceCategory.Lamp, DeviceRailCounter.CategoryOf(device));

        var counts = DeviceRailCounter.Count(new IBaseDeviceModel[] { device }).ToDictionary(c => c.Category);
        Assert.Equal(1, counts[EnumDeviceCategory.Lamp].Total);
        Assert.Equal(1, counts[EnumDeviceCategory.Lamp].Fault);
    }

    [Fact]
    public void should_prefer_explicit_category_device_over_clr_type_when_both_available()
    {
        // 판별자가 이미 채워져 있으면(v7.0+) CLR 타입 추정으로 되돌아가지 않는다 — 판별자가 정본.
        var device = new ControllerDeviceModel { Id = 1, CategoryDevice = EnumDeviceCategory.Gate };

        Assert.Equal(EnumDeviceCategory.Gate, DeviceRailCounter.CategoryOf(device));
    }

    [Fact]
    public void should_ignore_devices_whose_category_has_no_rail_slot()
    {
        var device = new ControllerDeviceModel { Id = 1, CategoryDevice = EnumDeviceCategory.Etc, Status = EnumDeviceStatus.ERROR };

        var counts = DeviceRailCounter.Count(new IBaseDeviceModel[] { device });

        Assert.All(counts, c => Assert.Equal(0, c.Total));
        Assert.All(counts, c => Assert.Equal(0, c.Fault));
    }

    [Theory]
    [InlineData(EnumDeviceStatus.ACTIVATED, false)]
    [InlineData(EnumDeviceStatus.ERROR, true)]
    [InlineData(EnumDeviceStatus.DEACTIVATED, false)]
    public void should_classify_fault_when_status_given(EnumDeviceStatus status, bool expectedFault)
    {
        Assert.Equal(expectedFault, DeviceRailCounter.IsFault(status));
    }

    [Fact]
    public void should_sum_totals_and_faults_across_categories_when_totals_requested()
    {
        var counts = new[]
        {
            new DeviceRailCount(EnumDeviceCategory.Controller, 3, 1),
            new DeviceRailCount(EnumDeviceCategory.Sensor, 5, 0),
            new DeviceRailCount(EnumDeviceCategory.Camera, 2, 2),
        };

        var (total, fault) = DeviceRailCounter.Totals(counts);

        Assert.Equal(10, total);
        Assert.Equal(3, fault);
    }

    [Fact]
    public void should_return_zero_totals_when_totals_given_empty_list()
    {
        var (total, fault) = DeviceRailCounter.Totals(Array.Empty<DeviceRailCount>());

        Assert.Equal(0, total);
        Assert.Equal(0, fault);
    }

    [Fact]
    public void should_throw_when_count_given_null_devices()
    {
        Assert.Throws<ArgumentNullException>(() => DeviceRailCounter.Count(null!));
    }

    [Fact]
    public void should_throw_when_totals_given_null_counts()
    {
        Assert.Throws<ArgumentNullException>(() => DeviceRailCounter.Totals(null!));
    }

    [Fact]
    public void should_skip_null_devices_when_counting()
    {
        var devices = new IBaseDeviceModel?[]
        {
            null,
            new ControllerDeviceModel { Id = 1, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.ACTIVATED },
        };

        var counts = DeviceRailCounter.Count(devices!).ToDictionary(c => c.Category);

        Assert.Equal(1, counts[EnumDeviceCategory.Controller].Total);
    }

    // ── BaseDeviceViewModel.IsFault — 행 단위 판정. DeviceRailCounter.IsFault 와 규칙이 같아야 한다 ──
    // (device-console-v8 N02-B: 둘은 독립적으로 계산하지만 결과가 어긋나면 목록 강조와 레일 배지가 따로 논다)

    public static IEnumerable<object[]> AllDeviceStatuses() =>
        Enum.GetValues(typeof(EnumDeviceStatus)).Cast<EnumDeviceStatus>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(AllDeviceStatuses))]
    public void should_match_rail_counter_fault_rule_when_row_view_model_status_given(EnumDeviceStatus status)
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel { Status = status });

        Assert.Equal(DeviceRailCounter.IsFault(status), vm.IsFault);
    }
}

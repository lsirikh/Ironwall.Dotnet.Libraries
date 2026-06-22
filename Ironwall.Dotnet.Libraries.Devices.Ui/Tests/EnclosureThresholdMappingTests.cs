using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 함체 임계값(threshold_config) 모델↔DTO 왕복 + DeviceEquals 회귀 가드.
/// PRD EnclosureThresholdDialog P1 — 매핑 양방향 드롭(BLOCKER급) 해소 보호.
/// </summary>
public class EnclosureThresholdMappingTests
{
    private static EnclosureDeviceModel MakeEnclosure(EnclosureThresholdConfigModel? th)
        => new()
        {
            DeviceNumber = 1,
            DeviceName = "함체_01",
            DoorStatus = "CLOSED",
            ThresholdConfig = th,
        };

    private static EnclosureThresholdConfigModel Th(double? th = 45, double? tl = -10, double? hh = 80, double? hl = 20, double? v = 5)
        => new() { TempHigh = th, TempLow = tl, HumidityHigh = hh, HumidityLow = hl, VibrationThreshold = v };

    [Fact]
    public void should_write_threshold_to_dto_when_model_has_it()
    {
        var dto = MakeEnclosure(Th()).ToEnclosureDeviceDto();

        Assert.NotNull(dto.ThresholdConfig);
        Assert.Equal(45.0, (double?)dto.ThresholdConfig!["temp_high"]);
        Assert.Equal(80.0, (double?)dto.ThresholdConfig["humidity_high"]);
    }

    [Fact]
    public void should_leave_dto_threshold_null_when_model_has_none()
    {
        var dto = MakeEnclosure(null).ToEnclosureDeviceDto();

        Assert.Null(dto.ThresholdConfig);
    }

    [Fact]
    public void should_not_write_empty_threshold_to_dto()
    {
        // 리뷰 M1: 전 필드 null인 빈 임계값(다이얼로그 주입)은 전송하지 않아 서버 값 보존
        var dto = MakeEnclosure(new EnclosureThresholdConfigModel()).ToEnclosureDeviceDto();

        Assert.Null(dto.ThresholdConfig);
    }

    [Fact]
    public void should_read_threshold_from_dto_jobject()
    {
        var dto = new EnclosureDeviceDto
        {
            NumberDevice = 1,
            NameDevice = "함체_01",
            ThresholdConfig = JObject.FromObject(Th(th: 50, v: 7.5))
        };

        var model = dto.ToEnclosureDeviceModel();

        Assert.NotNull(model.ThresholdConfig);
        Assert.Equal(50.0, model.ThresholdConfig!.TempHigh);
        Assert.Equal(7.5, model.ThresholdConfig.VibrationThreshold);
    }

    [Fact]
    public void should_round_trip_threshold_values()
    {
        var model = MakeEnclosure(Th(th: 42, tl: -5, hh: 75, hl: 30, v: 3.3));

        var result = model.ToEnclosureDeviceDto().ToEnclosureDeviceModel();

        Assert.Equal(42.0, result.ThresholdConfig!.TempHigh);
        Assert.Equal(-5.0, result.ThresholdConfig.TempLow);
        Assert.Equal(75.0, result.ThresholdConfig.HumidityHigh);
        Assert.Equal(30.0, result.ThresholdConfig.HumidityLow);
        Assert.Equal(3.3, result.ThresholdConfig.VibrationThreshold);
    }

    // ───────── DeviceEquals 임계값 변경 감지 ─────────
    [Fact]
    public void should_detect_change_when_threshold_differs()
    {
        Assert.False(EnclosureDevicePanelViewModel.DeviceEquals(MakeEnclosure(Th(th: 45)), MakeEnclosure(Th(th: 50))));
    }

    [Fact]
    public void should_be_equal_when_threshold_same()
    {
        Assert.True(EnclosureDevicePanelViewModel.DeviceEquals(MakeEnclosure(Th()), MakeEnclosure(Th())));
    }

    [Fact]
    public void should_treat_null_and_empty_threshold_as_equal()
    {
        // 다이얼로그가 빈 임계값을 생성해도 허위 변경감지가 나지 않아야 함
        Assert.True(EnclosureDevicePanelViewModel.DeviceEquals(MakeEnclosure(null), MakeEnclosure(new EnclosureThresholdConfigModel())));
    }
}

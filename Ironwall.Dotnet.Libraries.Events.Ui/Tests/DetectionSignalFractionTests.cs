using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Converters;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Events;
using ApiListResponse = Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiListResponse<Ironwall.Dotnet.Libraries.Messages.Dto.Events.DetectionEventDto>;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 탐지 신호값 실수(#4) — 화면 · 모델 · 본문 왕복 시험
                  신호는 정수 · 실수 모두(서버 Optional[float]). 정수는 "21", 실수는 "21.5"(꼬리 0 없이),
                  모델 · 본문을 거쳐도 반올림 · 21.0 변형이 없어야 한다.
****************************************************************************/
public class DetectionSignalFractionTests
{
    #region - Helpers -
    private static DetectionEventDto BuildDto(int id, double? signal, string createdAt = "2026-10-12T10:00:00")
        => new()
        {
            Id = id,
            CreatedAt = createdAt,
            TypeEvent = "Intrusion",
            ActionReported = "True",
            Result = "PIR_SENSOR",
            Device = new Ironwall.Dotnet.Libraries.Messages.Dto.Devices.BaseDeviceDto { Id = 5 },
            Detail = signal is double s ? new DetectionDetailDto { Signal = s } : null
        };

    private static T WithCulture<T>(string name, Func<T> body)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { return body(); }
        finally { CultureInfo.CurrentCulture = saved; }
    }
    #endregion

    #region - 표시 문구 -
    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-001")]
    [InlineData(21d, "21")]
    [InlineData(21.5d, "21.5")]
    [InlineData(32.2d, "32.2")]
    [InlineData(21.25d, "21.25")]
    [InlineData(3400d, "3,400")]
    [InlineData(3400.5d, "3,400.5")]
    public void should_show_integer_as_integer_and_fraction_as_is_when_signal_formatted(double signal, string expected)
    {
        var text = WithCulture("ko-KR", () => SignalFormat.Text(signal));

        Assert.Equal(expected, text);
    }

    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-002")]
    [InlineData(null)]
    [InlineData(0d)]
    public void should_show_dash_when_signal_missing_or_zero(double? signal)
    {
        Assert.Equal("—", SignalFormat.Text(signal));
    }
    #endregion

    #region - DTO → 모델 → 본문 왕복 -
    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-003")]
    public void should_keep_fraction_when_dto_mapped_to_model()
    {
        var model = BuildDto(1, 21.5).ToDetectionEventModel();

        Assert.Equal(21.5, model.Signal);
    }

    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-004")]
    [InlineData(21d, "\"signal\":21}")]
    [InlineData(21.5d, "\"signal\":21.5}")]
    public void should_write_signal_in_read_shape_when_replace_body_rebuilt_from_model(double signal, string expected)
    {
        // 읽기(DTO) → 모델 → PUT 본문(ReplaceDto) — 정수는 21 그대로(21.0 아님), 실수는 반올림 없이
        var model = BuildDto(1, signal).ToDetectionEventModel();

        var json = JsonConvert.SerializeObject(model.ToDetectionEventReplaceDto());

        Assert.Contains(expected, json);
        Assert.DoesNotContain("21.0", json);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-005")]
    public void should_keep_fractional_bbox_when_objects_mapped_both_ways()
    {
        var dto = BuildDto(1, 0);
        dto.Detail!.Objects = new List<DetectedObjectDto>
        {
            new() { Label = "person", Confidence = 0.9, Bbox = new List<double> { 0.168, 0.213, 0.095, 0.453 } }
        };

        var model = dto.ToDetectionEventModel();
        var json = JsonConvert.SerializeObject(model.ToDetectionEventReplaceDto());

        Assert.Equal(new List<double> { 0.168, 0.213, 0.095, 0.453 }, model.Objects![0].Bbox);
        Assert.Contains("\"bbox\":[0.168,0.213,0.095,0.453]", json);
    }
    #endregion

    #region - 미니바 변환기: 실수 · 정수 모두 -
    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-006")]
    public void should_draw_bar_for_fractional_signal_when_converter_gets_double()
    {
        var width = new SignalBarWidthConverter().Convert(new object[] { 21.5d, 43d }, typeof(double), "64", CultureInfo.InvariantCulture);
        var isMax = new SignalIsMaxConverter().Convert(new object[] { 43d, 43d }, typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.Equal(32d, (double)width, 6);   // 수정 전: as int? 로 받아 0 폭
        Assert.Equal(true, isMax);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-007")]
    public void should_still_draw_bar_when_converter_gets_int_from_host_preview()
    {
        // 호스트 미리보기(shell-surface-preview)는 아직 int Signal · int MaxSignal 을 바인딩한다
        var width = new SignalBarWidthConverter().Convert(new object[] { 435, 870 }, typeof(double), "64", CultureInfo.InvariantCulture);

        Assert.Equal(32d, (double)width, 6);
    }
    #endregion

    #region - 탐지 이력 다이얼로그: 그리드 · 차트 · 통계 -
    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-008")]
    public async Task should_show_fractional_signals_in_history_grid_chart_and_stats()
    {
        var apiMock = new Mock<IEventApiService>();
        apiMock.Setup(EventApiQuery.AnyDetection())
            .ReturnsAsync(new ApiListResponse
            {
                Success = true,
                Data = new List<DetectionEventDto>
                {
                    BuildDto(1, 21, "2026-10-12T10:00:00"),
                    BuildDto(2, 21.5, "2026-10-12T10:01:00"),
                    BuildDto(3, 32.2, "2026-10-12T10:02:00"),
                }
            });
        var eaMock = new Mock<IEventAggregator>();
        eaMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        var vm = new DetectionHistoryDialogViewModel(eaMock.Object, new Mock<ILogService>().Object, apiMock.Object, new DeviceProvider());
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 5 });

        await ((IActivate)vm).ActivateAsync(CancellationToken.None);

        Assert.Equal(3, vm.FilteredItems.Count);
        Assert.Equal(new[] { "21", "21.5", "32.2" }, vm.FilteredItems.OrderBy(i => i.EventId).Select(i => i.SignalText));
        Assert.Equal(new[] { 21d, 21.5d, 32.2d }, vm.ChartPoints.OrderBy(p => p.Time).Select(p => p.Signal));
        Assert.Equal(32.2, vm.MaxSignal);
        Assert.Equal("32.2", vm.MaxSignalText);
        Assert.Equal("24.9", vm.AvgSignalText);   // (21 + 21.5 + 32.2) / 3 = 24.9
    }
    #endregion
}

[Collection("IoC-Dependent")]
public class DetectionSignalFractionRowTests : IoCStubbedTestBase
{
    private static DetectionEventViewModel BuildRow(double? signal)
        => new(new DetectionEventModel
        {
            Id = 100,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            DateTime = new DateTime(2026, 10, 12, 10, 0, 0),
            Signal = signal
        });

    [Theory]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-009")]
    [InlineData(21d, "21")]
    [InlineData(21.5d, "21.5")]
    public void should_show_signal_text_in_grid_row_and_property_panel(double signal, string expected)
    {
        var row = BuildRow(signal);
        var selection = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        Assert.Equal(expected, row.SignalText);
        Assert.Equal(expected, selection.SignalText);
        Assert.Equal(expected, selection.SignalEditText);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-010")]
    public void should_keep_typed_decimal_point_and_apply_fraction_when_signal_edited()
    {
        var row = BuildRow(1200);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        sut.SignalEditText = "21.";          // 소수점까지 친 순간 — 글자가 "21" 로 되돌아가면 안 된다
        Assert.Equal("21.", sut.SignalEditText);
        Assert.Equal(21d, sut.SignalEdit);

        sut.SignalEditText = "21.5";
        sut.ApplyButton();

        Assert.True(row.IsEdited);
        Assert.Equal(21.5, ((IDetectionEventModel)row.Model).Signal);
        Assert.Equal("21.5", row.SignalText);
    }

    [Fact]
    [Trait("Scenario", "SIGNAL-FLOAT-UI-011")]
    public void should_not_change_signal_when_edit_text_is_not_a_number()
    {
        var row = BuildRow(1200);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        sut.SignalEditText = "abc";
        sut.ApplyButton();

        Assert.False(row.IsEdited);
        Assert.Equal(1200d, ((IDetectionEventModel)row.Model).Signal);
    }
}

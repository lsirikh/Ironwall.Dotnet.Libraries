using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Events;
using ApiListResponse = Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiListResponse<Ironwall.Dotnet.Libraries.Messages.Dto.Events.DetectionEventDto>;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : Detection_Signal_History 단위 테스트 (TEST-01~04)
                  매핑 왕복(FR-01/02) · Replace detail 보존(FR-03) ·
                  조회 엔진 상한/실패(FR-12) · 필터/통계(FR-14, RISK-01 null-안전)
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionSignalTests
{
    #region - Helpers -
    private static DetectionEventDto BuildDto(int id, int? signal, bool actioned = true,
        string result = "PIR_SENSOR", string createdAt = "2026-07-22T11:05:12")
        => new()
        {
            Id = id,
            CreatedAt = createdAt,
            TypeEvent = "Intrusion",
            ActionReported = actioned ? "True" : "False",
            Result = result,
            Detail = signal is int s ? new DetectionDetailDto { Signal = s } : null
        };

    private static DetectionHistoryDialogViewModel CreateVm(Mock<IEventApiService> apiMock, out Mock<IEventAggregator> eaMock)
    {
        eaMock = new Mock<IEventAggregator>();
        eaMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        var logMock = new Mock<ILogService>();
        return new DetectionHistoryDialogViewModel(eaMock.Object, logMock.Object, apiMock.Object, new DeviceProvider());
    }

    private static async Task ActivateAsync(DetectionHistoryDialogViewModel vm)
        => await ((IActivate)vm).ActivateAsync(CancellationToken.None);
    #endregion

    #region - TEST-01 매핑 왕복 (FR-01/02) -
    [Fact]
    public void should_map_signal_when_detail_present()
    {
        var dto = BuildDto(1, 2000);

        var model = dto.ToDetectionEventModel();
        var modelWithProvider = dto.ToDetectionEventModel(null);

        Assert.Equal(2000, model.Signal);
        Assert.Equal(2000, modelWithProvider.Signal);
    }

    [Fact]
    public void should_map_null_signal_when_detail_missing()
    {
        var dto = BuildDto(1, signal: null);

        var model = dto.ToDetectionEventModel();

        Assert.Null(model.Signal);
    }

    [Fact]
    public void should_rebuild_detail_when_signal_present_on_todto()
    {
        var model = new DetectionEventModel
        {
            Id = 7,
            DateTime = DateTime.Now,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            Signal = 3150
        };

        var dto = model.ToDetectionEventDto();

        Assert.NotNull(dto.Detail);
        Assert.Equal(3150, dto.Detail!.Signal);
    }

    [Fact]
    public void should_omit_detail_when_signal_null_on_todto()
    {
        var model = new DetectionEventModel
        {
            Id = 7,
            DateTime = DateTime.Now,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            Signal = null
        };

        var dto = model.ToDetectionEventDto();

        Assert.Null(dto.Detail);
    }

    [Fact]
    public void should_copy_signal_when_copy_constructor_used()
    {
        var source = new DetectionEventModel { Result = EnumDetectionType.PIR_SENSOR, Signal = 820 };

        var copy = new DetectionEventModel(source);

        Assert.Equal(820, copy.Signal);
    }

    [Fact]
    public void should_map_full_detail_when_present()
    {
        // detail 전체(objects/model/inference_ms/thumbnail) 표면화 — 런타임 피드백 v1.3
        var dto = BuildDto(1, 1500);
        dto.Detail!.Model = "yolov8n";
        dto.Detail.InferenceMs = 45;
        dto.Detail.Thumbnail = "http://192.168.1.50:8080/events/1001/thumb.jpg";
        dto.Detail.Objects = new List<DetectedObjectDto>
        {
            new() { Label = "person", Confidence = 0.95, Bbox = new List<int> { 100, 200, 50, 100 } }
        };

        var model = dto.ToDetectionEventModel();

        Assert.Equal(1500, model.Signal);
        Assert.Equal("yolov8n", model.AiModel);
        Assert.Equal(45, model.InferenceMs);
        Assert.Equal("http://192.168.1.50:8080/events/1001/thumb.jpg", model.Thumbnail);
        Assert.Single(model.Objects!);
        Assert.Equal("person", model.Objects![0].Label);
        Assert.Equal(0.95, model.Objects[0].Confidence);
        Assert.Equal(new List<int> { 100, 200, 50, 100 }, model.Objects[0].Bbox);
    }

    [Fact]
    public void should_rebuild_full_detail_when_ai_fields_present_on_replace()
    {
        // AI 이벤트(signal=0)도 detail 필드가 있으면 PUT에서 전체 보존돼야 한다(서버 전체 교체 계약)
        var model = new DetectionEventModel
        {
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.AI_DETECT,
            Signal = 0,
            AiModel = "yolov8n",
            InferenceMs = 45,
            Thumbnail = "http://t/1.jpg",
            Objects = new List<DetectionObjectModel>
            {
                new() { Label = "person", Confidence = 0.95, Bbox = new List<int> { 1, 2, 3, 4 } }
            }
        };

        var dto = model.ToDetectionEventReplaceDto();

        Assert.NotNull(dto.Detail);
        Assert.Equal(0, dto.Detail!.Signal);
        Assert.Equal("yolov8n", dto.Detail.Model);
        Assert.Equal(45, dto.Detail.InferenceMs);
        Assert.Single(dto.Detail.Objects!);
        Assert.Equal("person", dto.Detail.Objects![0].Label);
    }

    [Fact]
    public void should_preserve_frame_dims_on_replace_roundtrip()
    {
        // audit 후속: PUT Replace가 frame_width/frame_height를 소실하지 않아야 함(bbox 스케일 기준 보존)
        var dto = BuildDto(1, 0, result: "AI_DETECT");
        dto.Detail!.FrameWidth = 1920;
        dto.Detail.FrameHeight = 1080;

        var model = dto.ToDetectionEventModel();
        Assert.Equal(1920, model.FrameWidth);
        Assert.Equal(1080, model.FrameHeight);

        var replace = model.ToDetectionEventReplaceDto();
        Assert.NotNull(replace.Detail);
        Assert.Equal(1920, replace.Detail!.FrameWidth);
        Assert.Equal(1080, replace.Detail.FrameHeight);
    }

    [Fact]
    public void should_deep_copy_objects_when_copy_constructor_used()
    {
        // audit 후속: 복사생성자가 Objects를 깊은 복사(리스트/Bbox 공유 방지)
        var source = new DetectionEventModel
        {
            Result = EnumDetectionType.AI_DETECT,
            Objects = new List<DetectionObjectModel>
            {
                new() { Label = "person", Confidence = 0.9, Bbox = new List<int> { 1, 2, 3, 4 } }
            }
        };

        var copy = new DetectionEventModel(source);
        copy.Objects![0].Bbox![0] = 999;

        Assert.NotSame(source.Objects, copy.Objects);
        Assert.Equal(1, source.Objects![0].Bbox![0]);   // 원본 불변
    }
    #endregion

    #region - TEST-02 Replace detail 보존 (FR-03) -
    [Fact]
    public void should_include_detail_in_replace_dto_when_signal_present()
    {
        var model = new DetectionEventModel
        {
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.THERMAL_SENSOR,
            Signal = 1720
        };

        var dto = model.ToDetectionEventReplaceDto();

        // 서버 PUT은 detail "전체 교체" — 미전송 시 소실되므로 반드시 재구성돼야 한다
        Assert.NotNull(dto.Detail);
        Assert.Equal(1720, dto.Detail!.Signal);
    }

    [Fact]
    public void should_omit_detail_in_replace_dto_when_signal_null()
    {
        var model = new DetectionEventModel
        {
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.THERMAL_SENSOR,
            Signal = null
        };

        var dto = model.ToDetectionEventReplaceDto();

        // 신호가 애초에 없던 이벤트는 기존 페이로드와 동일(필드 생략)
        Assert.Null(dto.Detail);
    }
    #endregion

    #region - TEST-03 조회 엔진 (FR-12) -
    [Fact]
    public async Task should_stop_at_500_when_pages_exceed_limit()
    {
        var apiMock = new Mock<IEventApiService>();
        int calls = 0;
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? s, string? e, int? c, int? sensor, string? st, int page, int limit, CancellationToken ct) =>
            {
                calls++;
                // 매 페이지 만건 응답(100건) — 무한 데이터 시뮬레이션
                var data = Enumerable.Range(0, limit)
                    .Select(i => BuildDto((page - 1) * limit + i + 1, 100 + i))
                    .ToList();
                return new ApiListResponse { Success = true, Data = data };
            });

        var vm = CreateVm(apiMock, out _);
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 5, DeviceName = "Sensor-A-1", DeviceNumber = 1 });

        await ActivateAsync(vm);

        Assert.Equal(500, vm.FilteredItems.Count);   // 상한 500 절단
        Assert.True(vm.IsTruncated);
        Assert.Equal(5, calls);                       // 100×5 페이지에서 중단
    }

    [Fact]
    public async Task should_keep_empty_state_when_api_fails()
    {
        var apiMock = new Mock<IEventApiService>();
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse { Success = false, Message = "server down" });

        var vm = CreateVm(apiMock, out var eaMock);
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 5 });

        await ActivateAsync(vm);

        Assert.True(vm.HasLoadError);
        Assert.Empty(vm.FilteredItems);
        Assert.Empty(vm.ChartPoints);
        // 표준 팝업(EventAggregator) 발행 확인 — raw MessageBox 금지 규약
        eaMock.Verify(x => x.PublishAsync(
            It.Is<object>(m => m is OpenInfoPopupMessageModel),
            It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    #endregion

    #region - TEST-04 필터/통계 (FR-14, RISK-01) -
    private static Mock<IEventApiService> BuildMixedApi()
    {
        // PIR 3건(820 미조치 / 1000 조치 / 3000 조치) + AI_DETECT 2건(signal 0)
        var data = new List<DetectionEventDto>
        {
            BuildDto(1, 820, actioned: false),
            BuildDto(2, 1000, actioned: true),
            BuildDto(3, 3000, actioned: true),
            BuildDto(4, 0, actioned: true, result: "AI_DETECT"),
            BuildDto(5, 0, actioned: false, result: "AI_DETECT"),
        };
        var apiMock = new Mock<IEventApiService>();
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse { Success = true, Data = data });
        return apiMock;
    }

    [Fact]
    public async Task should_exclude_zero_signal_when_ai_chip_default_off()
    {
        var vm = CreateVm(BuildMixedApi(), out _);
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 5 });

        await ActivateAsync(vm);

        // AI_DETECT 칩은 신호 전무(전부 0)라 기본 off → 그리드/차트/통계에서 제외
        var aiChip = vm.Chips.Single(c => c.Result == EnumDetectionType.AI_DETECT);
        Assert.False(aiChip.IsOn);
        Assert.Equal(3, vm.FilteredItems.Count);
        Assert.Equal(3, vm.ChartPoints.Count);
        Assert.Equal(3, vm.TotalCount);

        // 칩을 켜면 그리드에 포함되지만 차트(signal>0)에는 그대로 제외 (null-안전 규약)
        aiChip.IsOn = true;
        Assert.Equal(5, vm.FilteredItems.Count);
        Assert.Equal(3, vm.ChartPoints.Count);
    }

    [Fact]
    public async Task should_filter_unactioned_when_toggle_on()
    {
        var vm = CreateVm(BuildMixedApi(), out _);
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 5 });

        await ActivateAsync(vm);
        vm.OnlyUnactioned = true;

        Assert.Single(vm.FilteredItems);              // PIR 미조치 1건 (AI 미조치는 칩 off로 제외)
        Assert.False(vm.FilteredItems[0].IsActioned);
    }

    [Fact]
    public async Task should_compute_stats_when_items_loaded()
    {
        var vm = CreateVm(BuildMixedApi(), out _);
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 5 });

        await ActivateAsync(vm);

        // 신호 통계는 signal>0만 (820/1000/3000)
        Assert.Equal(3000, vm.MaxSignal);
        Assert.Equal(1607, (int)Math.Round(double.Parse(vm.AvgSignalText.Replace(",", ""))));
        Assert.Equal(1, vm.UnactionedCount);
        Assert.StartsWith("PIR_SENSOR", vm.TopResultText);
    }
    #endregion
}

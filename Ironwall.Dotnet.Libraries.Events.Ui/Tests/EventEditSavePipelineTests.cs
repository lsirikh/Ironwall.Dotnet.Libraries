using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : PRD Event_Edit_Save_Pipeline — 배치 1(FR-01~06) 단위 테스트
                  · FR-01/02 detail 편집이 dirty(IsEdited)를 켜고 저장 대상에 포함되는가
                  · FR-03 값이 안 바뀌면 dirty를 켜지 않는가(거짓 PUT 방지)
                  · FR-04 Draft 수집 소스가 ViewModelProvider 단일 기준인가
                  · FR-05 "0건 저장"과 "저장 성공"이 구분 통지되는가
                  · FR-06 적용 직후 그리드 표시값(SignalText)이 갱신되는가
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

#region - 공통: IoC 스텁 -
/// <summary>
/// 이벤트 행/속성창 ViewModel은 생성자에서 <c>IoC.Get&lt;IEventAggregator&gt;</c>·<c>IoC.Get&lt;ILogService&gt;</c>
/// (BasePanelViewModel)를 호출하므로 헤드리스 테스트에서는 IoC를 스텁해야 한다.
/// IoC는 전역 정적 상태라 <c>[Collection("IoC-Dependent")]</c> 로 직렬화한다(기존 테스트와 동일 규약).
/// </summary>
public abstract class IoCStubbedTestBase : IDisposable
{
    protected readonly DeviceProvider DeviceProviderStub = new();

    protected IoCStubbedTestBase()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            if (type == typeof(DeviceProvider)) return DeviceProviderStub;
            return null!;
        };
        IoC.GetAllInstances = type => Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = (type, key) => throw new InvalidOperationException("IoC is not initialized.");
        IoC.GetAllInstances = type => throw new InvalidOperationException("IoC is not initialized.");
        IoC.BuildUp = obj => throw new InvalidOperationException("IoC is not initialized.");
        GC.SuppressFinalize(this);
    }
}
#endregion

#region - 행 ViewModel: detail 편집 → dirty 경로 (FR-01/02/03/06) -
[Collection("IoC-Dependent")]
public class DetectionDetailDirtyTests : IoCStubbedTestBase
{
    private static DetectionEventViewModel BuildRow(int id = 100, int? signal = 1200,
        string? aiModel = null, int? inferenceMs = null, string? thumbnail = null,
        List<DetectionObjectModel>? objects = null)
        => new(new DetectionEventModel
        {
            Id = id,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            DateTime = new DateTime(2026, 8, 3, 12, 0, 0),
            Signal = signal,
            AiModel = aiModel,
            InferenceMs = inferenceMs,
            Thumbnail = thumbnail,
            Objects = objects
        });

    /// <summary>저장 루프(DetectionEventPanelViewModel)의 수정 대상 필터와 동일한 술어.</summary>
    private static IReadOnlyList<DetectionEventViewModel> UpdateTargets(IEnumerable<DetectionEventViewModel> rows)
        => rows.Where(vm => vm.IsEdited && vm.Model.Id > 0).ToList();

    [Fact]
    public void should_mark_row_as_edited_when_only_signal_changed()
    {
        // Arrange
        var row = BuildRow(signal: 1200);
        Assert.False(row.IsEdited);

        // Act — detail 한 개만 변경
        row.Signal = 3400;

        // Assert
        Assert.True(row.IsEdited);
        Assert.Equal(3400, ((IDetectionEventModel)row.Model).Signal);
    }

    [Fact]
    public void should_not_mark_row_as_edited_when_signal_value_unchanged()
    {
        // Arrange
        var row = BuildRow(signal: 1200);

        // Act — 같은 값 재대입
        row.Signal = 1200;

        // Assert — 거짓 dirty 금지(불필요 PUT·감사로그 오염 방지, FR-03)
        Assert.False(row.IsEdited);
    }

    [Fact]
    public void should_mark_row_as_edited_when_ai_model_changed()
    {
        var row = BuildRow(aiModel: "yolov8n");

        row.AiModel = "yolov8s";

        Assert.True(row.IsEdited);
        Assert.Equal("yolov8s", ((IDetectionEventModel)row.Model).AiModel);
    }

    [Fact]
    public void should_mark_row_as_edited_when_frame_size_changed()
    {
        var row = BuildRow();

        row.FrameWidth = 1920;
        row.FrameHeight = 1080;

        Assert.True(row.IsEdited);
        var m = (IDetectionEventModel)row.Model;
        Assert.Equal(1920, m.FrameWidth);
        Assert.Equal(1080, m.FrameHeight);
    }

    [Fact]
    public void should_mark_row_as_edited_when_inference_ms_changed()
    {
        var row = BuildRow(inferenceMs: 45);

        row.InferenceMs = 61;

        Assert.True(row.IsEdited);
        Assert.Equal(61, ((IDetectionEventModel)row.Model).InferenceMs);
    }

    [Fact]
    public void should_notify_signal_text_when_signal_changed()
    {
        // Arrange — FR-06: 적용 직후 그리드 신호 컬럼/미니바가 즉시 갱신되어야 한다
        var row = BuildRow(signal: 1200);
        var raised = new List<string>();
        ((INotifyPropertyChanged)row).PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        // Act
        row.Signal = 5600;

        // Assert
        Assert.Contains(nameof(DetectionEventViewModel.Signal), raised);
        Assert.Contains(nameof(DetectionEventViewModel.SignalText), raised);
        Assert.Contains(nameof(DetectionEventViewModel.HasSignal), raised);
        Assert.Equal("5,600", row.SignalText);
    }

    [Fact]
    public void should_not_raise_signal_text_when_signal_unchanged()
    {
        var row = BuildRow(signal: 1200);
        var raised = new List<string>();
        ((INotifyPropertyChanged)row).PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        row.Signal = 1200;

        Assert.DoesNotContain(nameof(DetectionEventViewModel.SignalText), raised);
    }

    [Fact]
    public void should_not_mark_row_as_edited_when_model_updated_directly_by_server_sync()
    {
        // Arrange — FR-02 예외: 서버발 갱신(NATS SYNC·썸네일 후속 수신)은 사용자의 편집이 아니다.
        //           모델 직접 갱신 경로는 dirty를 켜지 않아야 불필요한 PUT이 발생하지 않는다.
        var row = BuildRow(signal: 1200);

        // Act — 서버발 갱신을 모사(모델 직접 대입)
        ((IDetectionEventModel)row.Model).Signal = 9999;
        ((IDetectionEventModel)row.Model).Thumbnail = "https://server/api/thumbnails/x.jpg";

        // Assert
        Assert.False(row.IsEdited);
    }

    [Fact]
    public void should_include_row_in_update_targets_when_only_detail_changed()
    {
        // Arrange — 이 테스트가 사용자 증상의 핵심 재현이다.
        //           수정 전에는 detail 편집이 모델 직접 대입이라 IsEdited=false → 저장 대상 0건 → 무음 실패.
        var edited = BuildRow(id: 100, signal: 1200);
        var untouched = BuildRow(id: 101, signal: 800);
        var draft = BuildRow(id: 0, signal: 500);

        // Act
        edited.Signal = 3400;
        var targets = UpdateTargets(new[] { edited, untouched, draft });

        // Assert
        Assert.Single(targets);
        Assert.Same(edited, targets[0]);
    }

    [Fact]
    public void should_send_signal_in_put_dto_when_only_detail_edited()
    {
        // Arrange
        var row = BuildRow(signal: 1200, aiModel: "yolov8n", inferenceMs: 45);

        // Act
        row.Signal = 3400;
        row.AiModel = "yolov8s";
        var dto = ((IDetectionEventModel)row.Model).ToDetectionEventReplaceDto();

        // Assert
        Assert.NotNull(dto.Detail);
        Assert.Equal(3400, dto.Detail!.Signal);
        Assert.Equal("yolov8s", dto.Detail.Model);
        Assert.Equal(45, dto.Detail.InferenceMs);
    }

    [Fact]
    public void should_preserve_thumbnail_and_objects_when_detail_updated()
    {
        // Arrange — 서버 PUT은 detail 통째 교체이므로, 편집하지 않은 키도 함께 실려야 유실되지 않는다.
        const string thumb = "https://server/api/thumbnails/images/CAM-008.jpg";
        var objects = new List<DetectionObjectModel>
        {
            new() { Label = "person", Confidence = 0.95, Bbox = new List<int> { 100, 200, 50, 100 } }
        };
        var row = BuildRow(signal: 1200, thumbnail: thumb, objects: objects);

        // Act
        row.Signal = 3400;
        var dto = ((IDetectionEventModel)row.Model).ToDetectionEventReplaceDto();

        // Assert
        Assert.Equal(thumb, dto.Detail!.Thumbnail);
        Assert.NotNull(dto.Detail.Objects);
        Assert.Single(dto.Detail.Objects!);
        Assert.Equal("person", dto.Detail.Objects![0].Label);
    }
}
#endregion

#region - 속성창 적용(SelectionViewModel) → 행 VM 반영 (FR-01/02) -
[Collection("IoC-Dependent")]
public class DetectionSelectionApplyTests : IoCStubbedTestBase
{
    private static DetectionEventViewModel BuildRow(int id, int? signal, string? aiModel = null)
        => new(new DetectionEventModel
        {
            Id = id,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            DateTime = new DateTime(2026, 8, 3, 12, 0, 0),
            Signal = signal,
            AiModel = aiModel
        });

    [Fact]
    public void should_mark_row_as_edited_when_apply_called_with_detail_edit()
    {
        // Arrange
        var row = BuildRow(100, 1200);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Act — 사용자가 신호 칸을 고치고 [적용]
        sut.SignalEdit = 3400;
        sut.ApplyButton();

        // Assert — 저장 루프가 이 행을 집어갈 수 있어야 한다
        Assert.True(row.IsEdited);
        Assert.Equal(3400, ((IDetectionEventModel)row.Model).Signal);
    }

    [Fact]
    public void should_not_mark_row_as_edited_when_apply_called_without_any_change()
    {
        // Arrange
        var row = BuildRow(100, 1200);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Act — 아무것도 고치지 않고 [적용]
        sut.ApplyButton();

        // Assert
        Assert.False(row.IsEdited);
    }

    [Fact]
    public void should_keep_previous_value_when_edit_field_is_null()
    {
        // Arrange — 빈칸 = "변경 없음"(기존 값 유지)이 현행 계약이다.
        var row = BuildRow(100, 1200, aiModel: "yolov8n");
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Act — 신호만 고치고 AI 모델은 비운다
        sut.SignalEdit = 3400;
        sut.AiModelEdit = null;
        sut.ApplyButton();

        // Assert
        var m = (IDetectionEventModel)row.Model;
        Assert.Equal(3400, m.Signal);
        Assert.Equal("yolov8n", m.AiModel);   // 비운 항목은 그대로 유지
    }

    [Fact]
    public void should_seed_edit_model_from_selection_when_constructed()
    {
        // Arrange & Act — 편집 버퍼는 선택의 공통값으로 시딩된다
        var row = BuildRow(100, 1200, aiModel: "yolov8n");
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Assert — 래퍼와 편집 모델이 같은 상태를 가리킨다(정본 1곳)
        Assert.Equal(1200, sut.Edit.Signal);
        Assert.Equal("yolov8n", sut.Edit.AiModel);
        Assert.Equal(EnumDetectionType.PIR_SENSOR, sut.Edit.Result);
        Assert.Equal(sut.Edit.Signal, sut.SignalEdit);
        Assert.Equal(sut.Edit.Result, sut.Result);
    }

    [Fact]
    public void should_not_seed_detail_when_multiple_rows_selected()
    {
        // Arrange — 상세는 이벤트별 계측값이라 다중 선택에서 대표값처럼 보이면 안 된다
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel>
        {
            BuildRow(100, 1200), BuildRow(101, 800)
        });

        // Assert
        Assert.Null(sut.Edit.Signal);
        Assert.Null(sut.Edit.AiModel);
        // 공통값이 같은 필드는 다중 선택에서도 시딩된다
        Assert.Equal(EnumDetectionType.PIR_SENSOR, sut.Edit.Result);
    }

    [Fact]
    public void should_normalize_blank_ai_model_to_no_change()
    {
        // Arrange — 공백만 입력 = "변경 없음"으로 정규화(빈 문자열이 모델에 들어가지 않는다)
        var row = BuildRow(100, 1200, aiModel: "yolov8n");
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Act
        sut.AiModelEdit = "   ";
        sut.ApplyButton();

        // Assert
        Assert.Null(sut.Edit.AiModel);
        Assert.Equal("yolov8n", ((IDetectionEventModel)row.Model).AiModel);
        Assert.False(row.IsEdited);
    }

    [Fact]
    public void should_trim_ai_model_when_applied()
    {
        var row = BuildRow(100, 1200, aiModel: "yolov8n");
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        sut.AiModelEdit = "  yolov8s  ";
        sut.ApplyButton();

        Assert.Equal("yolov8s", ((IDetectionEventModel)row.Model).AiModel);
        Assert.True(row.IsEdited);
    }

    [Fact]
    public void should_not_change_immutable_fields_when_row_is_not_draft()
    {
        // Arrange — 기존 행(Id>0)에서 서버 불변 필드는 편집 모델에 값이 있어도 반영되지 않는다.
        //           (type_event/created_at/action_reported는 행 VM 세터의 IsDraft 가드가 차단)
        var row = BuildRow(100, 1200);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Act
        sut.MessageType = EnumEventType.Fault;
        sut.Status = EnumTrueFalse.True;
        sut.DateTime = new DateTime(2020, 1, 1);
        sut.ApplyButton();

        // Assert — 값 불변 + 거짓 dirty도 없음
        var m = row.Model;
        Assert.Equal(EnumEventType.Intrusion, m.MessageType);
        Assert.Equal(new DateTime(2026, 8, 3, 12, 0, 0), m.DateTime);
        Assert.False(row.IsEdited);
    }

    [Fact]
    public void should_apply_result_to_grid_row_when_changed()
    {
        // Arrange — 기존 행에서 실제로 바뀌는 공통 필드는 result 뿐이다(서버 Replace 계약)
        var row = BuildRow(100, 1200);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { row });

        // Act
        sut.Result = EnumDetectionType.AI_DETECT;
        sut.ApplyButton();

        // Assert — DataGrid 항목(행 VM)에 반영 + 저장 대상 편입
        Assert.Equal(EnumDetectionType.AI_DETECT, row.Result);
        Assert.True(row.IsEdited);
    }

    [Fact]
    public void should_apply_common_fields_to_all_rows_when_multiple_selected()
    {
        // Arrange — 공통 필드는 다중 선택 전체에 적용된다(상세와 달리 대상이 모호하지 않다)
        var first = BuildRow(100, 1200);
        var second = BuildRow(101, 800);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { first, second });

        // Act
        sut.Result = EnumDetectionType.AI_DETECT;
        sut.ApplyButton();

        // Assert
        Assert.Equal(EnumDetectionType.AI_DETECT, first.Result);
        Assert.Equal(EnumDetectionType.AI_DETECT, second.Result);
        Assert.True(first.IsEdited);
        Assert.True(second.IsEdited);
    }

    [Fact]
    public void should_not_apply_detail_when_multiple_rows_selected()
    {
        // Arrange — detail은 이벤트별 값이라 다중 선택에서는 대상이 모호 → 적용하지 않는다(현행 계약 고정)
        var first = BuildRow(100, 1200);
        var second = BuildRow(101, 800);
        var sut = new DetectionSelectionViewModel(new List<DetectionEventViewModel> { first, second });

        // Act
        sut.SignalEdit = 3400;
        sut.ApplyButton();

        // Assert
        Assert.False(sut.IsDetailEditable);
        Assert.Equal(1200, ((IDetectionEventModel)first.Model).Signal);
        Assert.Equal(800, ((IDetectionEventModel)second.Model).Signal);
        Assert.False(first.IsEdited);
        Assert.False(second.IsEdited);
    }
}
#endregion

#region - PUT 실패 후 저장 검증 폴백 (서버가 커밋 후 응답 조립에서 500) -
/// <summary>
/// 서버 `PUT /events/detections/{id}` 는 UPDATE를 커밋한 뒤 응답 조립(event.device lazy-load)에서
/// 터져 500을 돌려준다 — DB에는 값이 반영됐는데 클라는 "저장 실패"로 표시한다(실측 2026-08-04).
/// 실패를 성공으로 둔갑시키지 않고 <b>실제 서버 상태를 되읽어</b> 보낸 값과 일치할 때만 성공 처리한다.
/// </summary>
public class DetectionSaveVerificationTests
{
    private static DetectionEventDto BuildDto(int id, int? signal, string result = "PIR_SENSOR", string type = "Intrusion")
        => new()
        {
            Id = id,
            CreatedAt = "2026-08-03T12:00:00",
            TypeEvent = type,
            ActionReported = "False",
            Result = result,
            Detail = new DetectionDetailDto { Signal = signal }
        };

    private static IDetectionEventModel BuildSent(int id, int? signal)
        => new DetectionEventModel
        {
            Id = id,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            DateTime = new DateTime(2026, 8, 3, 12, 0, 0),
            Signal = signal
        };

    private static (EventProviderService svc, Mock<IEventApiService> api) CreateSut(
        ApiResponse<DetectionEventDto> putResult,
        ApiResponse<DetectionEventDto> getResult)
    {
        var api = new Mock<IEventApiService>();
        api.Setup(a => a.UpdateDetectionEventAsync(It.IsAny<int>(), It.IsAny<DetectionEventReplaceDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(putResult);
        api.Setup(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(getResult);
        return (new EventProviderService(new Mock<ILogService>().Object, api.Object), api);
    }

    private static ApiResponse<DetectionEventDto> Fail()
        => new() { Success = false, Data = null };

    private static ApiResponse<DetectionEventDto> Ok(DetectionEventDto dto)
        => new() { Success = true, Data = dto };

    [Fact]
    public async Task should_return_verified_model_when_put_fails_but_value_persisted()
    {
        // Arrange — PUT은 실패(500)하지만 서버에는 우리가 보낸 값이 들어가 있다
        var (svc, api) = CreateSut(Fail(), Ok(BuildDto(100, signal: 3400)));

        // Act
        var result = await svc.UpdateDetectionEventAsync(BuildSent(100, 3400));

        // Assert — 예외 없이 확인된 모델 반환 + 되읽기 1회
        Assert.NotNull(result);
        Assert.Equal(3400, result.Signal);
        api.Verify(a => a.GetDetectionEventByIdAsync(100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task should_throw_when_put_fails_and_server_value_differs()
    {
        // Arrange — 서버 값이 보낸 값과 다르다 = 진짜 실패
        var (svc, _) = CreateSut(Fail(), Ok(BuildDto(100, signal: 1200)));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateDetectionEventAsync(BuildSent(100, 3400)));
    }

    [Fact]
    public async Task should_throw_when_put_fails_and_verification_fetch_fails()
    {
        // Arrange — 확인 조회 자체가 실패(네트워크 단절·404) → 원래 실패를 그대로 전파
        var (svc, _) = CreateSut(Fail(), Fail());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateDetectionEventAsync(BuildSent(100, 3400)));
    }

    [Fact]
    public async Task should_throw_when_put_fails_and_result_differs()
    {
        // Arrange — detail은 같지만 result가 반영되지 않았다 = 부분 실패도 실패로 본다
        var (svc, _) = CreateSut(Fail(), Ok(BuildDto(100, signal: 3400, result: "AI_DETECT")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateDetectionEventAsync(BuildSent(100, 3400)));
    }

    [Fact]
    public async Task should_not_fetch_verification_when_put_succeeds()
    {
        // Arrange — 정상 경로에서는 되읽기를 하지 않는다(서버 수정 후 이 폴백은 아예 타지 않음)
        var (svc, api) = CreateSut(Ok(BuildDto(100, signal: 3400)), Ok(BuildDto(100, signal: 3400)));

        var result = await svc.UpdateDetectionEventAsync(BuildSent(100, 3400));

        Assert.NotNull(result);
        api.Verify(a => a.GetDetectionEventByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
#endregion

#region - 저장 파이프라인: Draft 수집 소스 · 결과 통지 (FR-04/05) -
[Collection("IoC-Dependent")]
public class DetectionSavePipelineTests : IoCStubbedTestBase
{
    private readonly List<object> _published = new();
    private readonly DetectionEventPanelViewModel _panel;

    public DetectionSavePipelineTests()
    {
        var eaMock = new Mock<IEventAggregator>();
        eaMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Callback<object, Func<Func<Task>, Task>, CancellationToken>((msg, _, _) => _published.Add(msg))
              .Returns(Task.CompletedTask);

        var log = new Mock<ILogService>().Object;
        var providerService = new Mock<EventProviderService>(
            log, new Mock<IEventApiService>().Object, null, null).Object;

        _panel = new DetectionEventPanelViewModel(
            eaMock.Object, log, providerService, new DeviceProvider(), new EventProvider());
    }

    private string? LastPopupExplain()
        => _published.OfType<OpenInfoPopupMessageModel>().LastOrDefault()?.Explain;

    private static DetectionEventViewModel BuildRow(int id)
        => new(new DetectionEventModel
        {
            Id = id,
            MessageType = EnumEventType.Intrusion,
            Result = EnumDetectionType.PIR_SENSOR,
            DateTime = new DateTime(2026, 8, 3, 12, 0, 0)
        });

    [Fact]
    public void should_report_zero_changes_when_nothing_edited()
    {
        // Arrange — 편집하지 않은 기존 행만 있는 상태
        _panel.ViewModelProvider.Add(BuildRow(100));

        // Act
        _panel.OnClickSaveButton(this, new RoutedEventArgs());

        // Assert — 종전에는 무통지라 "저장 성공"과 구별할 수 없었다(FR-05)
        Assert.Equal("변경된 내용이 없습니다.", LastPopupExplain());
    }

    [Fact]
    public void should_collect_draft_from_viewmodel_provider_when_provider_not_mirrored()
    {
        // Arrange — 첫 조회 실패 등으로 EventProvider 미러링(CollectionChanged 구독)이 부착되지 않은 상태를 모사.
        //           수정 전에는 Draft 수집 소스가 _eventProvider 라서 POST가 조용히 누락됐다(DEF-03).
        //           장비 미선택 Draft는 "보류"로 집계되므로, 보류 통지가 뜨면 VP에서 수집됐다는 뜻이다.
        _panel.ViewModelProvider.Add(BuildRow(0));

        // Act
        _panel.OnClickSaveButton(this, new RoutedEventArgs());

        // Assert
        var explain = LastPopupExplain();
        Assert.NotNull(explain);
        Assert.Contains("보류", explain!);
        Assert.DoesNotContain("변경된 내용이 없습니다.", explain!);
    }
}
#endregion

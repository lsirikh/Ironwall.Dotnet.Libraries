using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 불변식 — 상세에 적용하지 않은 판정이 있으면 무엇이 오든(다른 행 선택 · 목록 다시 읽기 · 다른 목록의 바쁨 끝) 고친 칸이 말없이 사라지지 않는다.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 r9 SC-EVT-028 — 사유를 고친 뒤 다른 행을 고르자 원래 행도 새 행도 선택이 아니고('선택 0건') [되돌리기] 가 꺼졌다(고친 칸 유실).
/// 헤드리스로 확인한 길: 장애 목록이 다시 읽히는 도중(옛 행이 빠지고 새 행이 아직 안 들어온 사이) <b>다른 목록(탐지)의 바쁨 끝</b>이 오면
/// '다시 읽기가 끝났다 — 짝이 없는 선택은 삭제된 것' 으로 읽어 관문을 건너뛴 선택 비우기(SetSelection([]))로 상세를 다시 세웠다.
/// 탐지 레일에서 [적용] 한 저장은 레일을 옮긴 뒤에 끝난다(앱 로그 22:28:46 'A task was canceled … OnClickSaveButton').
/// </remarks>
[Collection("IoC-Dependent")]
public class EventDetailEditSurvivalTests : IDisposable
{
    private readonly EventDashboardViewModel _console;

    public EventDetailEditSurvivalTests()
    {
        var events = new EventProvider();
        var devices = new DeviceProvider();
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = BuildApi();
        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("tester");

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(EventProvider) ? events
            : type == typeof(DeviceProvider) ? devices
            : type == typeof(IAccountModel) ? account.Object
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();

        var providerService = new EventProviderService(log, api, devices, events);
        _console = new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, devices, events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, devices, events),
            new ConnectionEventPanelViewModel(ea, log, providerService, devices, events),
            new ActionEventPanelViewModel(ea, log, providerService, events),
            new EventInfoViewModel(devices, events, providerService, ea, log),
            new CameraEventInfoViewModel(events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService));
        _console.UseUiThread(ImmediateUiThread.Instance);
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private (MalfunctionEventViewModel First, MalfunctionEventViewModel Second) SeedTwoMalfunctions()
    {
        var first = new MalfunctionEventViewModel(TestEvents.Malfunction(701));
        var second = new MalfunctionEventViewModel(TestEvents.Malfunction(702));
        _console.MalfunctionPanelViewModel.ViewModelProvider.Add(first);
        _console.MalfunctionPanelViewModel.ViewModelProvider.Add(second);
        return (first, second);
    }

    private static void Busy(Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components.BaseDataGridMultiPanelViewModel<DetectionEventViewModel> panel)
    {
        panel.ReloadButtonEnable = true;      // 목록을 한 번 읽은 패널(헤디드와 같다) — 안 읽은 패널은 늘 '바쁨' 이라 끝남이 오지 않는다
        panel.IsSaving = true;
        panel.IsSaving = false;
    }

    [Fact]
    public async Task should_keep_the_unapplied_edit_when_another_list_finishes_while_this_list_is_mid_reload()
    {
        await ((IActivate)_console).ActivateAsync();
        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        var (first, _) = SeedTwoMalfunctions();
        Assert.True(_console.SetSelection(new object[] { first }));
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldReason, "제어기 장애", "펜스 장애");
        Assert.True(_console.Detail.CanRevert);

        // 다시 읽기 도중 — 옛 행이 빠졌고 새 행은 아직
        _console.MalfunctionPanelViewModel.ViewModelProvider.Remove(first);
        // 그 사이 탐지 목록의 저장이 끝났다(다른 목록의 바쁨 끝)
        Busy(_console.DetectionPanelViewModel);

        Assert.True(_console.Detail.CanRevert, $"다른 목록의 바쁨 끝이 고친 칸을 지우면 안 된다 · 상태={_console.Detail.State}");
        Assert.Equal(1, _console.SelectedRows.Count);

        // 새 행(같은 이벤트)이 들어오면 그것을 가리킨다 — 고친 칸은 그대로
        var twin = new MalfunctionEventViewModel(TestEvents.Malfunction(701));
        _console.MalfunctionPanelViewModel.ViewModelProvider.Add(twin);
        Assert.Same(twin, Assert.Single(_console.SelectedRows));
        Assert.True(_console.Detail.CanRevert);
    }

    [Fact]
    public async Task should_keep_the_unapplied_edit_when_this_list_finishes_and_the_edited_event_is_gone()
    {
        await ((IActivate)_console).ActivateAsync();
        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        var (first, _) = SeedTwoMalfunctions();
        Assert.True(_console.SetSelection(new object[] { first }));
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldReason, "제어기 장애", "펜스 장애");

        _console.MalfunctionPanelViewModel.ViewModelProvider.Remove(first);
        _console.MalfunctionPanelViewModel.IsSaving = true;       // 이 목록의 바쁨 끝 — 짝이 정말 없다
        _console.MalfunctionPanelViewModel.IsSaving = false;

        // 고친 칸이 있으면 말없이 버리지 않는다 — 운영자가 [적용] · [되돌리기] 로 끝낸다
        Assert.True(_console.Detail.CanRevert, $"고친 칸이 말없이 사라졌다 · 상태={_console.Detail.State}");
        Assert.Equal(1, _console.SelectedRows.Count);
    }

    [Fact]
    public async Task should_drop_a_vanished_row_when_this_list_finishes_and_nothing_was_edited()
    {
        await ((IActivate)_console).ActivateAsync();
        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        var (first, _) = SeedTwoMalfunctions();
        Assert.True(_console.SetSelection(new object[] { first }));

        _console.MalfunctionPanelViewModel.ViewModelProvider.Remove(first);
        Busy(_console.DetectionPanelViewModel);                   // 다른 목록의 끝 — 이 목록의 다시 읽기와 무관
        Assert.Equal(1, _console.SelectedRows.Count);

        _console.MalfunctionPanelViewModel.IsSaving = true;       // 이 목록의 바쁨 끝 — 정말 지워진 행은 뺀다
        _console.MalfunctionPanelViewModel.IsSaving = false;
        Assert.Empty(_console.SelectedRows);
    }

    [Fact]
    public async Task should_refuse_another_row_even_under_a_suppressed_guard_when_the_detail_has_unapplied_edits()
    {
        await ((IActivate)_console).ActivateAsync();
        await _console.SelectRailAsync(EventDashboardViewModel.MalfunctionRailKey);
        var (first, second) = SeedTwoMalfunctions();
        Assert.True(_console.SetSelection(new object[] { first }));
        _console.Detail.Tracker.Touch(EventDetailProjection.FieldReason, "제어기 장애", "펜스 장애");

        using (_console.SuppressSelectionGuard())
        {
            Assert.True(_console.SetSelection(new object[] { first }));      // 되돌리기(같은 선택)는 된다
            Assert.False(_console.SetSelection(new object[] { second }));    // 다른 이벤트는 관문을 건너뛰어도 안 된다
            Assert.False(_console.SetSelection(Array.Empty<object>()));
        }

        Assert.Same(first, Assert.Single(_console.SelectedRows));
        Assert.True(_console.Detail.CanRevert);
    }

    private static IEventApiService BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                  It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<DetectionEventDto>());
        mock.Setup(a => a.GetMalfunctionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<MalfunctionEventDto>());
        mock.Setup(a => a.GetConnectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                                                   It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<ConnectionEventDto>());
        mock.Setup(a => a.GetActionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                                               It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Empty<ActionEventDto>());
        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto> { Success = true, Data = new EventDashboardDto() });
        return mock.Object;

        static ApiListResponse<T> Empty<T>()
            => new() { Success = true, Data = new List<T>(), Pagination = new PaginationDto { Page = 1, Limit = 100, Total = 0, TotalPages = 1 } };
    }
}

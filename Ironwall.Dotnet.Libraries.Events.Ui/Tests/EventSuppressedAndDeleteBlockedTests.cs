using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : G40 억제 202 · G41 삭제 409 사유 — 가짜 응답(Mock IEventApiService)으로 판정.
                  · 202+suppressed → 실패로 세지 않고 Draft 를 치우고 "정비 창(schedule_id N)…" 을 보인다
                  · 409(조치보고 남음) → "조치보고를 먼저 지워야 삭제됩니다(Id …)" 를 따로 적는다. 재시도형 409 는 일반 실패
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]
public class EventSuppressedAndDeleteBlockedTests : IoCStubbedTestBase
{
    private readonly List<object> _published = new();
    private readonly Mock<IEventApiService> _api = new();

    private Mock<IEventAggregator> Ea()
    {
        var ea = new Mock<IEventAggregator>();
        ea.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((msg, _, _) => _published.Add(msg))
          .Returns(Task.CompletedTask);
        return ea;
    }

    private EventProviderService Provider() => new(new Mock<ILogService>().Object, _api.Object);

    private static ApiResponse<T> Suppressed<T>(int? scheduleId) => new()
    {
        Success = true, StatusCode = 202, Suppressed = true, ScheduleId = scheduleId, Message = "suppressed by schedule",
    };

    private static ApiResponse<bool> Conflict(JToken? details = null) => new()
    {
        Success = false, StatusCode = 409,
        Error = new ApiError { Code = "CONFLICT", Message = "action reports exist", DetailsToken = details },
    };

    private static IBaseDeviceModel Device(int id)
    {
        var d = new Mock<IBaseDeviceModel>();
        d.SetupGet(x => x.Id).Returns(id);
        return d.Object;
    }

    private string? LastPopup() => _published.OfType<OpenInfoPopupMessageModel>().LastOrDefault()?.Explain;

    // ── G40 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task 억제_202_는_세_종류_생성_모두_억제됨_결과형으로_돌아온다()
    {
        _api.Setup(a => a.CreateDetectionEventAsync(It.IsAny<DetectionEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Suppressed<DetectionEventDto>(7));
        _api.Setup(a => a.CreateMalfunctionEventAsync(It.IsAny<MalfunctionEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Suppressed<MalfunctionEventDto>(8));
        _api.Setup(a => a.CreateConnectionEventAsync(It.IsAny<ConnectionEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Suppressed<ConnectionEventDto>(null));
        var provider = Provider();

        var d = await Assert.ThrowsAsync<EventSuppressedException>(() => provider.InsertDetectionEventAsync(new DetectionEventModel { Device = Device(5) }));
        var m = await Assert.ThrowsAsync<EventSuppressedException>(() => provider.InsertMalfunctionEventAsync(new MalfunctionEventModel { Device = Device(5) }));
        var c = await Assert.ThrowsAsync<EventSuppressedException>(() => provider.InsertConnectionEventAsync(new ConnectionEventModel { Device = Device(5) }));

        Assert.Equal(7, d.ScheduleId);
        Assert.Equal(8, m.ScheduleId);
        Assert.Null(c.ScheduleId);
    }

    [Fact]
    public void 탐지_패널_저장_억제_202_는_실패로_세지_않고_Draft_를_치운다()
    {
        _api.Setup(a => a.CreateDetectionEventAsync(It.IsAny<DetectionEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Suppressed<DetectionEventDto>(7));
        var panel = new DetectionEventPanelViewModel(Ea().Object, new Mock<ILogService>().Object, Provider(), new DeviceProvider(), new EventProvider());
        panel.ViewModelProvider.Add(new DetectionEventViewModel(new DetectionEventModel { Id = 0, MessageType = EnumEventType.Intrusion, Device = Device(5) }));

        panel.OnClickSaveButton(this, new RoutedEventArgs());

        var popup = LastPopup();
        Assert.Equal("정비 창(schedule_id 7)에 걸려 저장되지 않았습니다.", popup);
        Assert.DoesNotContain("저장하지 못했습니다", popup!);
        Assert.Empty(panel.ViewModelProvider);   // Draft 정리 — 다음 저장에 다시 보내지 않는다
    }

    [Fact]
    public void 장애_패널_저장_억제_202_는_실패_통지_대신_정비_창_안내()
    {
        _api.Setup(a => a.CreateMalfunctionEventAsync(It.IsAny<MalfunctionEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Suppressed<MalfunctionEventDto>(3));
        var panel = new MalfunctionEventPanelViewModel(Ea().Object, new Mock<ILogService>().Object, Provider(), new DeviceProvider(), new EventProvider());
        panel.ViewModelProvider.Add(new MalfunctionEventViewModel(new MalfunctionEventModel { Id = 0, Device = Device(5) }));

        panel.OnClickSaveButton(this, new RoutedEventArgs());

        Assert.Equal("정비 창(schedule_id 3)에 걸려 저장되지 않았습니다.", LastPopup());
        Assert.Empty(panel.ViewModelProvider);
    }

    [Fact]
    public void 연결_패널_저장_억제_202_는_실패_통지_대신_정비_창_안내()
    {
        _api.Setup(a => a.CreateConnectionEventAsync(It.IsAny<ConnectionEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Suppressed<ConnectionEventDto>(4));
        var panel = new ConnectionEventPanelViewModel(Ea().Object, new Mock<ILogService>().Object, Provider(), new DeviceProvider(), new EventProvider());
        panel.ViewModelProvider.Add(new ConnectionEventViewModel(new ConnectionEventModel { Id = 0, Device = Device(5) }));

        panel.OnClickSaveButton(this, new RoutedEventArgs());

        Assert.Equal("정비 창(schedule_id 4)에 걸려 저장되지 않았습니다.", LastPopup());
        Assert.Empty(panel.ViewModelProvider);
    }

    [Fact]
    public void 억제_요약_문구()
    {
        Assert.Null(EventSuppressedException.Summarize(Array.Empty<EventSuppressedException>()));
        Assert.Equal("정비 창에 걸려 저장되지 않았습니다.", EventSuppressedException.Summarize(new[] { new EventSuppressedException(null, null) }));
        Assert.Equal("2건이 정비 창(schedule_id 3, 5)에 걸려 저장되지 않았습니다.",
            EventSuppressedException.Summarize(new[] { new EventSuppressedException(3, null), new EventSuppressedException(5, null) }));
    }

    // ── G41 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task 삭제_409_는_거절_예외로_구분된다_재시도형_409_는_일반_실패()
    {
        _api.Setup(a => a.DeleteDetectionEventAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Conflict());
        _api.Setup(a => a.DeleteDetectionEventAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Conflict(new JObject { ["retryable"] = true }));
        _api.Setup(a => a.DeleteMalfunctionEventAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Conflict());
        _api.Setup(a => a.DeleteConnectionEventAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Conflict());
        var provider = Provider();

        await Assert.ThrowsAsync<DeleteBlockedException>(() => provider.DeleteDetectionEventAsync(1));
        await Assert.ThrowsAsync<DeleteBlockedException>(() => provider.DeleteMalfunctionEventAsync(1));
        await Assert.ThrowsAsync<DeleteBlockedException>(() => provider.DeleteConnectionEventAsync(1));
        Assert.False(await provider.DeleteDetectionEventAsync(2));   // 동시성 409 — 다시 보내면 되는 실패
    }

    [Fact]
    public async Task 탐지_패널_삭제_409_는_조치보고_사유를_따로_적는다()
    {
        _api.Setup(a => a.DeleteDetectionEventAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(Conflict());
        _api.Setup(a => a.DeleteDetectionEventAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResponse<bool> { Success = false, StatusCode = 500 });
        _api.Setup(a => a.DeleteDetectionEventAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResponse<bool> { Success = true, StatusCode = 200 });
        var ea = Ea();
        var panel = new DetectionEventPanelViewModel(ea.Object, new Mock<ILogService>().Object, Provider(), new DeviceProvider(), new EventProvider());
        var rows = new[] { 11, 12, 13 }.Select(id => new DetectionEventViewModel(new DetectionEventModel { Id = id })).ToList();
        SetPendingDelete(panel, rows);

        await panel.HandleAsync(new CallDeleteDetectionEventProcessMessageModel(), CancellationToken.None);

        var popup = _published.OfType<OpenInfoPopupMessageModel>().Single(p => p.Title == "삭제 일부 실패").Explain;
        Assert.Contains("1건 삭제에 실패했습니다", popup);
        Assert.Contains("(Id: 12)", popup);
        Assert.Contains("조치보고가 있는 이벤트는 조치보고를 먼저 지워야 삭제됩니다(Id 11).", popup);
    }

    [Fact]
    public void 삭제_확인_문구는_조치보고를_함께_지운다고_하지_않는다()
    {
        foreach (var kind in new[] { "Detection", "Malfunction", "Connection" })
        {
            var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Events.Ui", "ViewModels", "Panels", $"{kind}EventPanelViewModel.cs");
            var src = File.ReadAllText(path);
            Assert.DoesNotContain("조치보고도 함께 삭제", src);
            Assert.Contains("조치보고를 먼저 지우세요", src);
        }
    }

    private static void SetPendingDelete(DetectionEventPanelViewModel panel, List<DetectionEventViewModel> rows)
    {
        var field = typeof(DetectionEventPanelViewModel).GetField("_pendingDeleteItems",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(panel, rows);   // IList<DetectionEventViewModel>

    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
}

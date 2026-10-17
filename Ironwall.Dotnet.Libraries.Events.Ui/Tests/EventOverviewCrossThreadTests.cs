using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 실창(GIS 호스트) 결함 D2 — 이벤트 콘솔을 열면 "[EventConsole] 개요 갱신 실패: 이 형식의 CollectionView 에서는
/// 발송자 스레드와 다른 스레드에서의 해당 SourceCollection 에 대한 변경 내용을 지원하지 않습니다" 가 작업 스레드 [11] 에서
/// 찍히고 개요(조각 · 막대)가 갱신되지 않았다(log-2026-09-27.txt:779).
/// </summary>
/// <remarks>
/// <para>기존 콘솔 시험은 <see cref="ImmediateUiThread"/> 로 스레드 전환을 없애고, 개요 컬렉션에 WPF 뷰를 물리지 않아
/// 이 경로가 터질 조건이 한 번도 만들어지지 않았다.</para>
/// <para>여기서는 실제와 같게 만든다 — ① 콘솔을 STA 디스패처 스레드에서 세우고 ② 개요의 조각 · 막대 컬렉션에
/// 그 스레드에서 <see cref="CollectionViewSource.GetDefaultView"/> 로 뷰를 물린 뒤(뷰의 ItemsControl 이 하는 일)
/// ③ 활성화한다. 활성화가 차트 패널을 열고, 차트 패널은 <c>Task.Run</c> 의 finally 에서 <c>UpdateAction</c> 을
/// <b>작업 스레드에서</b> 울린다 — 제품과 같은 발화 경로다.</para>
/// </remarks>
[Collection("IoC-Dependent")]
public sealed class EventOverviewCrossThreadTests : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Thread _sta;

    public EventOverviewCrossThreadTests()
    {
        using var ready = new ManualResetEventSlim(false);
        Dispatcher? dispatcher = null;
        _sta = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        _sta.SetApartmentState(ApartmentState.STA);
        _sta.IsBackground = true;
        _sta.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "STA 디스패처가 뜨지 않았다");
        _dispatcher = dispatcher!;
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
        _dispatcher.InvokeShutdown();
        _sta.Join(TimeSpan.FromSeconds(10));
    }

    /// <summary>제품의 <see cref="ApplicationUiThread"/> 와 같은 규칙을 이 시험의 STA 디스패처로 — Application 이 없어서 대신 세운다.</summary>
    private sealed class DispatcherUiThread : IUiThread
    {
        private readonly Dispatcher _dispatcher;
        public DispatcherUiThread(Dispatcher dispatcher) => _dispatcher = dispatcher;
        public bool IsOnUiThread => _dispatcher.CheckAccess();
        public void Post(System.Action action) => _dispatcher.BeginInvoke(action);
    }

    [Fact]
    public async Task should_refresh_the_overview_on_the_ui_thread_when_the_dashboard_update_arrives_from_a_worker_thread()
    {
        // Arrange
        var errors = new ConcurrentQueue<string>();
        var log = new Mock<ILogService>();
        log.Setup(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Callback<string, string, string, int>((msg, _, _, _) => errors.Enqueue(msg));

        var changeThreads = new ConcurrentQueue<int>();
        var staThreadId = _sta.ManagedThreadId;
        var resets = 0;
        EventDashboardViewModel? console = null;
        var keepAlive = new List<ICollectionView>();

        await _dispatcher.InvokeAsync(() =>
        {
            console = BuildConsole(log.Object);
            console.UseUiThread(new DispatcherUiThread(_dispatcher));

            // 뷰가 하는 일 — 조각 · 막대 목록에 이 스레드의 컬렉션 뷰를 물린다.
            keepAlive.Add(CollectionViewSource.GetDefaultView(console.Overview.Slices));
            keepAlive.Add(CollectionViewSource.GetDefaultView(console.Overview.Bars));

            console.Overview.Slices.CollectionChanged += (_, e) =>
            {
                changeThreads.Enqueue(Environment.CurrentManagedThreadId);
                if (e.Action == NotifyCollectionChangedAction.Reset) Interlocked.Increment(ref resets);
            };
        }).Task.WaitAsync(TimeSpan.FromSeconds(20));

        // Act — 활성화: 차트 패널이 통계를 읽고 작업 스레드에서 UpdateAction 을 울린다.
        await _dispatcher.InvokeAsync(() => ((IActivate)console!).ActivateAsync()).Task.Unwrap()
                         .WaitAsync(TimeSpan.FromSeconds(20));
        // 작업 스레드가 UI 스레드로 올려 둔 갱신까지 모두 돈다.
        await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task.WaitAsync(TimeSpan.FromSeconds(20));

        // Assert
        Assert.DoesNotContain(errors, m => m.Contains("개요 갱신 실패", StringComparison.Ordinal));
        Assert.All(changeThreads, id => Assert.Equal(staThreadId, id));
        // 레일 전환이 한 번, 작업 스레드에서 온 통계 도착이 한 번 — 둘 다 개요를 다시 채운다.
        Assert.True(resets >= 2, $"개요가 통계 도착으로 다시 채워지지 않았다(Reset {resets}회)");

        await _dispatcher.InvokeAsync(() => ((IDeactivate)console!).DeactivateAsync(true)).Task.Unwrap()
                         .WaitAsync(TimeSpan.FromSeconds(20));
        GC.KeepAlive(keepAlive);
    }

    private static EventDashboardViewModel BuildConsole(ILogService log)
    {
        var ea = new EventAggregator();
        var events = new EventProvider();
        var devices = new DeviceProvider();
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
        return new EventDashboardViewModel(
            ea, log,
            new EventTabControlViewModel(ea, log),
            new DetectionEventPanelViewModel(ea, log, providerService, devices, events),
            new MalfunctionEventPanelViewModel(ea, log, providerService, devices, events),
            new ConnectionEventPanelViewModel(ea, log, providerService, devices, events),
            new ActionEventPanelViewModel(ea, log, providerService, events),
            new EventInfoViewModel(devices, events, providerService, ea, log),
            new CameraEventInfoViewModel(events, ea, log),
            new DataChartPanelViewModel(ea, log, providerService));
    }

    private static IEventApiService BuildApi()
    {
        var mock = new Mock<IEventApiService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        mock.Setup(a => a.GetEventStatisticsDashboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventDashboardDto>
            {
                Success = true,
                Data = new EventDashboardDto { Summary = new EventSummaryDto { SensorDetection = 3, Malfunction = 1 } },
            });
        return mock.Object;
    }
}

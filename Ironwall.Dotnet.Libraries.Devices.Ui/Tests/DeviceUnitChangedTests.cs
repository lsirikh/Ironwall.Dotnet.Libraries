using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-30 — DeviceUnitChangedMessage → DeviceProvider 장비 모델의 UnitId 만 고친다(FR-49)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-M031. 적재 경로(VER-12)는 이미 UnitId 를 채운다 — 이 시험은 "콘솔이 바꾼 뒤 다음 적재 전까지" 의 틈만 본다.
                  컬렉션을 다시 만들지 않는다(메모리 symbolprovider_stalecache_markers_hardcast — 지도 심볼이 같은 인스턴스를 붙잡고 있다).
****************************************************************************/
public class DeviceUnitChangedTests
{
    private static (DeviceProvider Provider, CameraDeviceModel Camera, SensorDeviceModel Sensor) Seed(IEventAggregator? events = null, Dispatcher? ui = null)
    {
        var provider = events is null ? new DeviceProvider() : new DeviceProvider(events, ui);
        var camera = new CameraDeviceModel { Id = 379, UnitId = 3 };
        var sensor = new SensorDeviceModel { Id = 349, UnitId = 3 };
        provider.Add(camera);
        provider.Add(sensor);
        return (provider, camera, sensor);
    }

    [Fact]
    public void should_change_only_that_device_unit_id_in_place()
    {
        var (provider, camera, sensor) = Seed();
        var before = provider.CollectionEntity.ToList();

        var changed = provider.ApplyUnitChange(379, 27);

        Assert.Equal(1, changed);
        Assert.Equal(27, camera.UnitId);
        Assert.Equal(3, sensor.UnitId);                                         // 다른 장비는 그대로
        Assert.Equal(before, provider.CollectionEntity.ToList());              // 컬렉션 재구성 0 — 같은 인스턴스 · 같은 순서
        Assert.Same(camera, provider.CollectionEntity.First(d => d.Id == 379));
    }

    [Fact]
    public void should_ignore_unknown_devices_and_invalid_ids()
    {
        var (provider, camera, _) = Seed();

        Assert.Equal(0, provider.ApplyUnitChange(99_999, 27));
        Assert.Equal(0, provider.ApplyUnitChange(379, 0));
        Assert.Equal(3, camera.UnitId);
    }

    [Fact]
    public void should_not_count_a_change_when_the_unit_is_already_the_same()
    {
        var (provider, _, _) = Seed();

        Assert.Equal(0, provider.ApplyUnitChange(379, 3));
    }

    [Fact]
    public async Task should_apply_on_the_ui_thread_when_the_message_is_published_from_a_worker_thread()
    {
        // Arrange — "UI 스레드" 역할의 디스패처(STA)를 하나 띄운다
        using var ui = new DispatcherThread();
        var events = new EventAggregator();
        var (provider, camera, _) = Seed(events, ui.Dispatcher);
        var appliedOn = new List<int>();
        provider.DeviceUnitChanged += (_, device) => appliedOn.Add(Environment.CurrentManagedThreadId);

        // Act — NATS 콜백 · 작업 스레드에서 발행
        await Task.Run(() => events.PublishOnCurrentThreadAsync(new DeviceUnitChangedMessage(379, 27)));
        await ui.Dispatcher.InvokeAsync(() => { });                            // 디스패처 대기열을 비운다

        // Assert
        Assert.Equal(27, camera.UnitId);
        Assert.Equal(new[] { ui.ThreadId }, appliedOn);
    }

    [Fact]
    public async Task should_not_listen_when_constructed_without_an_event_aggregator()
    {
        var events = new EventAggregator();
        var (_, camera, _) = Seed();                                           // 옛 생성자 — 구독 없음

        await events.PublishOnCurrentThreadAsync(new DeviceUnitChangedMessage(379, 27));

        Assert.Equal(3, camera.UnitId);
    }

    [Fact]
    public async Task should_subscribe_the_container_singleton_when_the_host_registers_an_event_aggregator()
    {
        // 분기 배선 확인(메모리 branch_wiring_must_be_verified) — 호스트 부트스트래퍼처럼 EventAggregator 를 등록하면
        // Autofac 은 구독하는 생성자를 고른다. 이게 빠지면 처리기가 있어도 메시지가 영영 닿지 않는다.
        var builder = new ContainerBuilder();
        builder.RegisterType<EventAggregator>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterModule(new Ironwall.Dotnet.Libraries.Devices.Modules.DeviceModule());
        using var container = builder.Build();
        var provider = container.Resolve<DeviceProvider>();
        var events = container.Resolve<IEventAggregator>();
        var camera = new CameraDeviceModel { Id = 379, UnitId = 3 };
        provider.Add(camera);

        await events.PublishOnCurrentThreadAsync(new DeviceUnitChangedMessage(379, 27));

        Assert.Equal(27, camera.UnitId);
    }

    /// <summary>시험 전용 UI 스레드 — STA 에서 Dispatcher.Run.</summary>
    private sealed class DispatcherThread : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _ready = new();

        public DispatcherThread()
        {
            _thread = new Thread(() =>
            {
                Dispatcher = Dispatcher.CurrentDispatcher;
                ThreadId = Environment.CurrentManagedThreadId;
                _ready.Set();
                Dispatcher.Run();
            }) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _ready.Wait();
        }

        public Dispatcher Dispatcher { get; private set; } = null!;
        public int ThreadId { get; private set; }

        public void Dispose()
        {
            Dispatcher.InvokeShutdown();
            _thread.Join(TimeSpan.FromSeconds(5));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : 지도 카메라 더블클릭 — 팝업 방식 분기(자체 · 브로커 · 사용 안 함) 헤드리스
                  (camera-popup-modes T-07 · FR-01~03 · FR-05/06 · FR-27/28)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 옛 <c>IsCameraPopupUsed</c> 게이트 자리 — 설정 모드로 길을 고르는지, 사용 안 함은 영상 대신 상세를 여는지,
/// 브로커는 기다리지 않고 요청을 떠나보내는지, 그리고 <b>지도 뷰모델이 실제로 이 분기기에 이어져 있는지</b>(배선) 본다.
/// </summary>
public class CameraPopupDoubleClickDispatcherTests
{
    private const string ClientId = "gis-7f3a9c2e41b6";

    private sealed class FakeOverlaySettings : ICameraPopupOverlaySettings
    {
        public CameraPopupSettings Value = new();
        public string Id = CameraPopupDoubleClickDispatcherTests.ClientId;
        public bool IsCameraPopupUsed => Value.Mode != CameraPopupMode.None;
        public CameraPopupSettings Settings => Value;
        public string ClientId => Id;
    }

    private sealed class FakeBroker : ICameraPopupBrokerService
    {
        public readonly List<CameraPopupOpenRequest> Requests = new();
        public Task<CameraPopupBrokerOutcome>? Hold;

        public string BuildSubject() => "sensorway.unit999.nvr_manager.popup";
        public bool IsPending(int cameraId) => false;

        public Task<CameraPopupBrokerOutcome> RequestOpenAsync(CameraPopupOpenRequest request, Action<CameraPopupBrokerNotice>? notify = null,
                                                               CancellationToken ct = default)
        {
            Requests.Add(request);
            notify?.Invoke(new CameraPopupBrokerNotice(CameraPopupBrokerToasts.Requested(request.CameraName), true));
            return Hold ?? Task.FromResult(new CameraPopupBrokerOutcome(CameraPopupBrokerOutcomeKind.Opened, "모니터 1 · 칸 1에 띄웠습니다", "p"));
        }

        public Task<NvrPopupLayoutResult> GetLayoutAsync(string clientId, int timeoutSeconds, CancellationToken ct = default)
            => Task.FromResult(NvrPopupLayoutResult.Unavailable);
    }

    private sealed class Harness
    {
        public FakeOverlaySettings? Settings = new();
        public ICameraPopupBrokerService? Broker = new FakeBroker();
        public readonly List<(IPidsEditableMarker Marker, ICameraDeviceModel Camera, CameraPopupSettings Settings)> SelfOpened = new();
        public readonly List<IPidsEditableMarker> DetailOpened = new();
        public readonly List<CameraPopupBrokerNotice> Toasts = new();
        public bool ThrowOnSettings;

        public CameraPopupDoubleClickDispatcher Build() => new(
            () => ThrowOnSettings ? throw new InvalidOperationException("settings gone") : Settings,
            (m, c, s) => SelfOpened.Add((m, c, s)),
            m => DetailOpened.Add(m),
            () => Broker,
            () => "operator1",
            n => { lock (Toasts) Toasts.Add(n); });
    }

    private static IPidsEditableMarker CameraMarker(int id = 101, string title = "정문 PTZ", EnumDeviceType type = EnumDeviceType.IpCamera,
                                                    bool withCamera = true)
    {
        var camera = new Mock<ICameraDeviceModel>();
        camera.SetupGet(c => c.Id).Returns(id);
        camera.SetupGet(c => c.DeviceName).Returns("카메라-" + id);
        var marker = new Mock<IPidsEditableMarker>();
        marker.SetupGet(m => m.DeviceType).Returns(type);
        marker.SetupGet(m => m.Title).Returns(title);
        marker.SetupGet(m => m.LinkedDeviceId).Returns(id);
        marker.SetupGet(m => m.LinkedDevice).Returns(withCamera ? camera.Object : null);
        return marker.Object;
    }

    // ══════ 모드 → 길(순수) ══════

    [Theory]
    [InlineData(CameraPopupMode.Self, CameraPopupDoubleClickRoute.SelfOverlay)]
    [InlineData(CameraPopupMode.Broker, CameraPopupDoubleClickRoute.BrokerRequest)]
    [InlineData(CameraPopupMode.None, CameraPopupDoubleClickRoute.ShowDetail)]
    public void should_route_by_popup_mode_when_camera_double_clicked(CameraPopupMode mode, CameraPopupDoubleClickRoute expected)
        => Assert.Equal(expected, CameraPopupDoubleClickDispatcher.RouteFor(mode));

    // ══════ 자체(FR-04 그대로) ══════

    [Fact]
    public void should_open_self_overlay_when_mode_is_self()
    {
        var h = new Harness { Settings = new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.Self } } };
        var marker = CameraMarker();

        var route = h.Build().Handle(marker);

        Assert.Equal(CameraPopupDoubleClickRoute.SelfOverlay, route);
        var opened = Assert.Single(h.SelfOpened);
        Assert.Same(marker, opened.Marker);
        Assert.Equal(101, opened.Camera.Id);
        Assert.Empty(h.DetailOpened);
        Assert.Empty(((FakeBroker)h.Broker!).Requests);
    }

    [Fact]
    public void should_default_to_self_when_settings_not_registered()
    {
        var h = new Harness { Settings = null };

        var route = h.Build().Handle(CameraMarker());

        Assert.Equal(CameraPopupDoubleClickRoute.SelfOverlay, route);
        Assert.Single(h.SelfOpened);
    }

    [Fact]
    public void should_fall_back_to_self_without_throwing_when_settings_read_fails()
    {
        var h = new Harness { ThrowOnSettings = true };

        var route = h.Build().Handle(CameraMarker());

        Assert.Equal(CameraPopupDoubleClickRoute.SelfOverlay, route);
    }

    [Fact]
    public void should_ignore_when_self_mode_camera_has_no_model()
    {
        var h = new Harness();

        var route = h.Build().Handle(CameraMarker(withCamera: false));

        Assert.Equal(CameraPopupDoubleClickRoute.Ignored, route);
        Assert.Empty(h.SelfOpened);
    }

    [Fact]
    public void should_ignore_when_marker_is_not_camera()
    {
        var h = new Harness();

        var route = h.Build().Handle(CameraMarker(type: EnumDeviceType.Controller));

        Assert.Equal(CameraPopupDoubleClickRoute.Ignored, route);
        Assert.Empty(h.SelfOpened);
        Assert.Empty(h.DetailOpened);
    }

    // ══════ 사용 안 함(FR-03) ══════

    [Fact]
    public void should_open_camera_detail_instead_of_video_when_mode_is_none()
    {
        var h = new Harness { Settings = new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.None } } };
        var marker = CameraMarker();

        var route = h.Build().Handle(marker);

        Assert.Equal(CameraPopupDoubleClickRoute.ShowDetail, route);
        Assert.Same(marker, Assert.Single(h.DetailOpened));
        Assert.Empty(h.SelfOpened);
        Assert.Empty(((FakeBroker)h.Broker!).Requests);
        Assert.Empty(h.Toasts);
    }

    // ══════ 브로커(FR-05/06) ══════

    [Fact]
    public async Task should_send_open_request_with_settings_and_station_when_mode_is_broker()
    {
        // Arrange
        var h = new Harness
        {
            Settings = new FakeOverlaySettings
            {
                Value = new CameraPopupSettings
                {
                    Mode = CameraPopupMode.Broker, BrokerMonitor = 3, BrokerCell = 7,
                    BrokerOnOccupied = CameraPopupOnOccupied.Reject, BrokerResponseTimeoutSeconds = 9,
                },
            },
        };
        var dispatcher = h.Build();

        // Act
        var route = dispatcher.Handle(CameraMarker(id: 55, title: "북측 PTZ"));
        var outcome = await dispatcher.LastBrokerTask;

        // Assert
        Assert.Equal(CameraPopupDoubleClickRoute.BrokerRequest, route);
        Assert.Equal(CameraPopupBrokerOutcomeKind.Opened, outcome?.Kind);
        var request = Assert.Single(((FakeBroker)h.Broker!).Requests);
        Assert.Equal(new CameraPopupOpenRequest(55, "북측 PTZ", ClientId, 3, 7, CameraPopupOnOccupied.Reject, "operator1", 9), request);
        Assert.Equal("NVR 관제석에 북측 PTZ 팝업을 요청했습니다", h.Toasts.First().Text);
        Assert.Empty(h.SelfOpened);                                     // 브로커 모드는 GIS 가 영상을 띄우지 않는다
        Assert.Empty(h.DetailOpened);
    }

    [Fact]
    public void should_return_immediately_when_broker_request_is_pending()
    {
        // FR-28 — 응답을 기다리는 동안에도 더블클릭 처리(UI 스레드)는 바로 돌아온다
        var broker = new FakeBroker { Hold = new TaskCompletionSource<CameraPopupBrokerOutcome>().Task };
        var h = new Harness { Settings = new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.Broker } }, Broker = broker };
        var dispatcher = h.Build();

        var route = dispatcher.Handle(CameraMarker());

        Assert.Equal(CameraPopupDoubleClickRoute.BrokerRequest, route);
        Assert.False(dispatcher.LastBrokerTask.IsCompleted);
        Assert.Single(broker.Requests);
    }

    [Fact]
    public void should_toast_when_broker_service_missing()
    {
        var h = new Harness { Settings = new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.Broker } }, Broker = null };

        var route = h.Build().Handle(CameraMarker());

        Assert.Equal(CameraPopupDoubleClickRoute.BrokerRequest, route);
        Assert.Equal(CameraPopupBrokerToasts.NoBroker, Assert.Single(h.Toasts).Text);
    }

    [Fact]
    public async Task should_toast_no_response_without_exception_when_nats_down_in_broker_mode()
    {
        // Arrange — 진짜 서비스 + 진짜 실행기 + 끊긴 NATS
        var nats = new Mock<INatsService>();
        nats.Setup(n => n.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new InvalidOperationException("NATS 연결 끊김"));
        var natsSetup = new Mock<INatsSetupModel>();
        natsSetup.SetupGet(n => n.DomainNats).Returns("sensorway");
        natsSetup.SetupGet(n => n.GroupNats).Returns("unit999");
        var h = new Harness
        {
            Settings = new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.Broker, BrokerResponseTimeoutSeconds = 2 } },
            Broker = new CameraPopupBrokerService(new BrokerRequestClient(nats.Object), natsSetup.Object),
        };
        var dispatcher = h.Build();

        // Act
        var ex = Record.Exception(() => dispatcher.Handle(CameraMarker()));
        var outcome = await dispatcher.LastBrokerTask;

        // Assert
        Assert.Null(ex);
        Assert.Equal(CameraPopupBrokerOutcomeKind.NoResponse, outcome?.Kind);
        Assert.Equal("NVR Manager 응답 없음(2초) — 이 관제석에서 돌고 있는지 확인하세요", h.Toasts.Last().Text);
    }

    [Fact]
    public async Task should_use_linked_device_id_fallback_when_camera_model_missing_in_broker_mode()
    {
        var h = new Harness { Settings = new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.Broker } } };
        var dispatcher = h.Build();

        dispatcher.Handle(CameraMarker(id: 77, withCamera: false));
        await dispatcher.LastBrokerTask;

        Assert.Equal(77, Assert.Single(((FakeBroker)h.Broker!).Requests).CameraId);
    }

    // ══════ 배선 — 지도 뷰모델이 실제로 이 분기기를 탄다 ══════

    [Fact]
    public void should_route_map_double_click_to_broker_and_show_bottom_toast_when_mode_is_broker()
    {
        // Arrange — 무거운 생성자를 건너뛰고 더블클릭 처리기만 돌린다(설정 창구 · 브로커는 해석 캐시에 끼운다).
        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        var broker = new FakeBroker { Hold = new TaskCompletionSource<CameraPopupBrokerOutcome>().Task };
        Set(vm, "_overlaySettings", new FakeOverlaySettings { Value = new CameraPopupSettings { Mode = CameraPopupMode.Broker } });
        Set(vm, "_overlaySettingsResolved", true);
        Set(vm, "_cameraPopupBroker", broker);
        Set(vm, "_cameraPopupBrokerResolved", true);
        var handler = typeof(MapViewModel).GetMethod("OnMapMarkerDoubleClicked", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Act
        handler.Invoke(vm, new object?[] { CameraMarker(id: 12, title: "서문 PTZ") });

        // Assert
        Assert.Equal(12, Assert.Single(broker.Requests).CameraId);
        Assert.Equal(ClientId, broker.Requests[0].TargetClientId);
        Assert.False(string.IsNullOrWhiteSpace(broker.Requests[0].RequestedBy));   // 로그인 없으면 Windows 사용자명 폴백
        Assert.True(vm.IsCameraPopupToastVisible);
        Assert.Equal("NVR 관제석에 서문 PTZ 팝업을 요청했습니다", vm.CameraPopupToastMessage);
    }

    private static void Set(object target, string field, object? value)
        => typeof(MapViewModel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}

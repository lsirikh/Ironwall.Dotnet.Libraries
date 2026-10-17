using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-188(카메라 팝업 제어 허브) — 팝업 2개 → 허브 → 하나 닫기 → 「모두 닫기」(확인) · 허브 끌기 → 위치 기억.
/// 실제 영상 · LibVLC · 팝업 호스트 프로세스는 쓰지 않는다: 팝업 뷰모델은 호스트 없이 만들고(영상 "사용할 수 없음"), 허브는 실제 컨트롤
/// (CameraPopupControlHubStyle 템플릿, 부모 = Canvas, 창 없음)을 MapView.xaml 과 같은 바인딩으로 MapViewModel 에 묶는다. 위치 저장소는 가짜.
/// 수 표시 · 클램프 산수는 CameraPopupHubMathTests 몫.
/// </summary>
public class CameraPopupHubScenarioTests
{
    private sealed class FakeHubStore : ICameraPopupHubPositionStore
    {
        public (double X, double Y)? Saved;
        public int SaveCount;
        public Task<(double X, double Y)?> TryGetAsync(CancellationToken token = default) => Task.FromResult(Saved);
        public Task SaveAsync(double x, double y, CancellationToken token = default) { Saved = (x, y); SaveCount++; return Task.CompletedTask; }
    }

    private static (MapViewModel vm, Sink sink) Vm(FakeHubStore store)
    {
        var vm = Scenario.Uninit<MapViewModel>();
        var map = new GMapCustomControl { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        Scenario.SetField(vm, "<MainMap>k__BackingField", map);
        Scenario.SetField(vm, "_log", Mock.Of<ILogService>());
        var ea = new EventAggregator(); var sink = new Sink(); ea.SubscribeOnPublishedThread(sink);
        Scenario.SetField(vm, "_eventAggregator", ea);
        // 생성자 없이 만들어 필드 초기화식이 안 돈다 — 허브 · 팝업 닫기 경로가 읽는 것만 넣는다
        Scenario.SetField(vm, "_hubPositionX", double.NaN);
        Scenario.SetField(vm, "_hubPositionY", double.NaN);
        Scenario.SetField(vm, "_popupAutoCloseTimers", new Dictionary<CameraStreamPopupViewModel, DispatcherTimer>());
        Scenario.SetField(vm, "_ptzGestureCts", new ConcurrentDictionary<int, CancellationTokenSource>());
        Scenario.SetField(vm, "_cameraPopupHostResolved", true);   // 팝업 호스트 없음(영상 · PTZ 비활성, GIS 정상)
        Scenario.SetField(vm, "_hubPositionStore", store);
        Scenario.Call(vm, "InitializeNavigationCommands");
        return (vm, sink);
    }

    /// <summary>MapView.xaml 의 허브 선언과 같은 바인딩(Canvas.Left/Top ← HubX/Y, HubX/Y ⇄ HubPositionX/Y, 수 ← CameraPopups.Count).</summary>
    private static (CameraPopupControlHub hub, Canvas canvas) Hub(MapViewModel vm)
    {
        var hub = new CameraPopupControlHub();
        hub.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/CameraPopupControlHubStyle.xaml", UriKind.Relative) });
        hub.Style = (Style)hub.Resources[typeof(CameraPopupControlHub)];
        hub.SetBinding(Canvas.LeftProperty, new Binding(nameof(CameraPopupControlHub.HubX)) { RelativeSource = RelativeSource.Self });
        hub.SetBinding(Canvas.TopProperty, new Binding(nameof(CameraPopupControlHub.HubY)) { RelativeSource = RelativeSource.Self });
        hub.SetBinding(CameraPopupControlHub.CloseAllCommandProperty, new Binding(nameof(MapViewModel.CloseAllCameraPopupsCommand)) { Source = vm });
        hub.SetBinding(CameraPopupControlHub.CloseCommandProperty, new Binding(nameof(MapViewModel.CloseCameraPopupCommand)) { Source = vm });
        hub.SetBinding(CameraPopupControlHub.DragCompletedCommandProperty, new Binding(nameof(MapViewModel.SaveHubPositionCommand)) { Source = vm });
        hub.SetBinding(CameraPopupControlHub.FocusCommandProperty, new Binding(nameof(MapViewModel.FocusCameraPopupCommand)) { Source = vm });
        hub.SetBinding(CameraPopupControlHub.HubXProperty, new Binding(nameof(MapViewModel.HubPositionX)) { Source = vm, Mode = BindingMode.TwoWay });
        hub.SetBinding(CameraPopupControlHub.HubYProperty, new Binding(nameof(MapViewModel.HubPositionY)) { Source = vm, Mode = BindingMode.TwoWay });
        hub.SetBinding(CameraPopupControlHub.OpenCountProperty, new Binding("CameraPopups.Count") { Source = vm });
        hub.SetBinding(CameraPopupControlHub.PopupsSourceProperty, new Binding(nameof(MapViewModel.CameraPopups)) { Source = vm });
        var canvas = new Canvas { Width = 1200, Height = 800 };
        canvas.Children.Add(hub);
        Scenario.Layout(canvas, 1200, 800);
        return (hub, canvas);
    }

    private static CameraStreamPopupViewModel Popup(int cameraId, string title) => new(cameraId, title, new PointLatLng(37.5, 127.0));

    [Fact]
    [Trait("Scenario", "MAP-188")]
    public void should_count_two_then_one_then_zero_and_hide_hub_when_one_closed_then_close_all_confirmed() => OneSta.StaAsync(async () =>
    {
        var (vm, sink) = Vm(new FakeHubStore());
        var a = Popup(11, "정문 PTZ"); var b = Popup(12, "후문 고정");
        vm.CameraPopups.Add(a); vm.CameraPopups.Add(b);
        var (hub, canvas) = Hub(vm);

        Assert.Equal(2, hub.OpenCount);
        Assert.Equal("2", hub.BadgeText);
        Assert.Equal(Visibility.Visible, hub.Visibility);
        Assert.Equal(2, hub.PopupsSource!.Cast<object>().Count());   // 플라이아웃 목록 = 열린 팝업

        // 허브 행 [✕](CloseCommand, param = 그 팝업) — 하나 닫기
        vm.SelectedCameraPopup = a;
        hub.CloseCommand!.Execute(a);
        Scenario.Layout(canvas, 1200, 800);
        Assert.Equal(1, hub.OpenCount);
        Assert.Equal("1", hub.BadgeText);
        Assert.Equal(new[] { b }, vm.CameraPopups);
        Assert.Null(vm.SelectedCameraPopup);                            // 닫힌 팝업 선택이 남지 않는다
        Assert.Equal(Visibility.Visible, hub.Visibility);

        // 「모두 닫기」 — 표준 확인 팝업을 먼저 띄우고(바로 닫지 않는다), 확인 콜백에서 닫는다
        hub.CloseAllCommand!.Execute(null);
        var confirm = Assert.Single(sink.Messages.OfType<OpenConfirmPopupMessageModel>());
        Assert.Equal("열린 카메라 영상 1개를 모두 닫으시겠습니까?", confirm.Explain);
        Assert.Equal(1, hub.OpenCount);
        var callback = Assert.IsType<CallCloseAllCameraPopupsProcessMessageModel>(confirm.MessageModel);
        await vm.HandleAsync(callback, CancellationToken.None);      // 「확인」 = 팝업 인프라가 콜백 메시지를 재발행
        Scenario.Layout(canvas, 1200, 800);

        Assert.Empty(vm.CameraPopups);
        Assert.Equal(0, hub.OpenCount);
        Assert.Equal(Visibility.Collapsed, hub.Visibility);            // 0 개면 허브 자체가 숨는다
        Assert.False(hub.IsFlyoutOpen);
        Assert.Single(sink.Messages.OfType<ClosePopupMessageModel>()); // 확인창도 닫는다

        // 0 개에서 「모두 닫기」가 다시 와도 확인창을 띄우지 않는다
        vm.CloseAllCameraPopupsCommand!.Execute(null);
        Assert.Single(sink.Messages.OfType<OpenConfirmPopupMessageModel>());
    });

    /// <remarks>헤디드 몫: 알약(PART_Pill)을 실제 마우스로 끄는 동작(8 px 데드존 · 캡처). 여기서는 끌기가 하는 일(HubX/Y 갱신 → 놓을 때 DragCompletedCommand)을 그대로 밟는다.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-188")]
    public void should_save_dragged_hub_position_and_restore_it_on_next_map_view() => OneSta.StaAsync(async () =>
    {
        var store = new FakeHubStore();
        var (vm, _) = Vm(store);
        vm.CameraPopups.Add(Popup(11, "정문 PTZ"));
        var (hub, canvas) = Hub(vm);
        // 창이 없어 Loaded 가 오지 않는다 — Loaded 처리기(부모 Canvas 찾기 + 자리 잡기)를 직접 부른다
        Scenario.Call(hub, "OnLoaded", hub, new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.True(hub.ActualWidth > 0 && hub.ActualHeight > 0);
        Assert.Equal(Math.Max(0, 1200 - hub.ActualWidth - 360), hub.HubX, 6);   // 저장값 없음 = 기본 우하단 도킹
        Assert.Equal(Math.Max(0, 800 - hub.ActualHeight - 14), hub.HubY, 6);
        Assert.Equal(hub.HubX, vm.HubPositionX);                       // 기본 도킹 자리도 VM 으로 돌아간다(TwoWay)

        // 끌기: 허브 좌표 갱신(OnPillMove) → 놓기(OnPillUp) = DragCompletedCommand
        hub.HubX = 300; hub.HubY = 200;
        Assert.Equal(300, vm.HubPositionX);
        Assert.Equal(200, vm.HubPositionY);
        Assert.Equal(300, Canvas.GetLeft(hub));
        hub.DragCompletedCommand!.Execute(null);
        Assert.Equal((300d, 200d), store.Saved);
        Assert.Equal(1, store.SaveCount);

        // 다음 지도(재시작) — 저장소에서 읽어 같은 자리
        var (vm2, _) = Vm(store);
        await (Task)Scenario.Call(vm2, "LoadHubPositionAsync")!;
        Assert.Equal(300, vm2.HubPositionX);
        Assert.Equal(200, vm2.HubPositionY);
        vm2.CameraPopups.Add(Popup(12, "후문 고정"));
        var (hub2, canvas2) = Hub(vm2);
        Assert.Equal(300, hub2.HubX);
        Assert.Equal(200, hub2.HubY);
        Assert.Equal(300, Canvas.GetLeft(hub2));
        Assert.Equal(200, Canvas.GetTop(hub2));

        // 위치가 없으면(NaN) 저장하지 않는다
        var store3 = new FakeHubStore();
        var (vm3, _) = Vm(store3);
        vm3.SaveHubPositionCommand!.Execute(null);
        Assert.Equal(0, store3.SaveCount);
    });
}

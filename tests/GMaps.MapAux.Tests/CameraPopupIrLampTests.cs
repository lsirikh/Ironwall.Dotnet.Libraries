using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 지도 카메라 팝업 옵션 탭의 IR 조명(2026-10-22) — 카메라가 알린 값만 단추로 보이고, 누르면 호스트에 SetIrLamp 요청,
/// 성공이면 "마지막으로 보낸 값" 강조, 실패 · 미지원은 알림 한 줄(던지지 않는다). 호스트는 가짜(ICameraPopupControl), 창 없음.
/// </summary>
public class CameraPopupIrLampTests
{
    private static readonly VideoProviderInfo Provider = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    private static CameraStreamPopupViewModel Popup() => new(7, "정문", new PointLatLng(37, 127), null, Provider);

    [Fact]
    [Trait("Scenario", "MAP-IRLAMP-01")]
    public void should_show_only_announced_ir_lamp_buttons_and_hide_the_row_when_camera_announces_none()
    {
        var vm = Popup();
        Assert.False(vm.IsIrLampSupported);   // 처음(준비 전) = 감춤

        vm.SetIrLampState(new[] { IrLampMode.On, IrLampMode.Off }, null);
        var partial = (vm.IsIrLampSupported, vm.IsIrLampOnAvailable, vm.IsIrLampOffAvailable, vm.IsIrLampAutoAvailable, vm.IrLampCaption);
        vm.SetIrLampState(null, null);

        Assert.Equal((true, true, true, false, "IR 조명"), partial);
        Assert.False(vm.IsIrLampSupported);
    }

    [Fact]
    [Trait("Scenario", "MAP-IRLAMP-02")]
    public void should_raise_request_only_for_announced_modes_and_mark_the_last_sent_value()
    {
        var vm = Popup();
        var raised = new List<string>();
        vm.IrLampRequested += (_, m) => raised.Add(m);
        vm.SetIrLampState(new[] { IrLampMode.On, IrLampMode.Off }, IrLampMode.Off);

        vm.SetIrLampCommand.Execute("On");
        vm.SetIrLampCommand.Execute("Auto");   // 카메라가 알리지 않은 값 — 보내지 않는다

        Assert.Equal(new[] { "On" }, raised);
        Assert.Equal(IrLampMode.Off, vm.IrLampSent);
        Assert.Equal("IR 조명 (마지막으로 보낸 값)", vm.IrLampCaption);
    }

    private static (MapViewModel map, Mock<ICameraPopupControl> control, List<CameraRequest> sent) Map(Func<CameraRequest, CameraResponse> answer)
    {
        var map = Scenario.Uninit<MapViewModel>();
        Scenario.SetField(map, "_log", Mock.Of<ILogService>());
        var sent = new List<CameraRequest>();
        var control = new Mock<ICameraPopupControl>();
        control.Setup(c => c.RequestAsync(It.IsAny<CameraRequest>(), It.IsAny<CancellationToken>()))
            .Returns<CameraRequest, CancellationToken>((r, _) => { sent.Add(r); return Task.FromResult(answer(r)); });
        Scenario.SetField(map, "_cameraPopupControl", control.Object);
        return (map, control, sent);
    }

    private static Task Handle(MapViewModel map, CameraStreamPopupViewModel vm, string mode)
        => (Task)Scenario.Call(map, "HandleIrLampAsync", vm, mode)!;

    [Fact]
    [Trait("Scenario", "MAP-IRLAMP-03")]
    public async Task should_send_set_ir_lamp_and_keep_the_value_when_host_succeeds()
    {
        var (map, _, sent) = Map(r => new CameraResponse { RequestId = r.RequestId, Kind = r.Kind, CameraId = r.CameraId, Success = true, IrLamp = r.IrLamp });
        var vm = Popup();
        vm.SetIrLampState(new[] { IrLampMode.On, IrLampMode.Off, IrLampMode.Auto }, null);

        await Handle(map, vm, IrLampMode.Auto);

        var request = Assert.Single(sent);
        Assert.Equal((CameraRequestKind.SetIrLamp, "Auto", "7"), (request.Kind, request.IrLamp, request.CameraId));
        Assert.Equal(IrLampMode.Auto, vm.IrLampSent);
        Assert.Null(vm.IrLampStatusText);
    }

    [Theory]
    [Trait("Scenario", "MAP-IRLAMP-04")]
    [InlineData(CameraErrorCodes.Failed, "IR 조명을 바꾸지 못했습니다 — 카메라 연결을 확인하세요")]
    [InlineData(CameraErrorCodes.HostUnavailable, "IR 조명을 바꾸지 못했습니다 — 카메라 연결을 확인하세요")]
    [InlineData(CameraErrorCodes.NotSupported, "이 카메라는 이 IR 조명 명령을 받지 않습니다")]
    public async Task should_show_a_status_line_and_keep_the_last_value_when_host_fails(string code, string expected)
    {
        var (map, _, _) = Map(r => CameraResponse.Fail(r, code));
        var vm = Popup();
        vm.SetIrLampState(new[] { IrLampMode.On, IrLampMode.Off }, IrLampMode.Off);

        await Handle(map, vm, IrLampMode.On);

        Assert.Equal(IrLampMode.Off, vm.IrLampSent);
        Assert.Equal(expected, vm.IrLampStatusText);
    }

    [Fact]
    [Trait("Scenario", "MAP-IRLAMP-05")]
    public async Task should_not_throw_when_the_host_call_itself_throws()
    {
        var (map, control, _) = Map(_ => throw new InvalidOperationException("pipe broke"));
        var vm = Popup();
        vm.SetIrLampState(new[] { IrLampMode.On }, null);

        var ex = await Record.ExceptionAsync(() => Handle(map, vm, IrLampMode.On));

        Assert.Null(ex);   // GIS 는 죽지 않는다
        Assert.Null(vm.IrLampSent);
    }

    // ── 실제 옵션 탭 템플릿(CameraStreamPopupStyle.xaml, 창 없이 Measure/Arrange) ──

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    /// <summary>자기와 조상이 모두 Visible(창이 없어 IsVisible 은 늘 false 라 직접 본다).</summary>
    private static bool Shown(DependencyObject e, DependencyObject root)
    {
        for (var d = e; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    private static ResourceDictionary Tokens(string name)
        => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{name}.xaml", UriKind.Relative) };

    [Fact]
    [Trait("Scenario", "MAP-IRLAMP-06")]
    public void should_show_the_ir_lamp_row_only_with_announced_buttons_in_the_real_options_tab() => OneSta.Sta(() =>
    {
        var vm = Popup();
        vm.ActiveTab = 2;
        vm.IsPanelExpanded = true;   // 탭 패널 펼침(우버튼 짧은클릭)
        var popup = new CameraStreamPopupControl { Width = CameraStreamPopupViewModel.DefaultWidth, Height = CameraStreamPopupViewModel.DefaultHeight, DataContext = vm };
        popup.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            MergedDictionaries =
            {
                Tokens("Shared"), Tokens("Dark"),
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/CameraStreamPopupStyle.xaml", UriKind.Relative) },
            },
        });
        popup.Style = (Style)popup.Resources[typeof(CameraStreamPopupControl)];
        void Layout()
        {
            popup.ApplyTemplate();
            popup.Measure(new Size(popup.Width, popup.Height));
            popup.Arrange(new Rect(0, 0, popup.Width, popup.Height));
            popup.UpdateLayout();
        }
        string LampButtons() => string.Join(",", Tree(popup).OfType<Button>()
            .Where(b => ReferenceEquals(b.Command, vm.SetIrLampCommand) && Shown(b, popup)).Select(b => (string)b.CommandParameter));
        bool NoticeShown() => Tree(popup).OfType<TextBlock>().Any(t => t.Text == "이 카메라는 영상 옵션을 지원하지 않습니다" && Shown(t, popup));

        Layout();
        var before = (LampButtons(), NoticeShown());
        vm.SetIrLampState(new[] { IrLampMode.On, IrLampMode.Off }, null);   // 영상 옵션은 없고 IR 조명만 되는 카메라
        Layout();
        var after = (LampButtons(), NoticeShown());
        var help = Tree(popup).OfType<HelpTip>().Where(h => Shown(h, popup)).Select(h => h.HelpKey).ToList();

        Assert.Equal(("", true), before);                  // 지원 안 함 = 칸 없음
        Assert.Equal(("On,Off", false), after);    // 알린 값만, 미지원 안내는 감춤
        Assert.Contains("Map.CameraPopup.Options", help);                   // 설명은 "?" 말풍선
    });
}

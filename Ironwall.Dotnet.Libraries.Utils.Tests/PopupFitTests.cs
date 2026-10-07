using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 2026-10-08 실창 레이아웃 점검(LayoutSweepTests)이 찾은 팝업 넘침 2건 — 조치 트레이 문구 콤보 목록이 작업 표시줄까지 내려감(FAIL),
/// 부대 창 오른쪽 끝 "?" 말풍선이 창 밖으로 펼쳐짐(WARN). 배치 판정은 순수 함수라 화면 없이 잰다 + 배선은 글자로 지킨다.
/// </summary>
public class PopupFitTests
{
    // 1920×1080 화면, 작업 영역 높이 1032(작업 표시줄 48)
    private static readonly Rect Work = new(0, 0, 1920, 1032);

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    #region - Drop-down (조치 트레이 문구) -
    // 이벤트 창(작업 영역 안 아래쪽), 트레이 문구 콤보 (564,865,728x25)
    private static readonly Rect EventWindow = new(0, 0, 1920, 1000);
    private static readonly Rect TrayCombo = new(564, 865, 728, 25);

    [Fact]
    public void tray_phrase_list_that_would_run_into_the_taskbar_opens_upward_inside_the_window()
    {
        // Arrange — 원래 템플릿은 콤보 바로 아래를 제안한다(문구 7개 ≈ 230)
        var popup = new Size(728, 230);
        var proposed = new Point(TrayCombo.Left, TrayCombo.Bottom);
        var bounds = DropDownPlacement.Bounds(EventWindow, Work);

        // Act
        var p = DropDownPlacement.Resolve(TrayCombo, popup, proposed, bounds);

        // Assert — 위로 펼쳐 창 안에 통째로
        Assert.Equal(TrayCombo.Top - popup.Height, p.Y);
        Assert.True(p.Y >= bounds.Top && p.Y + popup.Height <= EventWindow.Bottom);
        Assert.Equal(TrayCombo.Left, p.X);
    }

    [Fact]
    public void list_that_fits_below_keeps_the_template_position()
    {
        var combo = new Rect(564, 200, 728, 25);
        var popup = new Size(728, 230);
        var proposed = new Point(combo.Left, combo.Bottom);

        var p = DropDownPlacement.Resolve(combo, popup, proposed, DropDownPlacement.Bounds(EventWindow, Work));

        Assert.Equal(proposed, p);
    }

    [Fact]
    public void list_height_is_capped_to_the_larger_room_so_added_phrases_scroll_instead_of_overflowing()
    {
        // Arrange — 문구 관리에서 문구를 잔뜩 늘렸다: 원래 상한(1000)이 위쪽 자리보다 크다
        var bounds = DropDownPlacement.Bounds(EventWindow, Work);

        // Act
        var max = DropDownPlacement.MaxHeight(TrayCombo, bounds, requested: 1000);
        var popup = new Size(728, max + DropDownPlacement.ChromeAllowance);
        var p = DropDownPlacement.Resolve(TrayCombo, popup, new Point(TrayCombo.Left, TrayCombo.Bottom), bounds);

        // Assert — 위쪽 자리(865)에 맞춰 잘렸고, 그 높이로 위에 통째로 들어간다
        Assert.Equal(TrayCombo.Top - bounds.Top - DropDownPlacement.ChromeAllowance, max);
        Assert.True(p.Y >= bounds.Top && p.Y + popup.Height <= TrayCombo.Top + 0.001);
    }

    [Fact]
    public void list_height_never_grows_past_the_requested_maximum()
    {
        var max = DropDownPlacement.MaxHeight(TrayCombo, DropDownPlacement.Bounds(EventWindow, Work), requested: 360);

        Assert.Equal(360, max);
    }

    [Fact]
    public void window_reaching_under_the_taskbar_is_cut_at_the_work_area()
    {
        // 창이 작업 표시줄 줄까지 내려가 있어도 목록은 작업 영역(1032) 위에서 끝난다
        var tall = new Rect(0, 0, 1920, 1080);
        var combo = new Rect(564, 960, 728, 25);
        var popup = new Size(728, 120);

        var bounds = DropDownPlacement.Bounds(tall, Work);
        var p = DropDownPlacement.Resolve(combo, popup, new Point(combo.Left, combo.Bottom), bounds);

        Assert.Equal(Work.Bottom, bounds.Bottom);
        Assert.True(p.Y + popup.Height <= Work.Bottom);
    }

    [Fact]
    public void unknown_window_falls_back_to_the_work_area()
    {
        Assert.Equal(Work, DropDownPlacement.Bounds(Rect.Empty, Work));
        Assert.Equal(EventWindow, DropDownPlacement.Bounds(EventWindow, Rect.Empty));
    }

    [Fact]
    public void offscreen_test_window_is_left_alone()
    {
        var combo = new Rect(-10000, -10000, 200, 25);
        var proposed = new Point(-10000, -9975);

        var p = DropDownPlacement.Resolve(combo, new Size(200, 300), proposed, Work);

        Assert.Equal(proposed, p);
        Assert.Equal(360, DropDownPlacement.MaxHeight(combo, Work, 360));
    }
    #endregion

    #region - Help callout (부대 창 편제 트리 "?") -
    // 부대 창 — 오른쪽 끝이 1590, "?" (1573,308)
    private static readonly Rect UnitWindow = new(330, 120, 1260, 800);
    private static readonly Rect TreeHelp = new(1573, 308, 16, 16);
    private static readonly Size Callout = new(436, 200);

    [Fact]
    public void callout_near_the_right_window_edge_opens_inside_the_window()
    {
        // 작업 영역 기준만으로는 말풍선 x 가 창 오른쪽 밖으로 나갔다(1509~1945)
        var screenOnly = HelpCalloutPlacement.Resolve(TreeHelp, Callout, Work);
        Assert.True(screenOnly.Position.X + Callout.Width > UnitWindow.Right);

        var r = HelpCalloutPlacement.Resolve(TreeHelp, Callout, UnitWindow, Work);

        Assert.Equal(HelpCalloutSide.Below, r.Side);
        Assert.True(r.Position.X >= UnitWindow.Left && r.Position.X + Callout.Width <= UnitWindow.Right);
        Assert.True(r.Position.Y + Callout.Height <= UnitWindow.Bottom);
        Assert.True(r.TailOffset > Callout.Width / 2);   // 꼬리는 오른쪽("?" 쪽) 모서리로 옮겨 간다
    }

    [Fact]
    public void callout_that_already_fits_the_window_is_placed_as_before()
    {
        var target = new Rect(600, 300, 16, 16);

        var r = HelpCalloutPlacement.Resolve(target, Callout, UnitWindow, Work);
        var before = HelpCalloutPlacement.Resolve(target, Callout, Work);

        Assert.Equal(before, r);
    }

    [Fact]
    public void callout_larger_than_a_small_dialog_falls_back_to_the_work_area()
    {
        // 말풍선보다 작은 대화창 — 창 안 어디에도 통째로 안 들어간다
        var dialog = new Rect(800, 400, 300, 160);
        var target = new Rect(1080, 410, 16, 16);

        var r = HelpCalloutPlacement.Resolve(target, Callout, dialog, Work);

        Assert.Equal(HelpCalloutPlacement.Resolve(target, Callout, Work), r);
    }

    [Fact]
    public void callout_of_an_offscreen_test_window_is_not_pulled_onscreen()
    {
        var window = new Rect(-10200, -10200, 800, 600);
        var target = new Rect(-10000, -10000, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, window, Work);

        Assert.Equal(HelpCalloutSide.Below, r.Side);
        Assert.True(r.Position.X < -9000 && r.Position.Y < -9000);
    }
    #endregion

    #region - Wiring contracts -
    [Fact]
    public void tray_phrase_combo_turns_on_drop_down_fit()
    {
        var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Events.Ui", "Views", "Consoles", "ActionTrayView.xaml");
        var combo = XDocument.Load(path).Descendants()
            .Single(e => e.Name.LocalName == "ComboBox"
                         && (string?)e.Attribute(XName.Get("AutomationProperties.AutomationId")) == "Console.Events.Tray.Phrase");

        var fit = combo.Attributes().SingleOrDefault(a => a.Name.LocalName == "DropDownFit.IsEnabled");
        Assert.NotNull(fit);
        Assert.Equal("True", fit!.Value);
        Assert.Equal("clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils", fit.Name.NamespaceName);
    }

    [Fact]
    public void help_tip_places_its_callout_window_first()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Utils", "Console", "Help", "HelpTip.cs"));

        Assert.Contains("PopupScreenBounds.For(this, targetSize)", source);
        Assert.Contains("HelpCalloutPlacement.Resolve(target, popupSize, window, work)", source);
    }
    #endregion
}

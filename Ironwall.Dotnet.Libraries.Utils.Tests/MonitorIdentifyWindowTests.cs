using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose      : 모니터 식별 카드 창 — 창 속성 · 모양 · 클릭 통과 스타일 (화면에 띄우지 않는다)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 창은 <b>띄우지 않는다</b> — 손잡이(HWND)만 만들어 확장 스타일을 읽는다. 손잡이를 만들어도 보이지 않고,
/// 자리도 화면 밖(-32000)이다. 개발 PC 를 쓰는 사람의 화면을 건드리지 않는다.
/// </summary>
public class MonitorIdentifyWindowTests
{
    private const string Prefix = "CameraPopup.MonitorIdentify";

    private static MonitorIdentifyCard Card(bool selected)
        => new(2, "모니터 2 · 1920×1080 · 주", "TEST-NO-SUCH-DEVICE", new Int32Rect(-20000, -20000, 1920, 1080), 96, selected);

    [Fact]
    public void should_be_borderless_transparent_topmost_and_non_activating_when_created()
    {
        var r = OnSta(() =>
        {
            var w = new MonitorIdentifyWindow(Card(false), Prefix);
            var snapshot = (w.WindowStyle, w.AllowsTransparency, w.Topmost, w.ShowActivated, w.ShowInTaskbar,
                            w.IsHitTestVisible, w.Focusable, w.IsVisible,
                            AutomationId: AutomationProperties.GetAutomationId(w));
            w.Close();
            return snapshot;
        });

        Assert.Equal(WindowStyle.None, r.WindowStyle);
        Assert.True(r.AllowsTransparency);
        Assert.True(r.Topmost);
        Assert.False(r.ShowActivated);
        Assert.False(r.ShowInTaskbar);
        Assert.False(r.IsHitTestVisible);
        Assert.False(r.Focusable);
        Assert.False(r.IsVisible);
        Assert.Equal("CameraPopup.MonitorIdentify.2", r.AutomationId);
    }

    [Fact]
    public void should_apply_click_through_ex_style_when_handle_is_created_without_showing()
    {
        var r = OnSta(() =>
        {
            var w = new MonitorIdentifyWindow(Card(false), Prefix);
            var hwnd = w.EnsureHandle();
            var snapshot = (Hwnd: hwnd, Style: w.ExtendedStyle, w.IsVisible);
            w.Close();
            return snapshot;
        });

        Assert.NotEqual(IntPtr.Zero, r.Hwnd);
        Assert.True(MonitorIdentifyMath.IsClickThrough(r.Style));
        Assert.False(r.IsVisible);
    }

    [Fact]
    public void should_show_big_number_and_combo_label_when_card_is_built()
    {
        var texts = OnSta(() =>
        {
            var w = new MonitorIdentifyWindow(Card(false), Prefix);
            var list = Texts(w.CardBorder);
            w.Close();
            return list;
        });

        Assert.Equal(new[] { "2", "모니터 2 · 1920×1080 · 주" }, texts);
    }

    [Fact]
    public void should_add_thick_outline_and_selected_tag_when_card_is_selected()
    {
        var r = OnSta(() =>
        {
            var w = new MonitorIdentifyWindow(Card(true), Prefix);
            var snapshot = (Thickness: w.CardBorder.BorderThickness.Left, HasTag: w.SelectedTag is not null,
                            Texts: Texts(w.CardBorder), Name: AutomationProperties.GetName(w));
            w.Close();
            return snapshot;
        });

        Assert.Equal(MonitorIdentifyMath.SelectedOutline, r.Thickness);
        Assert.True(r.HasTag);
        Assert.Contains(MonitorIdentifyMath.SelectedText, r.Texts);
        Assert.EndsWith(MonitorIdentifyMath.SelectedText, r.Name);
    }

    [Fact]
    public void should_use_thin_outline_and_no_tag_when_card_is_not_selected()
    {
        var r = OnSta(() =>
        {
            var w = new MonitorIdentifyWindow(Card(false), Prefix);
            var snapshot = (Thickness: w.CardBorder.BorderThickness.Left, HasTag: w.SelectedTag is not null, Texts: Texts(w.CardBorder));
            w.Close();
            return snapshot;
        });

        Assert.Equal(MonitorIdentifyMath.NormalOutline, r.Thickness);
        Assert.False(r.HasTag);
        Assert.DoesNotContain(MonitorIdentifyMath.SelectedText, r.Texts);
    }

    [Fact]
    public void should_close_unshown_window_when_dismissed_with_animation()
    {
        var closed = OnSta(() =>
        {
            var w = new MonitorIdentifyWindow(Card(false), Prefix);
            w.Dismiss(animate: true);       // 안 보이는 창은 사라짐을 기다리지 않고 바로 닫는다
            w.Dismiss(animate: true);       // 두 번 불러도 된다
            return w.IsClosed;
        });

        Assert.True(closed);
    }

    #region - 도우미 -
    private static string[] Texts(DependencyObject root)
    {
        var result = new List<string>();
        void Walk(object node)
        {
            if (node is TextBlock t) result.Add(t.Text);
            if (node is DependencyObject d)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(d)) Walk(child);
            }
        }
        Walk(root);
        return result.ToArray();
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
        return result;
    }
    #endregion
}

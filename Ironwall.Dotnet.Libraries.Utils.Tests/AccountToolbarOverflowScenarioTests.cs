using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 시나리오 카탈로그 ACC-009(계정 콘솔 툴바 넘침 — 좁힘 → [⋯] → 열 메뉴가 제자리 [열 n/m] 과 같다)를 창 없이 확인한다.
/// </summary>
/// <remarks>
/// 계정 콘솔은 커널 <see cref="ConsoleToolbar"/> 를 <c>ConsoleKey="Accounts"</c> 로 쓰고, 열 메뉴는 <c>ColumnsClick="OnColumns"</c> 하나로만 연다.
/// 그래서 ① 좁으면 [열 n/m] 이 [⋯] 뒤로 접히고 ② 팝업 안의 [열 n/m] 이 제자리 단추와 <b>같은 ColumnsClick</b> 을 내면 같은 메뉴가 열린다.
/// 실제 커널 템플릿(Generic.xaml)을 물려 헤드리스로 잰다(창 · 화면 밖 창 없음 — <see cref="ConsoleToolbarTwoRowTests"/> 와 같은 방법).
/// 실제 폭으로 접히는 모습 · 팝업 위치 · 메뉴 항목 그림은 헤디드 몫이다.
/// </remarks>
[Collection(WpfApplicationCollection.Name)]
public class AccountToolbarOverflowScenarioTests
{
    private static readonly object EnsureApplicationGate = new();

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }

    /// <summary>계정 콘솔과 같은 모양 — Extra 없음 · 열 단추 있음 · 검색 있음(사용자 레일).</summary>
    private static ConsoleToolbar AccountsToolbar()
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }
        var kernel = (ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative));
        return new ConsoleToolbar { Style = (Style)kernel[typeof(ConsoleToolbar)], ConsoleKey = "Accounts", ColumnsText = "열 6/12", AddText = "사용자 추가" };
    }

    private static void Settle(FrameworkElement element, double width)
    {
        for (var i = 0; i < 6; i++)
        {
            element.Measure(new Size(width, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
            element.UpdateLayout();
        }
    }

    /// <summary>시각 트리를 걷고, 팝업을 만나면 그 안(논리 자식 — 닫힌 팝업은 시각 트리에 없다)도 걷는다.</summary>
    private static T? ById<T>(DependencyObject root, string automationId) where T : FrameworkElement
    {
        if (root is T match && AutomationProperties.GetAutomationId(match) == automationId) return match;
        if (root is Popup { Child: { } inner } && ById<T>(inner, automationId) is { } deep) return deep;
        if (root is not System.Windows.Media.Visual) return null;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            if (ById<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, i), automationId) is { } found) return found;
        return null;
    }

    [Trait("Scenario", "ACC-009")]
    [Fact]
    public void should_keep_the_columns_button_in_place_without_the_more_button_when_the_accounts_toolbar_is_wide()
    {
        var (overflow, moreVisible) = OnSta(() =>
        {
            var toolbar = AccountsToolbar();
            Settle(toolbar, 1000);
            var more = ById<Button>(toolbar, "Console.Accounts.Toolbar.More");
            return (toolbar.IsColumnsOverflow, more?.Visibility == Visibility.Visible);
        });

        Assert.False(overflow);
        Assert.False(moreVisible);
    }

    [Trait("Scenario", "ACC-009")]
    [Fact]
    public void should_fold_the_columns_button_behind_the_more_button_and_raise_the_same_columns_click_when_the_accounts_toolbar_is_narrow()
    {
        var (overflow, moreVisible, inPlaceClicks, foldedClicks) = OnSta(() =>
        {
            var toolbar = AccountsToolbar();
            var clicks = 0;
            toolbar.ColumnsClick += (_, _) => clicks++;

            // 넓을 때 제자리 [열 n/m] — 이것이 계정 콘솔의 열 메뉴(OnColumns)를 연다
            Settle(toolbar, 1000);
            var inPlace = (ButtonBase)toolbar.Template.FindName("PART_Columns", toolbar);
            inPlace.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var afterInPlace = clicks;

            // 좁힘 → [⋯] 가 나오고 [열 n/m] 은 그 뒤로 간다. 접히는 폭은 단추 글자 폭(글꼴)에 달려 있어
            // 고정 폭 하나 대신 넓은 쪽부터 좁혀 가며 처음 접히는 폭을 찾는다(더 좁아지면 검색이 아이콘으로 접혀 열 단추가 돌아올 수 있다).
            var width = 1000.0;
            for (; width >= 240 && !toolbar.IsColumnsOverflow; width -= 2) Settle(toolbar, width);
            Assert.True(toolbar.IsColumnsOverflow, "1000 → 240 으로 좁혀도 [열 n/m] 이 [⋯] 로 접히지 않았다");
            var more = ById<Button>(toolbar, "Console.Accounts.Toolbar.More");
            var folded = ById<Button>(toolbar, "Console.Accounts.Toolbar.ColumnsOverflow");
            Assert.NotNull(folded);
            Assert.Equal(Visibility.Visible, folded!.Visibility);          // 팝업 안에 접힌 [열 n/m] 이 보인다
            Assert.Equal("열 6/12", folded.Content);
            folded.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            return (toolbar.IsColumnsOverflow, more?.Visibility == Visibility.Visible, afterInPlace, clicks - afterInPlace);
        });

        Assert.True(overflow, "좁은 폭(360)에서 [열 n/m] 이 [⋯] 로 접히지 않았다");
        Assert.True(moreVisible, "[⋯](Console.Accounts.Toolbar.More) 가 보이지 않는다");
        Assert.Equal(1, inPlaceClicks);
        Assert.Equal(1, foldedClicks);                                     // 접힌 단추도 같은 ColumnsClick 한 번 → 같은 메뉴
    }

    [Trait("Scenario", "ACC-009")]
    [Fact]
    public void should_open_the_columns_menu_from_one_handler_on_the_accounts_console_toolbar()
    {
        var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", "AccountConsolePanelView.xaml");
        var toolbar = XDocument.Load(path).Descendants().Single(e => e.Name.LocalName == "ConsoleToolbar");

        Assert.Equal("Accounts", (string?)toolbar.Attribute("ConsoleKey"));   // [⋯] id = Console.Accounts.Toolbar.More
        Assert.Equal("OnColumns", (string?)toolbar.Attribute("ColumnsClick")); // 제자리 · 접힌 단추 모두 이 처리기 하나
    }
}

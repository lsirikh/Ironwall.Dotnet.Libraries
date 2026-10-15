using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;
using Xunit.Abstractions;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 권한 설정 표 — 모든 모듈 줄에 [전체] 단추와 동작 칸이 <b>실제로 그려진다</b>(2026-10-15 사용자 캡처: '카메라' 줄만 비었다).
/// 시각 트리 속성(IsVisible · 크기)은 정상이어도 그림이 비는 결함이라, 창을 그려 픽셀로 본다.
/// </summary>
public class PermissionMatrixRowCellsViewTests
{
    private readonly ITestOutputHelper _out;
    public PermissionMatrixRowCellsViewTests(ITestOutputHelper output) => _out = output;

    /// <remarks>
    /// 그룹 바꾸기 × (구성원 보기 왕복 · 끝까지 굴렸다 오기 · 칩 줄 펼침) × 라이트/다크. 2026-10-15 실창 캡처의 빈 줄은 이 조합 어디서도
    /// 재현되지 않았다(시각 트리 · 픽셀 모두 정상) — 같은 결함이 다시 생기면 여기서 잡히게 잠가 둔다.
    /// </remarks>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    public void should_paint_every_check_cell_and_row_all_button_on_every_module_row(bool members, bool scroll, bool strip) => AppHost.Run(() =>
    {
        var groups = Enumerable.Range(1, 6)
            .Select(i => new UserGroupDto { Id = 100 + i, Name = $"LRT-UI-PGNEW-14494{i}R", IsActive = true, Permissions = new PermissionsDto() })
            .ToArray();
        var (view, window, console) = HostPermissions(groups, width: 900, height: 520);
        try
        {
            foreach (var dark in new[] { false, true })
            {
                AppHost.SetDark(dark);
                foreach (var group in console.Matrix.Groups.ToList())
                {
                    console.Matrix.SelectedGroup = group;
                    AppHost.Pump(DispatcherPriority.ApplicationIdle);
                    var grid = Find<DataGrid>(view, g => AutomationProperties.GetAutomationId(g) == "Console.Accounts.Matrix")!;
                    if (members)
                    {
                        console.Matrix.ShowMembers = true;
                        AppHost.Pump(DispatcherPriority.ApplicationIdle);
                        console.Matrix.ShowMembers = false;
                        AppHost.Pump(DispatcherPriority.ApplicationIdle);
                    }
                    if (scroll)
                    {
                        grid.ScrollIntoView(grid.Items[^1]);
                        AppHost.Pump(DispatcherPriority.ApplicationIdle);
                        grid.ScrollIntoView(grid.Items[0]);
                        AppHost.Pump(DispatcherPriority.ApplicationIdle);
                    }
                    if (strip)
                    {
                        var s = Find<ConsoleChipStrip>(view, x => AutomationProperties.GetAutomationId(x) == "Console.Accounts.GroupStrip")!;
                        s.IsExpanded = !s.IsExpanded;
                        AppHost.Pump(DispatcherPriority.ApplicationIdle);
                    }
                    window.UpdateLayout();

                    var content = (FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);

                    var blank = new List<string>();
                    var viewport = grid.TranslatePoint(new Point(0, 0), content);
                    foreach (var row in FindAll<DataGridRow>(grid))
                    {
                        if (row.Item is not ModulePermRowViewModel m || !row.IsVisible) continue;
                        var top = row.TranslatePoint(new Point(0, 0), content).Y;
                        if (top < viewport.Y + 36 || top + row.ActualHeight > viewport.Y + grid.ActualHeight) continue;   // 머리 · 아래에 걸친 줄은 뺀다
                        foreach (var c in FindAll<CheckBox>(row))
                        {
                            var box = FindAll<Border>(c).First(b => b.Name == "Bd");
                                if (!Painted(bitmap, box, content)) blank.Add(AutomationProperties.GetAutomationId(c));
                        }
                        var all = FindAll<Button>(row).First(b => (b.Content as string) == "전체");
                        if (!Painted(bitmap, all, content)) blank.Add(m.ModuleKey + ".전체");
                    }
                    _out.WriteLine($"{(dark ? "dark" : "light")} {group.GroupName}: {string.Join(", ", blank)}");
                    Assert.True(blank.Count == 0, $"{(dark ? "다크" : "라이트")} {group.GroupName} — 그려지지 않은 칸: {string.Join(", ", blank)}");
                }
            }
        }
        finally
        {
            AppHost.SetDark(false);
            window.Close();
        }
    });

    /// <remarks>
    /// 사용자 캡처의 그룹(LRT-UI-PGNEW-…R)을 만든 실창 시험(SC-ACC-039 · 041 · 042)과 같은 순서 — 칸을 UIA Toggle 로 켜고 [저장](다시 읽기 → 줄 재생성),
    /// 다른 칸을 켰다 [되돌리기], 미적용 중 다른 칩을 눌렀다 [되돌리기].
    /// </remarks>
    [Fact]
    public void should_paint_every_check_cell_after_the_live_save_revert_sequence() => AppHost.Run(() =>
    {
        var groups = Enumerable.Range(1, 6)
            .Select(i => new UserGroupDto { Id = 100 + i, Name = $"LRT-UI-PGNEW-14494{i}R", IsActive = true, Permissions = new PermissionsDto() })
            .ToArray();
        var (view, window, console) = HostPermissions(groups, width: 900, height: 520);
        try
        {
            var target = console.Matrix.Groups[1];
            console.Matrix.SelectedGroup = target;
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            var grid = Find<DataGrid>(view, g => AutomationProperties.GetAutomationId(g) == "Console.Accounts.Matrix")!;

            Toggle(grid, 0);
            WaitTask(console.Matrix.ApplyAsync());
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            AssertPainted(window, grid, "저장 뒤");

            Toggle(grid, 1);
            console.Matrix.Revert();
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            AssertPainted(window, grid, "되돌리기 뒤");

            Toggle(grid, 1);
            console.Matrix.SelectedGroup = console.Matrix.Groups[0];
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            console.Matrix.Revert();
            AppHost.Pump(DispatcherPriority.ApplicationIdle);
            AssertPainted(window, grid, "칩 전환 시도 · 되돌리기 뒤");
        }
        finally { window.Close(); }
    });

    /// <summary>표의 n 번째 켤 수 있는 칸을 UIA Toggle 로 뒤집는다(실창 시험 ToggleCell 과 같은 길).</summary>
    private static void Toggle(DataGrid grid, int n)
    {
        var box = FindAll<CheckBox>(grid).Where(c => c.IsEnabled && c.IsVisible).ElementAt(n);
        var peer = UIElementAutomationPeer.CreatePeerForElement(box)!;
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
    }

    private static void AssertPainted(Window window, DataGrid grid, string when)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var blank = new List<string>();
        var viewport = grid.TranslatePoint(new Point(0, 0), content);
        foreach (var row in FindAll<DataGridRow>(grid))
        {
            if (row.Item is not ModulePermRowViewModel m || !row.IsVisible) continue;
            var top = row.TranslatePoint(new Point(0, 0), content).Y;
            if (top < viewport.Y + 36 || top + row.ActualHeight > viewport.Y + grid.ActualHeight) continue;
            foreach (var c in FindAll<CheckBox>(row))
                if (!Painted(bitmap, FindAll<Border>(c).First(b => b.Name == "Bd"), content)) blank.Add(AutomationProperties.GetAutomationId(c));
            var all = FindAll<Button>(row).FirstOrDefault(b => (b.Content as string) == "전체");
            if (all is null || !Painted(bitmap, all, content)) blank.Add(m.ModuleKey + ".전체");
        }
        Assert.True(blank.Count == 0, $"{when} — 그려지지 않은 칸: {string.Join(", ", blank)}");
    }

    /// <summary>요소 자리에 바탕과 다른 픽셀이 하나라도 있는가(테두리 · 글자). 바탕은 요소 바로 왼쪽 3px 의 픽셀.</summary>
    private static bool Painted(BitmapSource bitmap, FrameworkElement element, FrameworkElement root)
    {
        var origin = element.TranslatePoint(new Point(0, 0), root);
        int x0 = (int)origin.X, y0 = (int)origin.Y, w = (int)element.ActualWidth, h = (int)element.ActualHeight;
        if (w <= 0 || h <= 0) return false;
        var pixels = new int[w * h];
        bitmap.CopyPixels(new Int32Rect(x0, y0, w, h), pixels, w * 4, 0);
        var background = new int[1];
        bitmap.CopyPixels(new Int32Rect(Math.Max(0, x0 - 3), y0 + h / 2, 1, 1), background, 4, 0);
        return pixels.Any(p => p != background[0]);
    }

    private static (AccountConsolePanelView View, Window Window, TestAccountConsole Console) HostPermissions(IEnumerable<UserGroupDto> groups, double width, double height)
    {
        PlatformProvider.Current = new XamlPlatformProvider();
        IoC.BuildUp = _ => { };
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.GetInstance = (_, _) => null!;

        var list = groups.ToList();
        var (console, _, _, _, _) = ConsoleFixtures.Build(new[] { ConsoleFixtures.User(1, "op1", "김운영") }, list);
        WaitTask(console.ActivateForTestAsync());

        var view = new AccountConsolePanelView { Width = width, Height = height };
        ((IViewAware)console).AttachView(view);
        var host = new ContentControl();
        var window = AppHost.Show(host);
        View.SetModel(host, console);
        AppHost.Pump(DispatcherPriority.Loaded);
        AppHost.Pump();

        var rail = Find<ConsoleRail>(view, r => AutomationProperties.GetAutomationId(r) == "Console.Accounts.Rail")!;
        var peer = UIElementAutomationPeer.CreatePeerForElement(rail)!;
        var item = peer.GetChildren()!.OfType<ListBoxItemAutomationPeer>().First(p => (p.Item as ConsoleRailEntry)?.Key == AccountConsoleKeys.Permissions);
        ((ISelectionItemProvider)item.GetPattern(PatternInterface.SelectionItem)!).Select();

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (console.Matrix.Groups.Count < list.Count && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        return (view, window, console);
    }

    private static void WaitTask(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) AppHost.Pump(DispatcherPriority.Background);
        task.GetAwaiter().GetResult();
    }

    private static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
        => FindAll<T>(root).FirstOrDefault(match);

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in FindAll<T>(child)) yield return d;
        }
    }
}

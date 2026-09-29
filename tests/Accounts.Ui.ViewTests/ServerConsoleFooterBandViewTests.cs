using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using DeviceConsolePreview;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 서버 콘솔 바닥 — 2026-09-30 GIS 실창 020 "여기 레이아웃 좀 짤리지?":
/// ① 레일 바닥(합계 + 부대 + 콤보 + 예하 포함)이 띠보다 커 체크 상자가 콘솔 바닥에 붙었고,
/// ② 상태 띠의 배정 칩 칸(96)이 세 줄째를 칸 바닥에 붙여 잘라 보였고,
/// ③ 상세 적용 막대 윗선이 레일 · 상태 띠보다 75px 아래였다(세 칸 띠 윗선 어긋남).
/// 진짜 뷰 + 진짜 뷰모델(가짜 통로 · 칩 40개)을 호스트와 같은 사전으로 화면 밖에 띄워 잰다.
/// </summary>
public class ServerConsoleFooterBandViewTests
{
    [Theory]
    [InlineData(1280, 760, false, false)]
    [InlineData(1360, 800, false, false)]
    [InlineData(1600, 900, false, false)]
    [InlineData(1360, 800, true, false)]    // 한 대를 골라 지표 띠 + 상세 폼이 선 상태
    [InlineData(1360, 800, true, true)]     // 다크
    public void should_fit_every_bottom_band_and_align_their_tops_when_the_server_console_is_docked(double width, double height, bool select, bool dark)
    {
        AppHost.Run(() =>
        {
            using var host = CaliburnHost.Use();
            AppHost.SetDark(dark);
            var preview = new ServersPreview();
            var view = Wait(preview.BuildAsync(withData: true));
            view.Width = width;
            view.Height = height;
            var window = AppHost.Show(view);
            try
            {
                if (select)
                {
                    preview.Select(preview.Row("방송서버-01"));
                    AppHost.Pump();
                }
                Save(window, $"servers-{width}x{height}-{(select ? "selected" : "loaded")}-{(dark ? "dark" : "light")}.png");

                var shell = Descendants<ConsoleShell>(view).Single();
                var bands = shell.FooterBands.Where(b => b.IsVisible).ToList();
                Assert.Equal(3, bands.Count);                                           // 레일 바닥 · 상태 줄 · 상세 적용 막대

                // ③ 세 칸 띠 윗선이 1px 안에서 한 줄
                var tops = bands.Select(b => Bounds(b, shell).Top).ToList();
                Assert.True(tops.Max() - tops.Min() <= 1, $"띠 윗선 {string.Join(" / ", tops.Select(t => t.ToString("0.#")))}");

                // 띠가 콘솔 밖으로 밀려나 잘리지 않는다
                foreach (var band in bands)
                    Assert.True(Bounds(band, shell).Bottom <= shell.ActualHeight + 0.5, $"띠 바닥 {Bounds(band, shell).Bottom} > 콘솔 {shell.ActualHeight}");

                // ① 레일 바닥 · 상태 줄 — 내용이 띠 안에 들고 아래 여백이 4 이상
                AssertFitsInBand(ById(view, "Console.Servers.Rail.Footer"), bands, shell);
                AssertFitsInBand(ById(view, "Console.Servers.Status.Count"), bands, shell);

                // 부대 필터는 레일 바닥이 아니라 툴바 필터 줄에 있다(다른 콘솔과 같은 자리)
                var unitFilter = ById(view, "Console.Servers.UnitFilter");
                Assert.NotNull(Ancestor<ConsoleToolbar>(unitFilter));
                Assert.Null(Ancestor<ConsoleRail>(unitFilter));
                Assert.NotNull(Ancestor<ConsoleToolbar>(ById(view, "Console.Servers.IncludeDescendants")));

                // ② 배정 트레이 — 칩 두 줄이 온전히(줄 높이 × 2), 트레이 바닥과 상태 띠 사이 4 이상
                var tray = (ListBox)ById(view, "Console.Servers.Assign.Tray");
                Assert.True(tray.Items.Count > 8, "칩이 두 줄을 넘칠 만큼 있어야 잴 수 있다");
                Assert.Equal(ServerMonitorLayout.TrayMaxHeight, tray.ActualHeight, 1);
                var rows = tray.Items.Cast<object>()
                    .Select(i => tray.ItemContainerGenerator.ContainerFromItem(i) as FrameworkElement)
                    .Where(c => c is { IsVisible: true })
                    .Select(c => c!.ActualHeight).Distinct().ToList();
                Assert.All(rows, h => Assert.Equal(ServerMonitorLayout.TrayRowHeight, h, 0.5));
                var statusBand = bands.Single(b => IsAncestor(b, ById(view, "Console.Servers.Status.Count")));
                Assert.True(Bounds(statusBand, shell).Top - Bounds(tray, shell).Bottom >= 4,
                    $"트레이 바닥 {Bounds(tray, shell).Bottom:0.#} · 상태 띠 윗선 {Bounds(statusBand, shell).Top:0.#}");
            }
            finally
            {
                window.Close();
                AppHost.SetDark(false);
            }
        });
    }

    #region - 도우미 -
    private static void AssertFitsInBand(FrameworkElement content, IReadOnlyList<FrameworkElement> bands, FrameworkElement root)
    {
        var band = bands.Single(b => IsAncestor(b, content));
        var inner = Bounds(content, root);
        var outer = Bounds(band, root);
        var id = AutomationProperties.GetAutomationId(content);
        Assert.True(content.ActualHeight <= band.ActualHeight, $"{id}: 내용 {content.ActualHeight:0.#} > 띠 {band.ActualHeight:0.#}");
        Assert.True(inner.Top >= outer.Top - 0.5, $"{id}: 내용 윗변 {inner.Top:0.#} < 띠 윗선 {outer.Top:0.#}");
        Assert.True(outer.Bottom - inner.Bottom >= 4, $"{id}: 아래 여백 {outer.Bottom - inner.Bottom:0.#} < 4");
    }

    private static Rect Bounds(FrameworkElement element, Visual root)
        => element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject node)
    {
        for (var n = VisualTreeHelper.GetParent(node); n != null; n = VisualTreeHelper.GetParent(n))
            if (ReferenceEquals(n, ancestor)) return true;
        return false;
    }

    private static T? Ancestor<T>(DependencyObject node) where T : DependencyObject
    {
        for (var n = VisualTreeHelper.GetParent(node); n != null; n = VisualTreeHelper.GetParent(n))
            if (n is T hit) return hit;
        return null;
    }

    private static FrameworkElement ById(DependencyObject root, string id)
        => Descendants<FrameworkElement>(root).FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id)
           ?? throw new InvalidOperationException($"{id} 를 찾지 못했다");

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    /// <summary>비동기 작업을 Application 디스패처를 돌리며 기다린다(뷰모델 활성화 · 재적재).</summary>
    private static T Wait<T>(Task<T> task)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < until) AppHost.Pump(DispatcherPriority.Background);
        return task.GetAwaiter().GetResult();
    }

    /// <summary><c>SERVERS_BAND_SNAPSHOT_DIR</c> 가 있으면 PNG 를 남긴다(눈으로 대조용).</summary>
    private static void Save(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("SERVERS_BAND_SNAPSHOT_DIR");
        if (!string.IsNullOrEmpty(directory)) AppHost.Save(window, System.IO.Path.Combine(directory, name));
    }

    /// <summary>
    /// 호스트 부트스트래퍼가 해 주는 Caliburn 정적 배선(미리보기 도구와 같다) — 시험이 끝나면 되돌린다.
    /// </summary>
    private sealed class CaliburnHost : IDisposable
    {
        private readonly Func<Type, string, object> _getInstance = IoC.GetInstance;
        private readonly Func<Type, IEnumerable<object>> _getAll = IoC.GetAllInstances;
        private readonly Action<object> _buildUp = IoC.BuildUp;
        private readonly IPlatformProvider _platform = PlatformProvider.Current;

        public static CaliburnHost Use()
        {
            var saved = new CaliburnHost();
            IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? new EventAggregator() : null!;
            IoC.GetAllInstances = _ => Array.Empty<object>();
            IoC.BuildUp = _ => { };
            PlatformProvider.Current = new XamlPlatformProvider();
            return saved;
        }

        public void Dispose()
        {
            IoC.GetInstance = _getInstance;
            IoC.GetAllInstances = _getAll;
            IoC.BuildUp = _buildUp;
            PlatformProvider.Current = _platform;
        }
    }
    #endregion
}

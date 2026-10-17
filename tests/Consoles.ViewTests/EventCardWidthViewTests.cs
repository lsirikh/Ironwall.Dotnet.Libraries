using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Events.Ui.Views.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using MaterialDesignThemes.Wpf;
using Moq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 이벤트 카드 목록(셸 오른쪽 이벤트 칸, 폭 200~300) — 카드가 칸 폭을 따라가야 한다. 최소 폭(200)에서 잘리거나 가로 스크롤이 생기면 안 된다.
/// </summary>
/// <remarks>
/// 탐지 · 장애 카드의 앞 · 뒤 판이 <c>Width="220"</c> 고정이라, 셸 이벤트 칸을 최소 폭(ShellView 열 MinWidth 200)으로 줄이면
/// 목록 안쪽 폭(칸 − 항목 여백 − 세로 스크롤 막대)보다 카드가 넓어 오른쪽이 잘렸다. 실제 목록 뷰(<see cref="EventCardListPanelView"/>)에
/// 실제 카드 두 장을 넣어 화면 밖 창에 띄우고 재어 본다. 칸 사전(Console.* · MDIX)은 AppHost 가, 목록이 찾는 변환기 · 글꼴 키는
/// 호스트 ResourceDictionary 의 라이브러리 사본(Events.Ui Resources.xaml)이 준다 — 이 시험 동안만 앱 사전에 합친다.
/// <para>PNG 가 필요하면 <c>IRONWALL_CARDWIDTH_PNG_DIR</c> 에 폴더를 준다(없으면 저장하지 않는다).</para>
/// </remarks>
public class EventCardWidthViewTests
{
    private const string EventsUiResources = "pack://application:,,,/Ironwall.Dotnet.Libraries.Events.Ui;component/Resources/Resources.xaml";

    [Theory]
    [InlineData(200, false)]
    [InlineData(300, false)]
    [InlineData(200, true)]
    [InlineData(300, true)]
    public void should_fit_the_cards_to_the_panel_width_without_clipping_or_horizontal_scroll_when_the_event_panel_is_narrow_or_normal(double panelWidth, bool dark) => AppHost.Run(() =>
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var library = new ResourceDictionary { Source = new Uri(EventsUiResources, UriKind.Absolute) };
        merged.Add(library);
        Window? window = null;
        try
        {
            AppHost.SetDark(dark);
            var (panel, list) = HostPanel(panelWidth, out window);
            var dir = Environment.GetEnvironmentVariable("IRONWALL_CARDWIDTH_PNG_DIR");
            if (!string.IsNullOrWhiteSpace(dir))
                AppHost.Save(window, Path.Combine(dir, $"cards-{panelWidth:0}-{(dark ? "dark" : "light")}.png"));

            var scroll = RailProbe.Find<ScrollViewer>(list, _ => true)!;
            Assert.True(scroll is not null, "카드 목록의 스크롤 뷰어가 없다");
            var cards = new List<FrameworkElement>();
            cards.AddRange(Descendants<DetectionEventCardView>(list));
            cards.AddRange(Descendants<MalfunctionEventCardView>(list));
            Assert.Equal(2, cards.Count);

            var report = new List<string>();
            foreach (var card in cards)
            {
                var flipper = Descendants<Flipper>(card).First();
                var front = (FrameworkElement)((Border)flipper.FrontContent!).Child!;
                var bounds = front.TransformToAncestor(scroll).TransformBounds(new Rect(front.RenderSize));
                report.Add($"{card.GetType().Name}: 앞판 {front.ActualWidth:0.#} · 오른쪽 끝 {bounds.Right:0.#} / 보이는 폭 {scroll.ViewportWidth:0.#}"
                           + $" · 세로 막대 {scroll.ComputedVerticalScrollBarVisibility} · 배치 {Chain(front, scroll)}");
                Assert.True(bounds.Right <= scroll.ViewportWidth + 0.5,
                    $"[{panelWidth} · {(dark ? "다크" : "라이트")}] 카드 오른쪽이 목록 밖으로 나가 잘린다 · {string.Join(" | ", report)}");
                // 칸이 넉넉하면(보통 폭 300) 예전 220 모양 그대로, 좁으면 칸 안쪽 폭(항목 여백을 뺀 만큼)까지 줄어든다.
                Assert.True(front.ActualWidth <= 220.5 && front.ActualWidth >= Math.Min(220, scroll.ViewportWidth - 40) - 0.5,
                    $"[{panelWidth}] 카드가 칸 폭을 따라가지 않는다 · {string.Join(" | ", report)}");
            }
            Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 0.5 && scroll.ComputedHorizontalScrollBarVisibility != Visibility.Visible,
                $"[{panelWidth}] 가로 스크롤이 생겼다 · 내용 {scroll.ExtentWidth:0.#} / 보이는 폭 {scroll.ViewportWidth:0.#} · {string.Join(" | ", report)}");
        }
        finally
        {
            window?.Close();
            merged.Remove(library);
            AppHost.SetDark(false);
        }
    });

    /// <summary>실제 카드 목록 뷰에 탐지 · 장애 카드 한 장씩 — 셸 이벤트 칸처럼 폭을 고정해 화면 밖 창에 띄운다.</summary>
    private static (EventCardListPanelView Panel, ListBox List) HostPanel(double panelWidth, out Window window)
    {
        var ea = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        var api = new Mock<IEventApiService>().Object;
        var account = new Mock<IAccountModel>().Object;
        RailProbe.UseIoC(type =>
            type == typeof(IEventAggregator) ? ea
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? api
            : type == typeof(IAccountModel) ? account
            : type == typeof(IActionReportGuard) ? new ActionReportGuard()
            : null);
        if (!AssemblySource.Instance.Contains(typeof(EventCardListPanelView).Assembly))
            AssemblySource.Instance.Add(typeof(EventCardListPanelView).Assembly);
        PlatformProvider.Current = new XamlPlatformProvider();

        var vm = new EventCardListPanelViewModel(ea, log, null!, account, api,
            new Mock<ISymbolEventManager>().Object, new Mock<IEventQueueManager>().Object, new ActionReportGuard());
        vm.ViewModelProvider.Add(new DetectionEventCardViewModel(new DetectionEventDto
        {
            Id = 9101, TypeEvent = "Intrusion", Result = "THERMAL_SENSOR",
            Device = new BaseDeviceDto { Id = 100, TypeDevice = "Fence", NumberDevice = 12 },
            DeviceDescription = "제3구역 북측 철책 감지기 #12",
            CreatedAt = new DateTime(2026, 9, 30, 10, 21, 7).ToString("o"),
        }.ToDetectionEventModel()));
        vm.ViewModelProvider.Add(new MalfunctionEventCardViewModel(new MalfunctionEventDto
        {
            Id = 9102, TypeEvent = "Fault", Reason = "FAULT_CONTROLLER", ActionReported = "false",
            Device = new BaseDeviceDto { Id = 200, TypeDevice = "Controller", NumberDevice = 3 },
            DeviceDescription = "제3구역 함체 제어기 #3",
            CreatedAt = new DateTime(2026, 9, 30, 10, 22, 40).ToString("o"),
        }.ToMalfunctionEventModel()));

        var panel = new EventCardListPanelView { Width = panelWidth, Height = 420 };
        var host = new ContentControl();
        window = AppHost.Show(host);
        ((IViewAware)vm).AttachView(panel);
        View.SetModel(host, vm);
        AppHost.Pump(DispatcherPriority.Loaded);
        AppHost.Pump(DispatcherPriority.ApplicationIdle);
        var list = Descendants<ListBox>(panel).First(l => l.ItemsSource is not null);
        return (panel, list);
    }

    /// <summary>진단 — 카드 판에서 스크롤 뷰어까지 요소마다 (폭, 정렬, 내용 정렬). 어느 층이 내용 폭으로 줄이는지 보인다.</summary>
    private static string Chain(DependencyObject from, DependencyObject to)
    {
        var parts = new List<string>();
        for (var d = from; d is not null && d != to; d = VisualTreeHelper.GetParent(d))
            if (d is FrameworkElement fe) parts.Add($"{fe.GetType().Name}({fe.ActualWidth:0},{fe.HorizontalAlignment},{(fe is Control c ? c.HorizontalContentAlignment.ToString() : "-")})");
        return string.Join(" < ", parts);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }
}

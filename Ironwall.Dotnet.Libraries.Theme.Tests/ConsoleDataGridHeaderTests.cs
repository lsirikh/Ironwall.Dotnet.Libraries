using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// X2 — 실창에서 목록 머리에 콘솔 머리 스타일(흰 띠 · 아래 구분선 · 굵은 글자)이 안 먹고, 명시 <c>HeaderStyle</c> 을 준
/// 열에만 먹던 결함의 근본 원인을 헤드리스로 재현하고 잠근다.
/// </summary>
/// <remarks>
/// <para><b>근본 원인</b>: <see cref="DataGridColumnHeader"/> 의 <c>Style</c> 은 강제(coerce)로 정해진다 —
/// 머리 자신의 값 · 열의 <c>HeaderStyle</c> · 그리드의 <c>ColumnHeaderStyle</c> 중 <b>값의 출처(BaseValueSource)가
/// 가장 높은 것</b>이 이긴다(<c>DataGridHelper.GetCoercedTransferPropertyValue</c>). 앱이 MaterialDesign 기본 사전을
/// 병합하면 머리 자신이 <b>암시 스타일</b>(ImplicitStyleReference = 8)을 얻는다. 커널은 <c>ColumnHeaderStyle</c> 을
/// <b>스타일 Setter</b>(Style = 5)로 주고 있었으므로 늘 졌다. 열에 XAML 로 준 <c>HeaderStyle</c> 은 로컬 값(Local = 11)이라
/// 이겼다 — 그래서 명시한 열만 흰 띠였다. 미리보기 도구는 MDIX 기본 사전을 늦게 병합했거나 없어 재현되지 않았다.</para>
/// <para><b>고침</b>: <c>Console.DataGrid</c> 의 <c>Style.Resources</c> 에 <c>Console.DataGrid.ColumnHeader</c> 를 바탕으로 한
/// <b>암시 스타일</b>을 둔다 — 머리의 암시 스타일 탐색이 앱 사전보다 그리드 스타일 사전을 먼저 만난다(스크롤바를 이미 같은 방식으로 건다).</para>
/// </remarks>
[Collection(WpfRenderCollection.Name)]
public class ConsoleDataGridHeaderTests
{
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
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }

    private sealed record Row(string Module, string View);

    private static readonly Color HeaderBand = Color.FromRgb(0xFF, 0xFF, 0xFF);   // SurfaceAlt(라이트)
    private static readonly Color MdixBand = Color.FromRgb(0xF2, 0xF5, 0xF9);     // 실창 '모듈' 머리에서 잰 값(Surface)

    /// <summary>
    /// 실창과 같은 조건을 만든다: 바깥(앱) 사전에 MDIX 처럼 <b>암시</b> 열 머리 스타일이 있고, 그리드는 Console.DataGrid 를 쓴다.
    /// 첫 열은 HeaderStyle 없음, 둘째 열은 로컬 HeaderStyle(명시). 각 머리의 실제 Style · 바탕을 돌려준다.
    /// </summary>
    private static (Style? Plain, Style? Explicit, Color PlainBack, Style ConsoleHeader) Render()
    {
        // Application 인스턴스를 만들지 않는다 — 같은 시험 어셈블리의 테마 서비스 시험이 Application.Current 가 null 이라는
        // 전제로 돈다(인스턴스를 만들면 그 시험들이 다른 스레드의 Application 에 닿아 시험 호스트째 멈췄다 — 실측).
        // pack:// 체계 등록만 깨운다.
        _ = Application.Current;   // Application 의 정적 생성자가 pack:// 을 등록한다(인스턴스는 만들지 않는다)
        var console = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Styles.Console.xaml", UriKind.Absolute),
        };

        var mdixLike = new Style(typeof(DataGridColumnHeader));
        mdixLike.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(MdixBand)));

        var root = new Grid();
        root.Resources.MergedDictionaries.Add(console);
        root.Resources["SurfaceAltBrush"] = new SolidColorBrush(HeaderBand);
        root.Resources[typeof(DataGridColumnHeader)] = mdixLike;      // 앱 스코프 암시 스타일(MaterialDesign 기본 사전 흉내)

        var consoleHeader = (Style)console["Console.DataGrid.ColumnHeader"];
        var grid = new DataGrid { Style = (Style)console["Console.DataGrid"], ItemsSource = new[] { new Row("계정", "조회") } };
        grid.Columns.Add(new DataGridTextColumn { Header = "모듈", Binding = new System.Windows.Data.Binding(nameof(Row.Module)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "조회", Binding = new System.Windows.Data.Binding(nameof(Row.View)), HeaderStyle = consoleHeader });
        root.Children.Add(grid);

        root.Measure(new Size(600, 300));
        root.Arrange(new Rect(0, 0, 600, 300));
        root.UpdateLayout();

        var headers = Descendants<DataGridColumnHeader>(grid).Where(h => h.Column is not null).ToList();
        var plain = headers.First(h => Equals(h.Column.Header, "모듈"));
        var expl = headers.First(h => Equals(h.Column.Header, "조회"));
        var back = (plain.Background as SolidColorBrush)?.Color ?? Colors.Transparent;
        return (plain.Style, expl.Style, back, consoleHeader);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static bool IsOrDerivesFrom(Style? style, Style target)
    {
        for (var s = style; s is not null; s = s.BasedOn)
            if (ReferenceEquals(s, target)) return true;
        return false;
    }

    private static void EnsureApplication() { }

    [Fact]
    public void should_paint_the_console_header_band_on_a_column_without_header_style_when_the_app_has_an_implicit_header_style()
    {
        var (plain, _, back, consoleHeader) = OnSta(() => { EnsureApplication(); return Render(); });

        Assert.True(IsOrDerivesFrom(plain, consoleHeader),
            "HeaderStyle 을 안 준 열의 머리가 앱의 암시 스타일(MDIX)로 그려진다 — Console.DataGrid.ColumnHeader 가 아니다");
        Assert.Equal(HeaderBand, back);
    }

    [Fact]
    public void should_keep_an_explicit_column_header_style_when_a_column_sets_its_own()
    {
        var (_, expl, _, consoleHeader) = OnSta(() => { EnsureApplication(); return Render(); });

        Assert.Same(consoleHeader, expl);
    }
}

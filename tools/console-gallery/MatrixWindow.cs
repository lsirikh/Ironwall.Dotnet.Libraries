using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IoPath = System.IO.Path;

namespace ConsoleGallery;

/// <summary>
/// 상태 행렬 무대 — 손댄 스타일마다 normal · hover · pressed · focused · disabled 를 <b>한 장에</b> 늘어놓고
/// 라이트 · 다크 두 벌을 뜬다. 눈으로 보는 용도이자, 같이 떨어지는 좌표표(JSON)로 대비를 재는 용도다.
/// </summary>
/// <remarks>
/// hover · pressed · focused 는 읽기 전용 DP 라 입력 없이는 켤 수 없다. 그래서 여기서는 <b>리플렉션으로
/// DependencyPropertyKey 를 찾아 직접 켠다</b> — 도구 전용 꼼수이며 제품 코드에는 없다. 키를 못 찾으면
/// 그 칸은 기본 상태로 남고 좌표표에 <c>forced=false</c> 로 적힌다(조용히 통과하지 않게).
/// </remarks>
public sealed class MatrixWindow : Window
{
    private readonly StackPanel _root = new() { Margin = new Thickness(18) };
    private readonly List<(string Key, FrameworkElement Element, bool Forced)> _cells = new();

    public MatrixWindow()
    {
        Title = "콘솔 스타일 상태 행렬";
        Width = 1500;
        Height = 1000;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "BgBrush");
        _root.SetResourceReference(Panel.BackgroundProperty, "BgBrush");   // 투명 배경은 PNG 에서 검정이 된다
        Content = new ScrollViewer { Content = _root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Build();
    }

    /// <summary>행렬 루트 — 스냅샷은 이 시각 요소를 그린다.</summary>
    public FrameworkElement Stage => _root;

    #region - Build -
    private void Build()
    {
        Section("Console.Button", Row(
            Cell("button.normal", MakeButton("Console.Button", "저장"), null),
            Cell("button.hover", MakeButton("Console.Button", "저장"), "hover"),
            Cell("button.pressed", MakeButton("Console.Button", "저장"), "pressed"),
            Cell("button.focused", MakeButton("Console.Button", "저장"), "focus"),
            Cell("button.disabled", MakeButton("Console.Button", "저장", enabled: false), null)));

        Section("Console.Button.Primary", Row(
            Cell("primary.normal", MakeButton("Console.Button.Primary", "적용"), null),
            Cell("primary.hover", MakeButton("Console.Button.Primary", "적용"), "hover"),
            Cell("primary.pressed", MakeButton("Console.Button.Primary", "적용"), "pressed"),
            Cell("primary.focused", MakeButton("Console.Button.Primary", "적용"), "focus"),
            Cell("primary.disabled", MakeButton("Console.Button.Primary", "적용", enabled: false), null)));

        Section("Console.Button.Ghost / .Mini", Row(
            Cell("ghost.normal", MakeButton("Console.Button.Ghost", "갱신"), null),
            Cell("ghost.hover", MakeButton("Console.Button.Ghost", "갱신"), "hover"),
            Cell("ghost.focused", MakeButton("Console.Button.Ghost", "갱신"), "focus"),
            Cell("ghost.disabled", MakeButton("Console.Button.Ghost", "갱신", enabled: false), null),
            Cell("mini.normal", MakeButton("Console.Button.Mini", "M"), null),
            Cell("mini.disabled", MakeButton("Console.Button.Mini", "M", enabled: false), null)));

        Section("Console.SearchBox", Row(
            Cell("search.empty", MakeSearch(string.Empty), null, 220),
            Cell("search.text", MakeSearch("카메라"), null, 220),
            Cell("search.focused", MakeSearch("카메라"), "focus", 220),
            Cell("search.disabled", MakeSearch("카메라", enabled: false), null, 220)));

        Section("Console.CheckBox", Row(
            Cell("check.off", MakeCheck(false, true), null, 150),
            Cell("check.on", MakeCheck(true, true), null, 150),
            Cell("check.mixed", MakeCheck(null, true), null, 150),
            Cell("check.off.disabled", MakeCheck(false, false), null, 150),
            Cell("check.on.disabled", MakeCheck(true, false), null, 150),
            Cell("check.mixed.disabled", MakeCheck(null, false), null, 150),
            Cell("check.hover", MakeCheck(false, true), "hover", 150),
            Cell("check.focused", MakeCheck(true, true), "focus", 150)));

        Section("Console.ToggleSwitch", Row(
            Cell("toggle.off", MakeToggle(false, true), null, 110),
            Cell("toggle.on", MakeToggle(true, true), null, 110),
            Cell("toggle.off.disabled", MakeToggle(false, false), null, 110),
            Cell("toggle.on.disabled", MakeToggle(true, false), null, 110),
            Cell("toggle.focused", MakeToggle(true, true), "focus", 110)));

        Section("Console.ScrollBar", Row(
            Cell("scroll.normal", MakeScroller(), null, 240, 108),
            Cell("scroll.hover", MakeScroller("hover"), null, 240, 108),
            Cell("scroll.drag", MakeScroller("drag"), null, 240, 108)));

        Section("Console.Pill", Row(
            Cell("pill.plain", MakePill("Console.Pill", "대기", "TextMutedBrush"), null, 130),
            Cell("pill.normal", MakePill("Console.Pill.Normal", "정상", "StatusNormalBrush"), null, 130),
            Cell("pill.warning", MakePill("Console.Pill.Warning", "점검", "StatusWarningBrush"), null, 130),
            Cell("pill.critical", MakePill("Console.Pill.Critical", "장애", "StatusCriticalBrush"), null, 130),
            Cell("pill.info", MakePill("Console.Pill.Info", "안내", "StatusInfoBrush"), null, 130)));

        Section("ConsoleDetailHost — 적용 막대", Row(
            Cell("apply.clean", MakeDetail(dirty: false, readOnly: false), null, 360, 260),
            Cell("apply.dirty", MakeDetail(dirty: true, readOnly: false), null, 360, 260),
            Cell("apply.readonly", MakeDetail(dirty: false, readOnly: true), null, 360, 260)));

        // D-12 — 좁은 상세(최소 폭 300)에서 막대 문구가 쓸 수 있는 폭은 120 남짓이다. 버튼 둘이 자리를 먹기 때문이다.
        // 긴 한국어 한 문장(막힌 이동 안내)이 줄임표 없이 다 보이는지는 이 폭에서만 드러난다.
        Section("ConsoleDetailHost — 좁은 상세(300) 적용 막대 (D-12)", Row(
            Cell("narrow.clean", MakeDetail(dirty: false, readOnly: false, width: 300, footer: ConsoleDetailStateMachine.FooterText(ConsoleDetailState.Single, 0)), null, 324, 270),
            Cell("narrow.dirty", MakeDetail(dirty: true, readOnly: false, width: 300, footer: ConsoleDetailStateMachine.FooterText(ConsoleDetailState.Dirty, 2)), null, 324, 270),
            Cell("narrow.error", MakeDetail(dirty: true, readOnly: false, width: 300, footer: "적용하지 못했습니다 — 서버가 거절했습니다"), null, 324, 270),
            Cell("narrow.blocked", MakeDetail(dirty: true, readOnly: false, width: 300, footer: ConsoleDetailStateMachine.BlockedNotice), null, 324, 270)));

        // D-37 — 본문이 넘칠 때(세로 스크롤막대가 서는 상태) 마지막 잉크와 막대 윗선 사이에 숨 쉴 띠가 남는가.
        Section("ConsoleDetailHost — 넘치는 본문 (D-37)", Row(
            Cell("overflow.clean", MakeDetail(dirty: false, readOnly: false, width: 300, height: 250, footer: "변경 없음", overflow: true), null, 324, 270),
            Cell("overflow.dirty", MakeDetail(dirty: true, readOnly: false, width: 300, height: 250, footer: ConsoleDetailStateMachine.BlockedNotice, overflow: true), null, 324, 270),
            Cell("overflow.wide", MakeDetail(dirty: false, readOnly: false, width: 340, height: 250, footer: "변경 없음", overflow: true), null, 364, 270)));

        Section("ConsoleEmptyState (D-33)", Row(
            Cell("empty.plain", MakeEmpty(withAction: false), null, 360, 200),
            Cell("empty.action", MakeEmpty(withAction: true), null, 360, 200)));

        Section("토큰 — 구조선 · 행선", Row(
            Cell("token.border", MakeLine("BorderBrush", "SurfaceBrush"), null, 200, 62),
            Cell("token.divider", MakeLine("DividerBrush", "SurfaceAltBrush"), null, 200, 62),
            Cell("token.rowline", MakeLine("RowLineBrush", "SurfaceAltBrush"), null, 200, 62),
            Cell("token.searchplate", MakeLine("BorderBrush", "SurfaceSunkenBrush"), null, 200, 62)));
    }

    private void Section(string title, UIElement body)
    {
        var head = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6) };
        head.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        _root.Children.Add(head);
        _root.Children.Add(body);
    }

    private static UIElement Row(params UIElement[] cells)
    {
        var panel = new WrapPanel();
        foreach (var c in cells) panel.Children.Add(c);
        return panel;
    }

    /// <summary>한 칸 = 설명 한 줄 + 콘솔 바탕(SurfaceBrush) 위에 놓인 컨트롤. 재는 것은 컨트롤 자신의 사각이다.</summary>
    private UIElement Cell(string key, FrameworkElement control, string? force, double width = 170, double height = 56)
    {
        var caption = new TextBlock { Text = key, FontSize = 10.5, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(2, 0, 0, 3) };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");

        var plate = new Border { Width = width, Height = height, Padding = new Thickness(12, 0, 12, 0) };
        plate.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.VerticalAlignment = VerticalAlignment.Center;
        plate.Child = control;

        var forced = force == null || Force(control, force);
        _cells.Add((key, control, forced));

        return new StackPanel { Margin = new Thickness(0, 0, 10, 10), Children = { caption, plate } };
    }
    #endregion

    #region - Control factories -
    private static FrameworkElement MakeButton(string styleKey, string text, bool enabled = true)
    {
        var b = new Button { Content = text, IsEnabled = enabled };
        b.SetResourceReference(StyleProperty, styleKey);
        return b;
    }

    private static FrameworkElement MakeSearch(string text, bool enabled = true)
    {
        var t = new TextBox { Text = text, Width = 190, Tag = "장비번호·이름 검색", IsEnabled = enabled };
        t.SetResourceReference(StyleProperty, "Console.SearchBox");
        return t;
    }

    private static FrameworkElement MakeCheck(bool? state, bool enabled)
    {
        var c = new CheckBox { Content = "선택", IsThreeState = state == null, IsChecked = state, IsEnabled = enabled };
        c.SetResourceReference(StyleProperty, "Console.CheckBox");
        return c;
    }

    private static FrameworkElement MakeToggle(bool on, bool enabled)
    {
        var t = new ToggleButton { IsChecked = on, IsEnabled = enabled };
        t.SetResourceReference(StyleProperty, "Console.ToggleSwitch");
        return t;
    }

    private FrameworkElement MakeScroller(string? thumbState = null)
    {
        var inner = new StackPanel();
        for (var i = 0; i < 14; i++)
        {
            var line = new TextBlock { Text = $"항목 {i + 1}", FontSize = 12, Margin = new Thickness(6, 3, 6, 3) };
            line.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            inner.Children.Add(line);
        }
        var sv = new ScrollViewer { Width = 200, Height = 84, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, Content = inner };
        sv.SetResourceReference(StyleProperty, "Console.ScrollViewer");
        sv.SetResourceReference(Control.BackgroundProperty, "SurfaceAltBrush");
        if (thumbState != null)
            sv.Loaded += (_, _) =>
            {
                var thumb = FindDescendant<Thumb>(sv);
                if (thumb != null) Force(thumb, thumbState == "drag" ? "drag" : "hover");
            };
        return sv;
    }

    private static FrameworkElement MakePill(string styleKey, string text, string dotToken)
    {
        var dot = new Ellipse();
        dot.SetResourceReference(StyleProperty, "Console.Pill.Dot");
        dot.SetResourceReference(Shape.FillProperty, dotToken);
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        var pill = new Border { Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { dot, label } } };
        pill.SetResourceReference(StyleProperty, styleKey);
        return pill;
    }

    /// <summary>
    /// 상세 한 칸. <paramref name="overflow"/> 면 본문을 일부러 넘치게 채운다 — 세로 스크롤막대가 서고,
    /// 마지막 문단이 고정 적용 막대와 얼마나 떨어지는지(D-37)를 잴 수 있는 유일한 상태다.
    /// </summary>
    private static FrameworkElement MakeDetail(
        bool dirty, bool readOnly, double width = 340, double height = 240, string? footer = null, bool overflow = false)
    {
        var body = new StackPanel();
        var p = new TextBlock
        {
            Text = "이 값은 서버가 되돌려 준 그대로입니다. 아래 적용을 누르기 전에는 아무것도 보내지 않습니다.",
            FontSize = 12.5,
            TextWrapping = TextWrapping.WrapWithOverflow,
            Margin = new Thickness(0, 0, 0, 8),
        };
        p.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        body.Children.Add(p);

        if (overflow)
            for (var i = 0; i < 6; i++)
            {
                var more = new TextBlock
                {
                    Text = $"{i + 1}) 설치 위치와 담당 부대를 적어 둡니다. 여기에 적은 내용은 목록의 설명 열에도 같이 보입니다.",
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.WrapWithOverflow,
                    Margin = new Thickness(0, 0, 0, 8),
                };
                more.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                body.Children.Add(more);
            }

        var host = new ConsoleDetailHost
        {
            ConsoleKey = "Matrix",
            Kind = "카메라 · C-012",
            Title = "정문 카메라 1",
            Banner = readOnly ? "권한이 없어 읽기만 됩니다." : "등록 전에는 목록에 나타나지 않습니다.",
            IsReadOnly = readOnly,
            IsDirty = dirty,
            CanApply = dirty,
            CanRevert = dirty,
            ShowWidthTools = false,
            FooterText = footer ?? (dirty ? "변경 2건 미적용 — 적용하거나 되돌린 뒤 이동하세요" : "변경 없음"),
            Content = body,
            Width = width,
            Height = height,
        };
        host.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        return host;
    }

    private static FrameworkElement MakeEmpty(bool withAction)
    {
        var e = new ConsoleEmptyState
        {
            Title = "등록된 장비가 없습니다",
            Hint = "위의 등록으로 첫 장비를 추가하세요.",
            Width = 330,
            Height = 180,
        };
        if (withAction)
        {
            var b = new Button { Content = "장비 등록" };
            b.SetResourceReference(StyleProperty, "Console.Button.Primary");
            e.Action = b;
        }
        return e;
    }

    /// <summary>선 하나를 제 바탕 위에 놓은 표본 — 토큰 대비를 실제 렌더에서 잰다.</summary>
    private static FrameworkElement MakeLine(string lineToken, string plateToken)
    {
        var plate = new Border { Width = 170, Height = 40 };
        plate.SetResourceReference(Border.BackgroundProperty, plateToken);
        var line = new Rectangle { Height = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
        line.SetResourceReference(Shape.FillProperty, lineToken);
        plate.Child = line;
        return plate;
    }
    #endregion

    #region - Forced visual states (tool-only reflection) -
    /// <summary>읽기 전용 상태 DP 를 강제로 켠다. 성공하면 true.</summary>
    private static bool Force(DependencyObject target, string state)
    {
        var name = state switch
        {
            "hover" => "IsMouseOver",
            "pressed" => "IsPressed",
            "focus" => "IsKeyboardFocused",
            "drag" => "IsDragging",
            _ => null,
        };
        if (name == null) return false;

        var key = FindKey(target.GetType(), name);
        if (key == null) return false;
        target.SetValue(key, true);
        if (state == "focus" && target is UIElement ui) ui.SetValue(FindKey(typeof(UIElement), "IsKeyboardFocusWithin") ?? key, true);
        return true;
    }

    private static DependencyPropertyKey? FindKey(Type type, string property)
    {
        for (var t = type; t != null; t = t.BaseType!)
        {
            var f = t.GetField(property + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (f?.GetValue(null) is DependencyPropertyKey k) return k;
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            var deep = FindDescendant<T>(child);
            if (deep != null) return deep;
        }
        return null;
    }
    #endregion

    #region - Snapshot -
    /// <summary><c>--matrix &lt;폴더&gt;</c> — 라이트 · 다크 두 장 + 칸 좌표표(JSON).</summary>
    public async Task RunAsync(string directory, bool legacy = false)
    {
        Directory.CreateDirectory(directory);
        var prefix = legacy ? "legacy" : "matrix";
        if (legacy) Overlay(dark: false);
        await Settle();
        await Capture(directory, prefix + "-light");
        SwapTokens(dark: true);
        // 토큰 사전을 맨 뒤에 다시 붙였으므로 옛 값 사본도 다시 얹어야 한다 — 아니면 다크는 고친 값으로 찍힌다.
        if (legacy) Overlay(dark: true);
        await Settle();
        await Capture(directory, prefix + "-dark");
        SwapTokens(dark: false);
    }

    /// <summary>고치기 전 사본을 맨 위에 얹는다 — 같은 무대에서 before 를 재기 위해서다.</summary>
    private static void Overlay(bool dark)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
            if (merged[i].Source?.OriginalString.Contains("Legacy", StringComparison.Ordinal) == true)
                merged.RemoveAt(i);
        merged.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/ConsoleGallery;component/LegacyTokens.{(dark ? "Dark" : "Light")}.xaml") });
        merged.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/ConsoleGallery;component/LegacyStyles.xaml") });
    }

    private async Task Settle()
    {
        _root.UpdateLayout();
        await Task.Delay(320);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static void SwapTokens(bool dark)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(dark ? "Dark" : "Light")}.xaml"),
        });
        for (var i = merged.Count - 2; i >= 0; i--)
            if (merged[i].Source?.OriginalString.Contains("Tokens.", StringComparison.Ordinal) == true
                && !merged[i].Source.OriginalString.Contains("Shared", StringComparison.Ordinal))
            { merged.RemoveAt(i); break; }
    }

    private async Task Capture(string directory, string name)
    {
        await Settle();
        var width = _root.ActualWidth;
        var height = _root.ActualHeight;
        var dpi = VisualTreeHelper.GetDpi(_root);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            // TileBrush 기본 정렬은 Center 다 — 그대로 두면 내용이 가운데로 밀려 좌표표와 그림이 어긋난다(실측 x+80).
            dc.DrawRectangle(new VisualBrush(_root)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
            }, null, new Rect(0, 0, width, height));
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(IoPath.Combine(directory, name + ".png")))
            encoder.Save(file);

        // 좌표표 — 파이썬이 이 사각만 보고 잰다(눈대중 좌표 금지).
        var json = new StringBuilder("{\n  \"scale\": ").Append(dpi.DpiScaleX.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"cells\": {\n");
        var first = true;
        foreach (var (key, element, forced) in _cells)
        {
            if (!element.IsVisible) continue;
            var topLeft = element.TransformToAncestor(_root).Transform(new Point(0, 0));
            if (!first) json.Append(",\n");
            first = false;
            json.Append(CultureInfo.InvariantCulture, $"    \"{key}\": {{\"x\": {topLeft.X:0.##}, \"y\": {topLeft.Y:0.##}, \"w\": {element.ActualWidth:0.##}, \"h\": {element.ActualHeight:0.##}, \"forced\": {(forced ? "true" : "false")}}}");
        }
        json.Append("\n  }\n}\n");
        File.WriteAllText(IoPath.Combine(directory, name + ".json"), json.ToString(), new UTF8Encoding(true));
    }
    #endregion
}

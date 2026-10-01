using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;

/****************************************************************************
   Purpose      : 모니터 식별 카드 창 한 장 — 테두리 없음 · 반투명 · 맨 위 · 클릭 통과 · 초점 안 뺏음
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>떠 있는 식별 카드 하나 — 관리자(<see cref="MonitorIdentifyOverlay"/>)가 띄우고 닫는다.</summary>
public interface IMonitorIdentifySurface
{
    /// <summary>띄운다(활성화하지 않는다). <paramref name="animate"/> 면 서서히 나타난다.</summary>
    void Present(bool animate);

    /// <summary>닫는다. <paramref name="animate"/> 면 서서히 사라진 뒤 닫힌다. 여러 번 불러도 된다.</summary>
    void Dismiss(bool animate);

    /// <summary>이미 닫혔는가.</summary>
    bool IsClosed { get; }
}

/// <summary>
/// 식별 카드 창. 모양은 코드로 만든다(라이브러리 BAML 상대 URI 함정 회피) — 큰 번호 · 목록 글자 · 고른 모니터면 굵은 테두리 + "선택됨".
/// 색은 토큰을 <see cref="FrameworkElement.SetResourceReference"/> 로 잇는다(테마가 바뀌면 다시 풀린다).
/// </summary>
/// <remarks>
/// <para>배치: 창 손잡이를 먼저 만든 뒤 Win32 로 <b>이 스레드 좌표계</b>의 모니터 자리를 장치 이름으로 다시 찾아
/// 그 가운데에 놓는다(<see cref="MonitorIdentifyMath.PickBounds"/> · <see cref="MonitorIdentifyMath.CenteredWindow"/>).
/// 목록의 물리 픽셀을 그대로 쓰지 않는 까닭은 GIS 가 시스템 DPI 인지라 DPI 가 다른 모니터에서 같은 숫자가 다른 자리를 뜻하기 때문이다.</para>
/// <para>입력: <c>WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</c> + <see cref="UIElement.IsHitTestVisible"/> = false —
/// 마우스는 뒤 창으로 지나가고 설정 창의 초점은 그대로다.</para>
/// </remarks>
public sealed class MonitorIdentifyWindow : Window, IMonitorIdentifySurface
{
    private bool _closed;
    private bool _dismissing;

    public MonitorIdentifyWindow(MonitorIdentifyCard card, string automationPrefix)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        IsHitTestVisible = false;
        // 손잡이를 만드는 순간 어디에도 보이지 않게 화면 밖에서 시작한다 — 실제 자리는 Win32 로 놓는다.
        Left = -32000;
        Top = -32000;
        Width = MonitorIdentifyMath.WindowWidthDiu;
        Height = MonitorIdentifyMath.WindowHeightDiu;
        Opacity = 0;
        Title = card.Label;

        AutomationProperties.SetAutomationId(this, MonitorIdentifyMath.AutomationId(automationPrefix, card.Number));
        AutomationProperties.SetName(this, card.IsSelected ? $"{card.Label} · {MonitorIdentifyMath.SelectedText}" : card.Label);

        CardBorder = BuildCard(card, out var selectedTag);
        SelectedTag = selectedTag;
        Content = new Viewbox
        {
            // 창보다 카드가 크게 그려지는 좌표계(DPI 가 섞인 경우)에서도 잘리지 않고 줄어든다.
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = CardBorder,
        };

        SourceInitialized += (_, _) => MonitorIdentifyNative.ApplyClickThrough(Handle);
        Closed += (_, _) => _closed = true;
    }

    /// <summary>띄운 카드.</summary>
    public MonitorIdentifyCard Card { get; }

    /// <summary>카드 틀(테두리 두께 · 색으로 강조를 본다).</summary>
    public Border CardBorder { get; }

    /// <summary>"선택됨" 표지(고른 모니터가 아니면 <c>null</c>).</summary>
    public FrameworkElement? SelectedTag { get; }

    /// <summary>창 손잡이(만들기 전이면 0).</summary>
    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>지금 확장 스타일(손잡이가 없으면 0).</summary>
    public long ExtendedStyle => MonitorIdentifyNative.GetExStyle(Handle);

    public bool IsClosed => _closed;

    /// <summary>
    /// 창 손잡이만 만들고(보이지 않는다) 클릭 통과 스타일을 입힌다. 띄우기 전 준비 · 시험용.
    /// </summary>
    public IntPtr EnsureHandle() => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>이 스레드 좌표계에서 이 카드가 놓일 자리.</summary>
    public Int32Rect ComputePlacement()
    {
        var bounds = MonitorIdentifyMath.PickBounds(MonitorIdentifyNative.MonitorsInThreadSpace(), Card.DeviceName, Card.PhysicalBounds);
        var ppd = MonitorIdentifyMath.PixelsPerDiu(MonitorIdentifyNative.ThreadDpiMode(), MonitorIdentifyNative.SystemDpi(), Card.Dpi);
        return MonitorIdentifyMath.CenteredWindow(bounds, ppd);
    }

    public void Present(bool animate)
    {
        if (_closed) return;
        var rect = ComputePlacement();
        if (rect.IsEmpty)
        {
            Dismiss(animate: false);
            return;
        }

        var hwnd = EnsureHandle();
        MonitorIdentifyNative.PlaceTopmost(hwnd, rect);
        Show();                                       // ShowActivated=false → 활성화하지 않고 보인다(불투명도 0)
        MonitorIdentifyNative.PlaceTopmost(hwnd, rect);   // 보이는 동안 WPF 가 제 Left/Top 을 다시 써도 자리를 지킨다

        if (animate)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, MonitorIdentifyMath.ShownOpacity, MonitorIdentifyMath.FadeIn));
        else
            Opacity = MonitorIdentifyMath.ShownOpacity;
    }

    public void Dismiss(bool animate)
    {
        if (_closed) return;
        if (!animate || !IsVisible || Dispatcher.HasShutdownStarted)
        {
            BeginAnimation(OpacityProperty, null);
            Close();
            return;
        }

        if (_dismissing) return;
        _dismissing = true;
        var fade = new DoubleAnimation(Opacity, 0, MonitorIdentifyMath.FadeOut);
        fade.Completed += (_, _) => { if (!_closed) Close(); };
        BeginAnimation(OpacityProperty, fade);
    }

    #region - 모양 -
    private static Border BuildCard(MonitorIdentifyCard card, out FrameworkElement? selectedTag)
    {
        var number = new TextBlock
        {
            Text = MonitorIdentifyMath.NumberText(card.Number),
            FontSize = 120,
            FontWeight = FontWeights.Bold,
            LineHeight = 128,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        number.SetResourceReference(TextBlock.ForegroundProperty, card.IsSelected ? "PrimaryBrush" : "TextPrimaryBrush");

        var label = new TextBlock
        {
            Text = card.Label,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(number);
        stack.Children.Add(label);

        selectedTag = null;
        if (card.IsSelected)
        {
            var tagText = new TextBlock
            {
                Text = MonitorIdentifyMath.SelectedText,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
            };
            tagText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");

            var tag = new Border
            {
                Child = tagText,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 2, 10, 3),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            tag.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush");
            stack.Children.Add(tag);
            selectedTag = tag;
        }

        var border = new Border
        {
            Child = stack,
            MinWidth = 300,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(36, 18, 36, 22),
            BorderThickness = new Thickness(card.IsSelected ? MonitorIdentifyMath.SelectedOutline : MonitorIdentifyMath.NormalOutline),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        border.SetResourceReference(Border.BorderBrushProperty, card.IsSelected ? "PrimaryBrush" : "BorderBrush");
        return border;
    }
    #endregion
}

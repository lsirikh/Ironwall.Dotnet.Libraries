using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;

/// <summary>
/// 3등 신호등(fence-wiring-editor FR-14) — 왼 = 응답 없음 · 가운데 = 느림/일부 손실 · 오른 = 정상 · 모두 꺼짐 = 모름.
/// 색만이 아니라 <b>켜진 자리</b>로도 읽힌다. 색은 그릴 때마다 토큰에서 푼다(Frozen · 정적 브러시 없음).
/// peer 가 있다(<see cref="SignalLampAutomationPeer"/>) — 자동화가 AutomationId(<c>Devices.Wiring.Fence.Signal.{id}</c>)로 찾고 이름으로 상태를 읽는다.
/// </summary>
public sealed class SignalLamp : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(SignalLevel), typeof(SignalLamp),
        new FrameworkPropertyMetadata(SignalLevel.Unknown, FrameworkPropertyMetadataOptions.AffectsRender, OnLevelChanged));

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(object), typeof(SignalLamp), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public SignalLamp()
    {
        Width = 54;
        Height = 22;
        SetResourceReference(ThemeProbeProperty, "StatusNormalBrush");
        UpdateName();
    }

    public SignalLevel Level
    {
        get => (SignalLevel)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    private string _subject = "통신";

    /// <summary>이름 앞말(예: "제어기 통신" · "센서 3 통신") — 자동화 이름 = 앞말 + 상태.</summary>
    public string Subject
    {
        get => _subject;
        set { _subject = value ?? string.Empty; UpdateName(); }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SignalLamp)d).UpdateName();

    internal void UpdateName()
    {
        AutomationProperties.SetName(this, $"{Subject} {SignalMath.Text(Level)}");
        AutomationProperties.SetItemStatus(this, SignalMath.Text(Level));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        var body = Brush("SurfaceAltBrush", Colors.White);
        var edge = Brush("BorderBrush", Colors.Gray);
        dc.DrawRoundedRectangle(body, new Pen(edge, 1), new Rect(0.5, 0.5, w - 1, h - 1), h / 2, h / 2);

        var lit = SignalMath.LitIndex(Level);
        var r = h * 0.27;
        var off = Brush("TextMutedBrush", Colors.Gray);
        for (var i = 0; i < 3; i++)
        {
            var center = new Point(w * (0.22 + 0.28 * i), h / 2);
            if (i == lit)
            {
                var on = i switch
                {
                    0 => Brush("StatusCriticalBrush", Colors.Red),
                    1 => Brush("StatusWarningBrush", Colors.Orange),
                    _ => Brush("StatusNormalBrush", Colors.Green),
                };
                dc.DrawEllipse(on, new Pen(edge, 0.6), center, r, r);
            }
            else
            {
                dc.PushOpacity(0.35);
                dc.DrawEllipse(off, null, center, r, r);
                dc.Pop();
            }
        }
    }

    private Brush Brush(string key, Color fallback)
        => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    protected override AutomationPeer OnCreateAutomationPeer() => new SignalLampAutomationPeer(this);
}

/// <summary>신호등 peer — 이미지로 서고 이름(상태)을 낸다.</summary>
public sealed class SignalLampAutomationPeer : FrameworkElementAutomationPeer
{
    public SignalLampAutomationPeer(SignalLamp owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(SignalLamp);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Enums;
using MaterialDesignThemes.Wpf;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      : 추적 타겟 오버레이 마커 (물방울 핀 + 타입 아이콘 + 호버 툴팁)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// AiAnalysis TRACKING_STATUS 타겟 마커. transient(편집/영속 없음)라 ISymbolModel 결합을 피하고 <see cref="GMapMarker"/> 직접 파생.
/// <para><b>디자인</b>: 물방울 핀(<see cref="PackIconKind.MapMarker"/>, <b>색=위험도</b> severity hue-lock) 안에
/// 타입 아이콘(사람=Walk / 차=Car / 동물=Paw / 미상=HelpCircle, 흰색) + 진행방향 화살표.
/// <b>속도(m/s)는 핀 아래 상시 표시</b>, track_id는 <b>마우스 호버 툴팁</b>으로만.</para>
/// <para>핀 끝(tip)이 실제 좌표에 앵커. 생성·갱신은 UI Dispatcher에서만(매니저 보장).</para>
/// </summary>
public sealed class GMapTrackingMarker : GMapMarker
{
    private const double PinW = 36d;
    private const double PinH = 36d;
    private const double HeadX = 18d;   // 핀 머리 중심
    private const double HeadY = 14d;
    private const double TipX = 18d;     // 핀 끝(앵커)
    private const double TipY = 33d;

    private readonly PackIcon _pin;
    private readonly PackIcon _typeIcon;
    private readonly Polygon _arrow;
    private readonly RotateTransform _arrowRotate;
    private readonly TextBlock _speedLabel;

    public int CameraId { get; }
    public string TrackId { get; }

    public GMapTrackingMarker(int cameraId, string trackId, PointLatLng position) : base(position)
    {
        CameraId = cameraId;
        TrackId = trackId;

        var canvas = new Canvas { Width = PinW, Height = PinH };

        // 진행방향 화살표 — 머리 위에서 heading 방향으로 회전(orbit)
        _arrowRotate = new RotateTransform(0d, HeadX, HeadY);
        _arrow = new Polygon
        {
            Points = new PointCollection { new(HeadX, -4), new(HeadX - 4, 5), new(HeadX + 4, 5) },
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            StrokeThickness = 0.6,
            RenderTransform = _arrowRotate,
            Visibility = Visibility.Collapsed,
        };
        canvas.Children.Add(_arrow);

        // 물방울 핀(색=위험도)
        _pin = new PackIcon
        {
            Kind = PackIconKind.MapMarker,
            Width = PinW,
            Height = PinH,
            Foreground = BrushFor(EnumColorType.Gray),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 0.5,
            },
        };
        Canvas.SetLeft(_pin, 0);
        Canvas.SetTop(_pin, 0);
        canvas.Children.Add(_pin);

        // 타입 아이콘(흰색) — 핀 머리 중앙
        _typeIcon = new PackIcon
        {
            Kind = PackIconKind.HelpCircle,
            Width = 15,
            Height = 15,
            Foreground = Brushes.White,
        };
        Canvas.SetLeft(_typeIcon, HeadX - 7.5);
        Canvas.SetTop(_typeIcon, HeadY - 7.5);
        canvas.Children.Add(_typeIcon);

        // 속도: 핀 아래 상시 표시
        _speedLabel = new TextBlock
        {
            Text = "0.0 m/s",
            Foreground = Brushes.White,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            Width = 52,
            Background = new SolidColorBrush(Color.FromArgb(0xC0, 0, 0, 0)),
        };
        Canvas.SetLeft(_speedLabel, (PinW - 52) / 2);   // 핀 아래 중앙 정렬
        Canvas.SetTop(_speedLabel, PinH);               // 핀 끝 바로 아래
        canvas.Children.Add(_speedLabel);

        // track_id 는 호버 툴팁으로만
        canvas.ToolTip = trackId;

        Shape = canvas;
        Offset = new Point(-TipX, -TipY);   // 핀 끝이 좌표에 앵커
    }

    /// <summary>위협색·타입·방향·속도를 갱신(위치는 매니저가 <see cref="GMapMarker.Position"/> 직접 설정).</summary>
    public void Update(EnumColorType threatColor, EnumTargetType type, double bearing, bool showArrow, double speedMps)
    {
        _pin.Foreground = BrushFor(threatColor);
        _typeIcon.Kind = IconFor(type);
        _arrowRotate.Angle = bearing;
        _arrow.Visibility = showArrow ? Visibility.Visible : Visibility.Collapsed;
        _speedLabel.Text = $"{speedMps:F1} m/s";
    }

    /// <summary>lost/idle 등 흐릿 처리(제거 전 페이드).</summary>
    public void SetDimmed(bool dimmed) => ((Canvas)Shape).Opacity = dimmed ? 0.45 : 1.0;

    /// <summary>재생(Playback) 마커 식별 뱃지 — 라이브와 구분(핀 좌상단 ▶ PLAY).</summary>
    public void EnablePlaybackBadge()
    {
        var badge = new TextBlock
        {
            Text = "▶",
            Foreground = Brushes.White,
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromRgb(0xD3, 0x6B, 0xFF)),
            Padding = new Thickness(3, 0, 3, 0),
        };
        Canvas.SetLeft(badge, -2);
        Canvas.SetTop(badge, -2);
        ((Canvas)Shape).Children.Add(badge);
    }

    private static PackIconKind IconFor(EnumTargetType type) => type switch
    {
        EnumTargetType.Person => PackIconKind.Walk,
        EnumTargetType.Vehicle => PackIconKind.Car,
        EnumTargetType.Animal => PackIconKind.Paw,
        _ => PackIconKind.HelpCircle,
    };

    // severity hue-lock — 의미색은 테마 불변. NORMAL=Green/CAUTION=Orange/THREAT=Red/Unknown=Gray.
    private static SolidColorBrush BrushFor(EnumColorType color)
    {
        var b = color switch
        {
            EnumColorType.Green => new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
            EnumColorType.Orange => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)),
            EnumColorType.Red => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            _ => new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),
        };
        b.Freeze();
        return b;
    }
}

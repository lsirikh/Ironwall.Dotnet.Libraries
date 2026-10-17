using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 끌고 있는 것의 고스트 — 테두리 + 그림자 + 라벨 + N건 배지.
/// </summary>
/// <remarks>
/// <para>식별력을 배경색에 걸지 않는다(라이트에서 표면색 차이가 1.09:1) — 1.5px 주색 테두리로 구분한다.</para>
/// <para>토큰은 그릴 때마다 다시 해석한다. 한 번 해석해 두면 테마 전환 뒤 옛 색으로 굳는다.</para>
/// <para><c>AdornerManagerService</c> 에 등록하지 않는다 — 그 서비스의 주기 타이머가 배선을 깬 이력이 있다.</para>
/// <para>
/// 꾸미는 요소는 손잡이(작은 <see cref="Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragHandle"/>)라
/// 그 자체의 <see cref="Adorner.RenderSize"/> 로는 넘침을 재지 못한다 — 실제로 잘리는 경계는 이 어도너가
/// 얹힌 <see cref="AdornerLayer"/>(콘솔 전체) 의 가장자리다. 그래서 레이어를 넘겨받아 렌더할 때마다
/// 손잡이 기준 좌표로 가용 폭 · 높이를 다시 잰다(<see cref="DragMath.ClampGhostRect"/>).
/// </para>
/// </remarks>
public sealed class DragGhostAdorner : Adorner
{
    private readonly AdornerLayer? _layer;
    private readonly string _label;
    private readonly int _count;
    private Point _position;

    public DragGhostAdorner(UIElement adornedElement, AdornerLayer? layer, string label, int count) : base(adornedElement)
    {
        _layer = layer;
        _label = label;
        _count = count;
        IsHitTestVisible = false;      // 드롭존 HitTest 를 가리지 않는다
    }

    public void MoveTo(Point positionInAdorned)
    {
        if (_position == positionInAdorned) return;
        _position = positionInAdorned;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var surface = Brush("SurfaceAltBrush", Brushes.White);
        var primary = Brush("PrimaryBrush", Brushes.SteelBlue);
        var text = Brush("TextPrimaryBrush", Brushes.Black);
        var onPrimary = Brush("OnPrimaryBrush", Brushes.White);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var face = new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        var label = new FormattedText(_label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 12.5, text, dpi)
        {
            MaxTextWidth = 220,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        FormattedText? badge = _count > 1
            ? new FormattedText($"{_count}건", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 11.5, onPrimary, dpi)
            : null;

        const double padX = 10, padY = 6, gap = 8, badgePadX = 6;
        var badgeWidth = badge == null ? 0 : badge.Width + badgePadX * 2 + gap;
        var size = new Size(label.Width + padX * 2 + badgeWidth, Math.Max(label.Height, 16) + padY * 2);
        var rect = DragMath.ClampGhostRect(_position, size, AvailableBounds());

        // 그림자(형태) — DropShadowEffect 는 어도너 전체를 비트맵으로 만들어 RDP 에서 무겁다. 어두운 사각형 두 겹으로 흉내 낸다.
        var shadow = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0));
        dc.DrawRoundedRectangle(shadow, null, new Rect(rect.X + 1, rect.Y + 3, rect.Width, rect.Height), 5, 5);
        dc.DrawRoundedRectangle(shadow, null, new Rect(rect.X + 2, rect.Y + 6, rect.Width, rect.Height), 5, 5);

        dc.DrawRoundedRectangle(surface, new Pen(primary, 1.5), rect, 4, 4);
        dc.DrawText(label, new Point(rect.X + padX, rect.Y + (rect.Height - label.Height) / 2));

        if (badge != null)
        {
            var b = new Rect(rect.Right - padX - badge.Width - badgePadX * 2, rect.Y + (rect.Height - badge.Height - 2) / 2, badge.Width + badgePadX * 2, badge.Height + 2);
            dc.DrawRoundedRectangle(primary, null, b, 8, 8);
            dc.DrawText(badge, new Point(b.X + badgePadX, b.Y + 1));
        }
    }

    private Brush Brush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    /// <summary>
    /// 꾸미는 요소(<see cref="Adorner.AdornedElement"/>) 기준 좌표계에서, 실제로 잘리는 경계(레이어 가장자리)의 사각.
    /// 원점이 음수일 수 있다 — 레이어는 꾸미는 요소의 왼쪽 · 위쪽으로도 펼쳐져 있다. 레이어를 못 찾거나 변환이 없으면 무한 — 클램프하지 않는다.
    /// </summary>
    /// <remarks>
    /// 원점과 크기를 <b>같은 변환</b>으로 옮긴다(레이어 사각 전체를 꾸미는 요소 좌표로). 원점만 옮기고 크기는 레이어 단위로 두면
    /// 사이에 배율 변환이 있을 때 경계가 배율만큼 어긋난다. 공개는 시험 · 진단용 읽기다.
    /// </remarks>
    public Rect AvailableBounds()
    {
        if (_layer == null) return new Rect(0, 0, double.PositiveInfinity, double.PositiveInfinity);

        var toAdorned = _layer.TransformToVisual(AdornedElement);
        if (toAdorned == null) return new Rect(0, 0, double.PositiveInfinity, double.PositiveInfinity);
        return toAdorned.TransformBounds(new Rect(new Point(0, 0), _layer.RenderSize));
    }
}

/// <summary>
/// 삽입 위치 — 행 사이 <b>가로</b> 2px 선. 선택 표시(좌측 3px 세로 바)와 축이 직교해 색 없이도 구분된다.
/// </summary>
/// <remarks>컨테이너에 로컬 값을 쓰지 않는다 — 스타일 트리거를 이기고, 레이아웃을 흔들고, 컨테이너 재활용 때 다른 행을 따라간다.</remarks>
public sealed class InsertionLineAdorner : Adorner
{
    private double _y = double.NaN;

    public InsertionLineAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    /// <summary>선의 y(꾸미는 요소 좌표). <see cref="double.NaN"/> 이면 숨긴다. 값이 바뀔 때만 다시 그린다.</summary>
    public void MoveTo(double y)
    {
        if (_y.Equals(y)) return;
        _y = y;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (double.IsNaN(_y)) return;

        var primary = TryFindResource("PrimaryBrush") as Brush ?? Brushes.SteelBlue;
        var width = AdornedElement.RenderSize.Width;
        var y = Math.Max(1, Math.Min(AdornedElement.RenderSize.Height - 1, _y));

        dc.DrawLine(new Pen(primary, 2), new Point(6, y), new Point(Math.Max(6, width - 2), y));
        dc.DrawEllipse(primary, null, new Point(4, y), 3.5, 3.5);     // 머리 — 선이 어디서 시작하는지 형태로 보인다
    }
}

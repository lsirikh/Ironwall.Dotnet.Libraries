using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : Shift+드래그 러버밴드 마퀴 오버레이 — 맵 AdornerLayer(이미지·마커 위)에
                  반투명 채움+점선 외곽 렌더. 기존 OnRender 마퀴는 자식 마커/이미지 아래라
                  오버레이 이미지에 가려지는 문제 → adorner로 더 높은 레이어에서 렌더.
   Note         : AdornerManager 미사용(GMapCustomControl가 러버밴드 중에만 부착·제거, 5분 정리타이머 무관).
                  click-through(IsHitTestVisible=false — 캡처/히트는 컨트롤이 담당).
                  좌표=컨트롤 로컬(제공된 Rect 그대로 — 컨트롤 OnRender와 동일 공간).
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class RubberBandAdorner : Adorner
{
    private static readonly Brush _fill = Frozen(new SolidColorBrush(Color.FromArgb(40, 0, 170, 255)));
    private static readonly Pen _pen = FrozenDashPen(Color.FromArgb(210, 0, 170, 255), 1.5d);

    private Rect? _rect;
    private string? _label;
    private bool _warn;

    public RubberBandAdorner(UIElement map) : base(map)
    {
        IsHitTestVisible = false;   // 시각 전용 — 입력 투과(러버밴드 캡처는 컨트롤)
    }

    /// <summary>마퀴 사각형 갱신(컨트롤 로컬 좌표). null=숨김.</summary>
    public void Update(Rect? rect) => Update(rect, null, false);

    /// <summary>마퀴 사각형 + 오른쪽 아래 크기 글(3D 영역 그리기). warn = 경고색(StatusWarningBrush — 2 km 초과).</summary>
    public void Update(Rect? rect, string? label, bool warn) { _rect = rect; _label = label; _warn = warn; InvalidateVisual(); }

    protected override void OnRender(DrawingContext dc)
    {
        if (_rect is not Rect r || r.Width <= 0 || r.Height <= 0) return;
        if (string.IsNullOrEmpty(_label))
        {
            dc.DrawRectangle(_fill, _pen, r);
            return;
        }

        // 크기 글 — 토큰을 그릴 때마다 다시 읽는다(라이트/다크). 경고면 테두리도 같은 경고색.
        var accent = Token(_warn ? "StatusWarningBrush" : "PrimaryBrush") ?? _pen.Brush;
        var pen = _warn ? new Pen(accent, 2d) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) } : _pen;
        dc.DrawRectangle(_fill, pen, r);

        var text = new FormattedText(_label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            12d, accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var box = new Rect(Math.Max(r.Left, r.Right - text.Width - 12d), r.Bottom + 4d, text.Width + 12d, text.Height + 6d);
        dc.DrawRoundedRectangle(Token("SurfaceBrush") ?? Brushes.Black, new Pen(accent, 1d), box, 3d, 3d);
        dc.DrawText(text, new Point(box.Left + 6d, box.Top + 3d));
    }

    private Brush? Token(string key) => (AdornedElement as FrameworkElement)?.TryFindResource(key) as Brush;

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FrozenDashPen(Color c, double t)
    { var p = new Pen(Frozen(new SolidColorBrush(c)), t) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; p.Freeze(); return p; }
}

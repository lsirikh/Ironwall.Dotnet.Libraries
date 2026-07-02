using System;
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

    public RubberBandAdorner(UIElement map) : base(map)
    {
        IsHitTestVisible = false;   // 시각 전용 — 입력 투과(러버밴드 캡처는 컨트롤)
    }

    /// <summary>마퀴 사각형 갱신(컨트롤 로컬 좌표). null=숨김.</summary>
    public void Update(Rect? rect) { _rect = rect; InvalidateVisual(); }

    protected override void OnRender(DrawingContext dc)
    {
        if (_rect is Rect r && r.Width > 0 && r.Height > 0)
            dc.DrawRectangle(_fill, _pen, r);
    }

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FrozenDashPen(Color c, double t)
    { var p = new Pen(Frozen(new SolidColorBrush(c)), t) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; p.Freeze(); return p; }
}

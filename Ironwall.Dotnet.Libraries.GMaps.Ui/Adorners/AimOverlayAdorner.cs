using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 카메라 "특정 위치 확인/회전" 반경 오버레이 — 심볼·이미지 위(AdornerLayer)에
                  점선 반경원 + 등장 애니(r=0→Max) + 클릭 지점 주황 리플. (Camera_Aim_Overlay_Animation FR-AIM-01~05)
   Note         : AdornerManager 미사용(정리타이머 회피). 순수 시각 오버레이 → IsHitTestVisible=false(클릭스루, 불변식 #3).
                  좌표는 매 렌더 FromLatLngToLocal 재계산(팬/줌 추종), 디지털줌은 AdornerLayer가 자동 스케일(inner공간).
   Created On   : 2026-07-02
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class AimOverlayAdorner : Adorner, IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ILogService? _log;

    private readonly Pen _edgePen;    // 흰 점선 반경 외곽(기존 _aimEdgePen 스타일 {4,3})
    private readonly Pen _crossPen;   // 중심 십자
    private readonly CubicEase _ease = new() { EasingMode = EasingMode.EaseOut };

    // 등장 애니(r=0→목표)
    private double _growFraction = 1.0;      // 1=완전표시(애니 미사용 기본)
    private bool _growActive;
    private TimeSpan _growStart = TimeSpan.MinValue;
    private static readonly TimeSpan GrowDuration = TimeSpan.FromMilliseconds(420);

    // 주황 리플(클릭 지점 확대+페이드, 1회성)
    private bool _rippleActive;
    private double _rippleFraction;
    private PointLatLng _rippleCenter;
    private TimeSpan _rippleStart = TimeSpan.MinValue;
    private static readonly TimeSpan RippleDuration = TimeSpan.FromMilliseconds(650);
    private const double RippleMaxRadiusPx = 46d;

    private bool _renderHooked;
    private bool _disposed;

    /// <summary>리플 1회 재생 완료 시 발화 — 전이(transient) 리플 인스턴스를 컨트롤이 제거·Dispose 하도록.</summary>
    public event Action? RippleCompleted;

    public AimOverlayAdorner(GMapCustomControl map, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;

        IsHitTestVisible = false;   // 클릭스루 — aim 클릭은 컨트롤(OnMouseLeftButtonDown)이 처리(불변식 #3)

        _edgePen = new Pen(new SolidColorBrush(Colors.White), 2d) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        _crossPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1.5d);

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(팬/줌 추종, LineDrawingAdorner 패턴)
        _map.OnMapDrag += OnMapChanged;
        _map.SubscribeViewport(OnViewportSnapshot);   // 회전 통지(R-17) — Dispose서 해제
    }

    private void OnMapChanged() => InvalidateVisual();
    private void OnViewportSnapshot(GMapCustoms.MapViewportSnapshot _) => InvalidateVisual();

    /// <summary>반경 등장 애니 시작(r=0→목표, CubicEaseOut ~420ms).</summary>
    public void StartGrowIn()
    {
        _growFraction = 0d;
        _growActive = true;
        _growStart = TimeSpan.MinValue;
        HookRendering();
        InvalidateVisual();
    }

    /// <summary>클릭 지점 주황 리플 1회 재생(확대+페이드). 전이 인스턴스면 완료 시 <see cref="RippleCompleted"/> 발화.</summary>
    public void TriggerRipple(PointLatLng center)
    {
        _rippleCenter = center;
        _rippleFraction = 0d;
        _rippleActive = true;
        _rippleStart = TimeSpan.MinValue;
        HookRendering();
        InvalidateVisual();
    }

    private void HookRendering()
    {
        if (_renderHooked || _disposed) return;
        CompositionTarget.Rendering += OnRendering;
        _renderHooked = true;
    }

    private void UnhookRendering()
    {
        if (!_renderHooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _renderHooked = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = (e as RenderingEventArgs)?.RenderingTime ?? TimeSpan.Zero;
        bool rippleJustCompleted = false;

        if (_growActive)
        {
            if (_growStart == TimeSpan.MinValue) _growStart = now;
            double t = (now - _growStart).TotalMilliseconds / GrowDuration.TotalMilliseconds;
            if (t >= 1d) { _growFraction = 1d; _growActive = false; }
            else _growFraction = _ease.Ease(Math.Max(0d, t));
        }

        if (_rippleActive)
        {
            if (_rippleStart == TimeSpan.MinValue) _rippleStart = now;
            double t = (now - _rippleStart).TotalMilliseconds / RippleDuration.TotalMilliseconds;
            if (t >= 1d) { _rippleActive = false; _rippleFraction = 1d; rippleJustCompleted = true; }
            else _rippleFraction = _ease.Ease(Math.Max(0d, t));
        }

        InvalidateVisual();

        if (!_growActive && !_rippleActive) UnhookRendering();
        if (rippleJustCompleted) RippleCompleted?.Invoke();   // lock 밖 발화(재진입 방지)
    }

    protected override void OnRender(DrawingContext dc)
    {
        try
        {
            var centerN = _map.AimOverlayCenter;

            // 반경원 — aim 모드 중에만(리플 전이 인스턴스는 IsTargetAimMode=false라 건너뜀)
            if (_map.IsTargetAimMode && centerN.HasValue && _map.AimOverlayRadiusMeters > 0d)
            {
                var center = centerN.Value;
                double fullR = _map.MetersToScreenPixels(_map.AimOverlayRadiusMeters, center);
                if (fullR > 0d && !double.IsNaN(fullR) && !double.IsInfinity(fullR))
                {
                    var cp = _map.FromLatLngToLocal(center);
                    var cpt = new Point(cp.X, cp.Y);
                    double r = fullR * _growFraction;
                    if (r > 0.5d) dc.DrawEllipse(null, _edgePen, cpt, r, r);
                    if (_growFraction > 0.98d)   // 십자는 등장 완료 후
                    {
                        const double cross = 7d;
                        dc.DrawLine(_crossPen, new Point(cpt.X - cross, cpt.Y), new Point(cpt.X + cross, cpt.Y));
                        dc.DrawLine(_crossPen, new Point(cpt.X, cpt.Y - cross), new Point(cpt.X, cpt.Y + cross));
                    }
                }
            }

            // 주황 리플 — aim 모드와 독립(클릭 후 exit 이후에도 재생)
            if (_rippleActive || _rippleFraction is > 0d and < 1d)
            {
                var rp = _map.FromLatLngToLocal(_rippleCenter);
                var rpt = new Point(rp.X, rp.Y);
                double rr = RippleMaxRadiusPx * _rippleFraction;
                if (rr > 0.5d)
                {
                    byte alpha = (byte)Math.Clamp(0xCC * (1d - _rippleFraction), 0d, 255d);
                    var ripplePen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0x88, 0x00)), 3d);
                    dc.DrawEllipse(null, ripplePen, rpt, rr, rr);   // 애니 알파라 프레임 펜 생성(frozen 회피)
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"AimOverlayAdorner 렌더 실패: {ex.Message}");
        }
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) => null!;   // 클릭스루

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnhookRendering();
        _map.OnMapZoomChanged -= OnMapChanged;   // 이벤트 루팅 누수 방지(§2)
        _map.OnMapDrag -= OnMapChanged;
        _map.UnsubscribeViewport(OnViewportSnapshot);
        RippleCompleted = null;
    }
}

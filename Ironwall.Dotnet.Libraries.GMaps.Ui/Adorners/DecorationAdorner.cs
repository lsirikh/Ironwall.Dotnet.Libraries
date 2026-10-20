using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/// <summary>「꾸미기」 표면이 부르는 쪽(MapViewModel) — 그릴 것 · 마우스.</summary>
public interface IDecorationHost
{
    /// <summary>그 영역의 꾸미기 항목.</summary>
    IReadOnlyList<Map3DDecorationItem> DecorationItems { get; }
    /// <summary>판에서 고른 항목 uid(굵게 그린다).</summary>
    string? DecorationSelectedUid { get; }
    /// <summary>지금 도구(null = 고르기).</summary>
    DecoTool? DecorationTool { get; }
    /// <summary>찍고 있는 점(위경도).</summary>
    IReadOnlyList<UbPoint> DecorationDraftPoints { get; }
    void DecorationPointerDown(UbPoint screen, int clickCount);
    void DecorationRightClick(UbPoint screen);
}

/****************************************************************************
   Purpose      : 「꾸미기」 지도 표면 — 지도 위를 덮어 왼쪽 클릭 · 오른쪽 클릭을 받아 MapViewModel 로 넘기고, 꾸미기 항목을 단순 도형으로 그린다
                  (나무 = 초록 점 · 줄 · 영역 윤곽, 바닥 = 재질 색 채움, 소품 = 점 + 방향 막대, 담장 = 굵은 선) + 찍고 있는 점 · 다음 선 미리보기.
   Note         : 「건물 그리기」 표면(UserBuildingAdorner)과 같은 지오앵커 재렌더 · 휠은 지도로. 지도 위 끌기는 없다(옮기기는 [다시 놓기]).
                  재질 · 나무 색은 자료 색(고정), 고름 · 그리는 선은 테마 토큰(PrimaryBrush · SurfaceAltBrush).
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class DecorationAdorner : GeoSurfaceAdorner
{
    private readonly IDecorationHost _host;
    private Point? _hover;

    public DecorationAdorner(GMapControl map, IDecorationHost host, ILogService? log = null) : base(map, log) => _host = host;

    private Point Screen(UbPoint geo) => ScreenPoint(geo);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _map.Focus();
        _host.DecorationPointerDown(At(e), e.ClickCount);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _hover = e.GetPosition(_map);
        if (_host.DecorationDraftPoints.Count > 0) InvalidateVisual();   // 다음 선 미리보기
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        _host.DecorationRightClick(At(e));
        e.Handled = true;
    }

    /// <summary>바닥 재질 기본 색(2D 표시용).</summary>
    private static Color SurfaceColor(string? surface) => surface switch
    {
        "asphalt" => Color.FromRgb(0x55, 0x55, 0x55),
        "concrete" => Color.FromRgb(0xA9, 0xA9, 0xA9),
        "gravel" => Color.FromRgb(0x9E, 0x95, 0x84),
        "water" => Color.FromRgb(0x4A, 0x90, 0xC8),
        "sand" => Color.FromRgb(0xE0, 0xCC, 0x8F),
        "paving" => Color.FromRgb(0xB8, 0xA4, 0x8A),
        _ => Color.FromRgb(0x6A, 0xA8, 0x4F),
    };

    private static Color? Hex(string? s)
    {
        if (Map3DLook.NormalizeColor(s) is not { } h) return null;
        return Color.FromRgb(Convert.ToByte(h.Substring(1, 2), 16), Convert.ToByte(h.Substring(3, 2), 16), Convert.ToByte(h.Substring(5, 2), 16));
    }

    private static readonly Color TreeColor = Color.FromRgb(0x2E, 0x8B, 0x3E), PropColor = Color.FromRgb(0xE6, 0x91, 0x38), WallColor = Color.FromRgb(0x7A, 0x5C, 0x3A);

    protected override void OnRender(DrawingContext dc)
    {
        if (_disposed) return;
        try
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(new Size(_map.ActualWidth, _map.ActualHeight)));   // 히트 받는 바탕
            var primary = Resolve("PrimaryBrush", Color.FromRgb(0x22, 0xB8, 0xD9));
            var core = Resolve("SurfaceAltBrush", Color.FromRgb(0x1E, 0x28, 0x32));
            foreach (var it in _host.DecorationItems) DrawItem(dc, it, it.Uid == _host.DecorationSelectedUid, primary);

            var pts = _host.DecorationDraftPoints;
            if (pts.Count == 0) return;
            var line = new Pen(primary, 2.5) { LineJoin = PenLineJoin.Round }; line.Freeze();
            DrawPath(dc, pts.Select(Screen).ToList(), closed: false, null, line);
            if (_hover is { } h && _host.DecorationTool is { } t && Map3DDecoration.GeometryOf(t) != Map3DDecoration.GeoPoint)
            {
                var dashed = new Pen(primary, 1.5) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) }; dashed.Freeze();
                dc.DrawLine(dashed, Screen(pts[^1]), h);
                if (Map3DDecoration.GeometryOf(t) == Map3DDecoration.GeoPolygon && pts.Count >= 2) dc.DrawLine(dashed, h, Screen(pts[0]));
            }
            var vPen = new Pen(primary, 2); vPen.Freeze();
            foreach (var p in pts) dc.DrawEllipse(core, vPen, Screen(p), 4.5, 4.5);
        }
        catch (Exception ex) { _log?.Error($"DecorationAdorner 렌더 실패: {ex.Message}"); }
    }

    private void DrawItem(DrawingContext dc, Map3DDecorationItem it, bool selected, Brush primary)
    {
        var s = it.Points.Select(Screen).ToList();
        if (s.Count == 0) return;
        Color baseColor = it.Kind switch
        {
            Map3DDecoration.KindGround => Hex(it.Props.Value<string?>("color")) ?? SurfaceColor(it.Props.Value<string?>("surface")),
            Map3DDecoration.KindWall => Hex(it.Props.Value<string?>("color")) ?? WallColor,
            Map3DDecoration.KindProp => PropColor,
            _ => TreeColor,
        };
        var solid = new SolidColorBrush(baseColor); solid.Freeze();
        var fill = new SolidColorBrush(Color.FromArgb(0x66, baseColor.R, baseColor.G, baseColor.B)); fill.Freeze();
        // 고른 항목 — 색이 아니라 모양으로도: 테마 강조색의 굵은 외곽
        Pen? halo = selected ? new Pen(primary, 3) { LineJoin = PenLineJoin.Round } : null;
        halo?.Freeze();
        if (it.Geometry == Map3DDecoration.GeoPoint)
        {
            if (halo != null) dc.DrawEllipse(null, halo, s[0], 9, 9);
            dc.DrawEllipse(solid, null, s[0], it.Kind == Map3DDecoration.KindProp ? 4.5 : 5.5, it.Kind == Map3DDecoration.KindProp ? 4.5 : 5.5);
            if (it.Kind == Map3DDecoration.KindProp) DrawHeading(dc, it, s[0], solid);
            return;
        }
        bool closed = it.Geometry == Map3DDecoration.GeoPolygon;
        if (halo != null) DrawPath(dc, s, closed, null, halo);
        var pen = it.Kind == Map3DDecoration.KindWall ? new Pen(solid, 3.5) : closed ? new Pen(solid, 1.5) : new Pen(solid, 2) { DashStyle = new DashStyle(new double[] { 1, 2 }, 0) };
        pen.Freeze();
        DrawPath(dc, s, closed, closed && it.Kind == Map3DDecoration.KindGround ? fill : closed ? new SolidColorBrush(Color.FromArgb(0x33, baseColor.R, baseColor.G, baseColor.B)) : null, pen);
        if (it.Kind == Map3DDecoration.KindTree) foreach (var p in s) dc.DrawEllipse(solid, null, p, 3, 3);   // 줄 · 영역 나무 = 꼭짓점 점
    }

    /// <summary>소품 방향 막대 — 북 0° · 시계 방향(회전된 지도에서도 위경도로 셈해 화면에 맞는다).</summary>
    private void DrawHeading(DrawingContext dc, Map3DDecorationItem it, Point at, Brush brush)
    {
        double deg = it.Props["heading_deg"]?.Type is Newtonsoft.Json.Linq.JTokenType.Float or Newtonsoft.Json.Linq.JTokenType.Integer ? it.Props.Value<double>("heading_deg") : 0;
        double rad = deg * Math.PI / 180, lat = it.Points[0].Y;
        var ahead = new UbPoint(it.Points[0].X + Math.Sin(rad) * 5 / (111320.0 * Math.Cos(lat * Math.PI / 180)), lat + Math.Cos(rad) * 5 / 110540.0);
        var v = Screen(ahead) - at;
        if (v.Length < 1e-6) return;
        v.Normalize();
        var pen = new Pen(brush, 2); pen.Freeze();
        dc.DrawLine(pen, at, at + v * 13);
    }
}

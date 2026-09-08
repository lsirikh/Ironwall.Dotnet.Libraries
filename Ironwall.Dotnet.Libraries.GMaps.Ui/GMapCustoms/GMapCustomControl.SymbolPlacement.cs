using GMap.NET;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

public partial class GMapCustomControl
{
    private SymbolDropAdorner? _symbolDropPreview;
    private AdornerLayer? _symbolDropLayer;
    private bool _symbolDropLayerWarned;
    private AdornerDecorator? _dropHost;

    // ── D-18/D-19: 어도너(라벨·선택 박스) 위 드롭/배치 클릭 전달 ─────────────────────────────
    // 라벨·선택 어도너는 맵의 자식이 아니라 맵을 감싼 AdornerDecorator 의 AdornerLayer(형제)에 산다.
    // WPF 는 드롭 타깃을 hit 요소에서 부모로 올라가며 AllowDrop 인 요소로 정하므로, 어도너 위 드롭은 맵에 닿지 못해
    // 조용히 거부됐고(커서 ⃠), 배치 모드 클릭도 어도너가 먼저 받아 삼켰다(2026-09-07 순회 실측 2건).
    // 해법: 호스트(AdornerDecorator)를 드롭 타깃으로 승격하고, 맵 바깥에서 발생한 이벤트만 맵 로직으로 넘긴다.
    // 맵 자신 위 이벤트는 기존 OnDragOver/OnDrop/OnMouseLeftButtonDown 이 e.Handled 로 먼저 끝내므로 이중 처리는 없다.
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        try { AttachDropHost(); } catch (Exception ex) { _log?.Warning($"Symbol drop host attach failed: {ex.Message}"); }
    }

    private void AttachDropHost()
    {
        AdornerDecorator? host = null;
        for (DependencyObject? p = VisualTreeHelper.GetParent(this); p != null; p = VisualTreeHelper.GetParent(p))
            if (p is AdornerDecorator d) { host = d; break; }
        if (ReferenceEquals(host, _dropHost)) return;
        if (_dropHost != null)
        {
            _dropHost.RemoveHandler(DragOverEvent, (DragEventHandler)OnHostDragOver);
            _dropHost.RemoveHandler(DropEvent, (DragEventHandler)OnHostDrop);
            _dropHost.RemoveHandler(DragLeaveEvent, (DragEventHandler)OnHostDragLeave);
            _dropHost.RemoveHandler(PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)OnHostPreviewMouseLeftButtonDown);
        }
        _dropHost = host;
        if (host == null) return;
        host.AllowDrop = true;
        host.AddHandler(DragOverEvent, (DragEventHandler)OnHostDragOver);
        host.AddHandler(DropEvent, (DragEventHandler)OnHostDrop);
        host.AddHandler(DragLeaveEvent, (DragEventHandler)OnHostDragLeave);
        host.AddHandler(PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)OnHostPreviewMouseLeftButtonDown);
    }

    /// <summary>이벤트 원천이 맵 자신(또는 자식)인가 — 그 경우 맵의 기존 경로가 처리하므로 호스트는 손대지 않는다.</summary>
    private bool IsFromMapItself(RoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return false;
        try
        {
            for (DependencyObject? p = src; p != null; p = (p is Visual || p is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
                if (ReferenceEquals(p, this)) return true;
        }
        catch { }
        return false;
    }

    private void OnHostDragOver(object sender, DragEventArgs e) { if (e.Handled || IsFromMapItself(e)) return; OnDragOver(e); }
    private void OnHostDrop(object sender, DragEventArgs e) { if (e.Handled || IsFromMapItself(e)) return; OnDrop(e); }
    private void OnHostDragLeave(object sender, DragEventArgs e) { if (e.Handled || IsFromMapItself(e)) return; ClearSymbolDropPreview(); }

    /// <summary>배치 모드(팔레트 셀 클릭 → 지도 클릭)에서 어도너 위 클릭을 터널 단계에서 선점해 배치한다.
    /// 어도너(라벨 드래그·핸들)가 먼저 받으면 배치 클릭이 삼켜진다(D-19).</summary>
    private void OnHostPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditMode || !IsSymbolPlacementMode || IsTargetAimMode || IsLineDrawing || IsMeasuring) return;
        if (IsFromMapItself(e)) return;   // 맵 자신 위 클릭은 OnMouseLeftButtonDown 의 기존 배치 분기
        var mousePos = e.GetPosition(this);
        if (!new Rect(RenderSize).Contains(mousePos)) return;
        SymbolPlacementClicked?.Invoke(SymbolPlacementLocation(mousePos), mousePos);
        e.Handled = true;
    }

    public PointLatLng SymbolPlacementLocation(Point p)
    {
        int x = (int)Math.Floor(p.X), y = (int)Math.Floor(p.Y);
        (double lat, double lng) At(int px, int py) { var g = FromLocalToLatLng(px, py); return (g.Lat, g.Lng); }
        var geo = SubPixelGeo.Bilinear(At(x, y), At(x + 1, y), At(x, y + 1), At(x + 1, y + 1), p.X - x, p.Y - y);
        return new(geo.lat, geo.lng);
    }

    private SymbolPaletteDrag? ReadSymbolDrag(DragEventArgs e)
    {
        if (!IsEditMode || !IsSymbolPlacementMode || IsTargetAimMode || IsLineDrawing || IsMeasuring ||
            !e.Data.GetDataPresent(typeof(SymbolPaletteDrag))) return null;
        var data = e.Data.GetData(typeof(SymbolPaletteDrag)) as SymbolPaletteDrag;
        return data != null && ReferenceEquals(data.Map, this) ? data : null;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (!e.Data.GetDataPresent(typeof(SymbolPaletteDrag))) return;
        e.Handled = true; e.Effects = DragDropEffects.None;
        var data = ReadSymbolDrag(e); var point = e.GetPosition(this);
        if (data == null || !new Rect(RenderSize).Contains(point)) { ClearSymbolDropPreview(); return; }
        e.Effects = DragDropEffects.Copy;
        _symbolDropLayer ??= AdornerLayer.GetAdornerLayer(this);
        if (_symbolDropLayer == null)
        {
            // AdornerLayer 가 없으면 고스트/좌표 미리보기만 생략된다(드롭은 OnDrop 에서 정상 처리). 침묵하면 "가끔 미리보기가 안 보인다"로만 체감(R-17).
            if (!_symbolDropLayerWarned) { _symbolDropLayerWarned = true; _log?.Warning("Symbol drop preview: AdornerLayer 를 찾지 못해 미리보기를 생략합니다(드롭은 정상)."); }
            return;
        }
        if (_symbolDropPreview == null) { _symbolDropPreview = new SymbolDropAdorner(this, data.Item.ModelKey); _symbolDropLayer.Add(_symbolDropPreview); }
        var geo = SymbolPlacementLocation(point);
        _symbolDropPreview.Update(point, $"{data.Item.Title}\n{geo.Lat:F6}, {geo.Lng:F6}");
    }
    protected override void OnDragLeave(DragEventArgs e) { base.OnDragLeave(e); ClearSymbolDropPreview(); }
    protected override async void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (!e.Data.GetDataPresent(typeof(SymbolPaletteDrag))) return;
        e.Handled = true; e.Effects = DragDropEffects.None; ClearSymbolDropPreview();
        var data = ReadSymbolDrag(e); var point = e.GetPosition(this);
        if (data == null || !new Rect(RenderSize).Contains(point) || DataContext is not MapViewModel vm) return;
        e.Effects = DragDropEffects.Copy;
        try { await vm.PlacePaletteSymbolAsync(data.Item, SymbolPlacementLocation(point)); }
        catch (Exception ex) { _log?.Error($"Symbol drop failed: {ex.Message}"); }
    }
    public void ClearSymbolDropPreview()
    {
        if (_symbolDropPreview != null) _symbolDropLayer?.Remove(_symbolDropPreview);
        _symbolDropPreview = null; _symbolDropLayer = null;
    }

    private sealed class SymbolDropAdorner : Adorner
    {
        private Point _point; private string _text = "";
        private readonly Symbols3D.HousingVisual? _ghost;
        public SymbolDropAdorner(UIElement element, string? model) : base(element)
        {
            IsHitTestVisible = false;
            if (model != null) { _ghost = new Symbols3D.HousingVisual { ModelKey = model, Yaw = 145, Opacity = .65, Width = 54, Height = 54 }; AddVisualChild(_ghost); }
        }
        protected override int VisualChildrenCount => _ghost == null ? 0 : 1;
        protected override Visual GetVisualChild(int index) => index == 0 && _ghost != null ? _ghost : throw new ArgumentOutOfRangeException(nameof(index));
        protected override Size MeasureOverride(Size constraint) { _ghost?.Measure(new Size(54, 54)); return base.MeasureOverride(constraint); }
        protected override Size ArrangeOverride(Size finalSize) { _ghost?.Arrange(new Rect(_point.X - 27, _point.Y - 59, 54, 54)); return finalSize; }
        public void Update(Point point, string text) { _point = point; _text = text; InvalidateArrange(); InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc)
        {
            var pen = new Pen(Brushes.Turquoise, 1.5);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45, 65, 210, 202)), pen, _point, 13, 7);
            dc.DrawLine(pen, new(_point.X - 19, _point.Y), new(_point.X + 19, _point.Y));
            dc.DrawLine(pen, new(_point.X, _point.Y - 14), new(_point.X, _point.Y + 14));
            var text = new FormattedText(_text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            // [map-tilt FR-04] 힌트 클램프는 실제 가시 사각형(inner 좌표) 기준 — 디지털줌/틸트/오버스캔 시 RenderSize 는 화면 모서리가 아니다.
            var visible = AdornedElement is GMapCustomControl map ? map.GetVisibleInnerRect() : new Rect(AdornedElement.RenderSize);
            double x = Math.Clamp(_point.X + 22, visible.Left, Math.Max(visible.Left, visible.Right - text.Width - 16));
            double y = Math.Clamp(_point.Y + 14, visible.Top, Math.Max(visible.Top, visible.Bottom - text.Height - 14));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 17, 32, 48)), new Pen(Brushes.Turquoise, .7), new(x, y, text.Width + 16, text.Height + 14), 6, 6);
            dc.DrawText(text, new(x + 8, y + 7));
        }
    }
}

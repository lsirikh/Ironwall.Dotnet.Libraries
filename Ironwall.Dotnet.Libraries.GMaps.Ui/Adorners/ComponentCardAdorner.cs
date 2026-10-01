using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/// <summary>
/// 조립 카드(L3)를 지도 위 아이콘 옆에 띄우는 어도너 — 지도(<see cref="GMapCustomControl"/>)를 꾸민다.
/// </summary>
/// <remarks>
/// <para><b>따라가기</b>: 라벨 어도너(<see cref="LabelAdorner"/>)와 같은 투영(코어 로컬 + 회전 행렬)으로 아이콘 화면 중심을 구하고,
/// 줌 · 팬(드래그 중 포함) · 위치 · 디지털 줌 · 뷰포트 스냅샷마다 배치만 다시 한다(요소 1개 — 비용 무시 가능).</para>
/// <para><b>크기 고정</b>: 카드는 지도 좌표가 아니라 화면 좌표의 창이다 — 지도 줌 · 디지털 줌 · 틸트와 무관하게 늘 같은 DIU 크기로 그리고,
/// 아이콘의 화면 자리에 붙여 지도 보기 영역(어도너 층) 안에 가둔다(<see cref="GetDesiredTransform"/>).</para>
/// <para><b>입력</b>: 카드 밖은 히트 테스트를 통과시킨다(카드 사각형만 받는다) — 지도 좌드래그 팬을 가로채지 않는다.
/// <see cref="AdornerManagerService"/> 에는 등록하지 않는다(정리 타이머와 무관한 자기 수명).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class ComponentCardAdorner : Adorner, IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ComponentCardControl _card;
    private bool _disposed;

    public ComponentCardAdorner(GMapCustomControl map, ComponentCardViewModel viewModel) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _card = new ComponentCardControl { DataContext = viewModel };
        AddVisualChild(_card);
        AddLogicalChild(_card);

        _map.OnMapZoomChanged += OnMapChanged;
        _map.OnMapDrag += OnMapChanged;
        _map.OnPositionChanged += OnMapPositionChanged;
        _map.DigitalZoomLevelChanged += OnDigitalZoomChanged;
        _map.SubscribeViewport(OnViewportSnapshot);
    }

    public ComponentCardViewModel ViewModel { get; }

    public GMapPidsMarker Marker => ViewModel.Marker;

    /// <summary>시험용 — 마지막 배치의 카드 왼쪽 위.</summary>
    internal Point LastPlacement { get; private set; }

    /// <summary>카드 컨트롤(포커스 · 시험).</summary>
    internal ComponentCardControl Card => _card;

    private void OnMapChanged() => InvalidateArrange();
    private void OnMapPositionChanged(PointLatLng _) => InvalidateArrange();
    private void OnDigitalZoomChanged(int _) => InvalidateArrange();
    private void OnViewportSnapshot(MapViewportSnapshot _) => InvalidateArrange();

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => index == 0 ? _card : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size constraint)
    {
        _card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return base.MeasureOverride(constraint);
    }

    /// <summary>
    /// 지도의 보기 변환(디지털 줌 · 틸트 <c>ScaleTransform(s, s·cosφ)</c>)을 카드에 물려받지 않는다 — 어도너 좌표 = 어도너 층(화면) 좌표.
    /// </summary>
    /// <remarks>
    /// 어도너 층은 꾸미는 요소(지도)의 변환을 어도너에도 그대로 건다(<c>TransformToAncestor</c>). 지도 좌표의 것(라벨 · 측정 · 조준)은 그게 맞지만,
    /// 카드는 화면에 뜬 창이라 지도와 함께 커지거나 눌리면 안 된다(디지털 줌 2.0 에서 카드가 두 배로 그려지던 결함).
    /// 그래서 변환은 항등으로 두고, 아이콘 자리만 지도 → 층 변환으로 옮겨 그 옆에 붙인다(<see cref="ArrangeOverride"/>).
    /// </remarks>
    public override GeneralTransform GetDesiredTransform(GeneralTransform transform) => Transform.Identity;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = _card.DesiredSize;
        var layer = VisualTreeHelper.GetParent(this) as UIElement;
        var toScreen = MapToLayer(layer);
        var inner = IconCenter();
        var anchor = toScreen?.Transform(inner) ?? inner;
        // 아이콘 반 폭도 화면 크기로(디지털 줌 배율) — 카드가 확대된 아이콘을 덮지 않게
        var half = Marker.Width / 2.0;
        if (toScreen != null && double.IsFinite(half) && half > 0)
        {
            var edge = toScreen.Transform(new Point(inner.X + half, inner.Y));
            half = Math.Abs(edge.X - anchor.X);
        }
        var viewport = layer is { RenderSize: { Width: > 0, Height: > 0 } layerSize }
            ? layerSize
            : new Size(
                double.IsFinite(finalSize.Width) && finalSize.Width > 0 ? finalSize.Width : _map.ActualWidth,
                double.IsFinite(finalSize.Height) && finalSize.Height > 0 ? finalSize.Height : _map.ActualHeight);
        LastPlacement = ComponentCardPlacement.Place(anchor, half, size, viewport);
        _card.Arrange(new Rect(LastPlacement, size));
        return finalSize;
    }

    /// <summary>지도(안쪽 · 논리 좌표) → 어도너 층(화면) 변환. 층이 없거나 아직 붙지 않았으면 null(항등으로 둔다).</summary>
    private GeneralTransform? MapToLayer(UIElement? layer)
    {
        if (layer is null) return null;
        try { return _map.TransformToVisual(layer); }
        catch (InvalidOperationException) { return null; }   // 공통 조상 없음(분리 중)
    }

    /// <summary>아이콘 중심(지도 안쪽 좌표) — 라벨 어도너와 같은 연속 투영(코어 로컬 + 회전 행렬).</summary>
    private Point IconCenter()
    {
        var core = _map.FromLatLngToCoreLocal(Marker.Position);
        var rotation = _map.RotationMatrixValue;
        return rotation.IsIdentity ? core : rotation.Transform(core);
    }

    /// <summary>카드 사각형만 히트 — 그 밖은 지도로 통과(좌드래그 팬 보존).</summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var bounds = new Rect(LastPlacement, _card.RenderSize);
        return bounds.Contains(hitTestParameters.HitPoint) ? base.HitTestCore(hitTestParameters) : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        _map.OnPositionChanged -= OnMapPositionChanged;
        _map.DigitalZoomLevelChanged -= OnDigitalZoomChanged;
        _map.UnsubscribeViewport(OnViewportSnapshot);
        RemoveLogicalChild(_card);
        RemoveVisualChild(_card);
    }
}

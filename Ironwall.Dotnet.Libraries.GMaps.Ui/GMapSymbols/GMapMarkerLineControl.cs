using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.Enums;
using System.Windows.Input;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/12/2025 1:34:32 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 라인 마커 UI 컨트롤
/// </summary>
/// <summary>
/// 라인 심볼 전용 마커 컨트롤
/// - GMapMarkerBaseControl<GMapLineMarker>를 상속받아 라인 심볼 특화 기능 제공
/// - LinePattern에 따른 동적 Line 렌더링
/// - LineOpacity 및 라인 심볼 전용 속성 지원
/// </summary>
public class GMapMarkerLineControl : GMapMarkerBaseControl<GMapLineMarker>
{
    private Canvas _lineCanvas;
    private Polyline _mainPolyline;
    private GMapCustomControl _mapControl;

    // 실제 라인 경계 영역
    private Rect _actualLineBounds = Rect.Empty;

    // IEditableMarker 인터페이스에 추가해야 할 속성
    public Rect ActualBounds => _actualLineBounds;

    #region Static Constructor
    static GMapMarkerLineControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerLineControl),
            new FrameworkPropertyMetadata(typeof(GMapMarkerLineControl)));
    }
    #endregion

    #region Additional Dependency Properties

    /// <summary>
    /// 라인 패턴 타입
    /// </summary>
    public EnumLinePattern LinePattern
    {
        get { return (EnumLinePattern)GetValue(LinePatternProperty); }
        set { SetValue(LinePatternProperty, value); }
    }

    public static readonly DependencyProperty LinePatternProperty =
        DependencyProperty.Register("LinePattern", typeof(EnumLinePattern), typeof(GMapMarkerLineControl),
            new PropertyMetadata(EnumLinePattern.Solid, OnLinePatternChanged));

    /// <summary>
    /// 라인 투명도 (0.0 ~ 1.0)
    /// </summary>
    public double LineOpacity
    {
        get { return (double)GetValue(LineOpacityProperty); }
        set { SetValue(LineOpacityProperty, value); }
    }

    public static readonly DependencyProperty LineOpacityProperty =
        DependencyProperty.Register("LineOpacity", typeof(double), typeof(GMapMarkerLineControl),
            new PropertyMetadata(1.0, OnLineOpacityChanged));

    /// <summary>
    /// 닫힌 경로 여부
    /// </summary>
    public bool IsClosedPath
    {
        get { return (bool)GetValue(IsClosedPathProperty); }
        set { SetValue(IsClosedPathProperty, value); }
    }

    public static readonly DependencyProperty IsClosedPathProperty =
        DependencyProperty.Register("IsClosedPath", typeof(bool), typeof(GMapMarkerLineControl),
            new PropertyMetadata(false, OnIsClosedPathChanged));

    /// <summary>
    /// 화살표 표시 여부
    /// </summary>
    public bool ShowArrowHead
    {
        get { return (bool)GetValue(ShowArrowHeadProperty); }
        set { SetValue(ShowArrowHeadProperty, value); }
    }

    public static readonly DependencyProperty ShowArrowHeadProperty =
        DependencyProperty.Register("ShowArrowHead", typeof(bool), typeof(GMapMarkerLineControl),
            new PropertyMetadata(false, OnShowArrowHeadChanged));

    /// <summary>
    /// 드로잉 중 여부
    /// </summary>
    //public bool IsDrawing
    //{
    //    get { return (bool)GetValue(IsDrawingProperty); }
    //    set { SetValue(IsDrawingProperty, value); }
    //}

    //public static readonly DependencyProperty IsDrawingProperty =
    //    DependencyProperty.Register("IsDrawing", typeof(bool), typeof(GMapMarkerLineControl),
    //        new PropertyMetadata(false, OnIsDrawingChanged));

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMarkerLineControl()
    {
        // 기본 클래스에서 InitializeControl() 호출됨

    }

    /// <summary>
    /// GMapLineMarker 함께 생성하는 생성자
    /// </summary>
    /// <param name="lineMarker">연결할 라인 마커</param>
    public GMapMarkerLineControl(GMapLineMarker lineMarker) : base(lineMarker)
    {
        // 기본 클래스에서 UpdateFromMarker(), SetupDataBindings() 호출됨

        Loaded += OnControlLoaded;
        Unloaded += OnControlUnloaded;
    }

    #endregion
    #region Lifecycle Events
    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== GMapMarkerLineControl Loaded ===");

        // Visual Tree가 완성된 후 MapControl 찾기
        _mapControl = FindParentMapControl();

        if (_mapControl != null)
        {
            System.Diagnostics.Debug.WriteLine($"MapControl 찾음: {_mapControl.GetType().Name}");

            // 지도 이벤트 구독
            _mapControl.OnMapZoomChanged += OnMapChanged;
            _mapControl.OnMapDrag += OnMapChanged;
            _mapControl.OnPositionChanged += OnMapPositionChanged;

            // 초기 라인 그리기
            UpdateLineGeometry();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("MapControl을 찾을 수 없음!");
        }
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        if (_mapControl != null)
        {
            _mapControl.OnMapZoomChanged -= OnMapChanged;
            _mapControl.OnMapDrag -= OnMapChanged;
            _mapControl.OnPositionChanged -= OnMapPositionChanged;
        }
    }

    private void OnMapChanged()
    {
        System.Diagnostics.Debug.WriteLine("지도 변경 감지 - 라인 업데이트");
        UpdateLineGeometry();
    }

    private void OnMapPositionChanged(PointLatLng point)
    {
        UpdateLineGeometry();
    }
    #endregion
    #region Abstract Methods Implementation

    /// <summary>
    /// GMapLineMarker 전용 UI 업데이트 구현
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        if (Marker == null) return;

        // GMapLineMarker 전용 속성 동기화 (타입 안전)
        LinePattern = Marker.LinePattern;
        LineOpacity = Marker.LineOpacity;
        IsClosedPath = Marker.IsClosedPath;
        ShowArrowHead = Marker.ShowArrowHead;

        // 라인 심볼 전용 모양 업데이트
        UpdateLineGeometry();
    }

    /// <summary>
    /// GMapLineMarker 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        if (Marker == null) return;

        // GMapLineMarker 전용 바인딩 (타입 안전)
        SetupPropertyBinding(LinePatternProperty, nameof(Marker.LinePattern));
        SetupPropertyBinding(LineOpacityProperty, nameof(Marker.LineOpacity));
        SetupPropertyBinding(IsClosedPathProperty, nameof(Marker.IsClosedPath));
        SetupPropertyBinding(ShowArrowHeadProperty, nameof(Marker.ShowArrowHead));
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 오버라이드
    /// </summary>
    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();
        System.Diagnostics.Debug.WriteLine("GMapMarkerLineControl 초기화 완료");
    }

    /// <summary>
    /// 마커 모양 업데이트 오버라이드 (LinePattern 고려)
    /// </summary>
    protected override void UpdateMarkerAppearance()
    {
        // 기본 색상 설정 먼저
        base.UpdateMarkerAppearance();

        // 라인 심볼 전용 모양 업데이트
        UpdateLineAppearance();
    }

    /// <summary>
    /// 템플릿 적용 시 호출
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 템플릿에서 UI 요소 찾기
        _lineCanvas = GetTemplateChild("PART_LineCanvas") as Canvas;
        _mainPolyline = GetTemplateChild("PART_MainPolyline") as Polyline;

        System.Diagnostics.Debug.WriteLine($"Template 적용: Canvas={_lineCanvas != null}, Polyline={_mainPolyline != null}");

    }

    /// <summary>
    /// 단일 클릭 처리 오버라이드 (라인 심볼 전용 로직)
    /// </summary>
    protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerSingleClicked(e);
    }

    /// <summary>
    /// 더블클릭 처리 오버라이드 (편집 모드 토글 등)
    /// </summary>
    protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerDoubleClicked(e);
    }

    /// <summary>
    /// 클릭으로 선택과 비선택에 따른 이벤트 콜백
    /// </summary>
    /// <param name="isSelected"></param>
    protected override void OnSelectionChanged(bool isSelected)
    {
        base.OnSelectionChanged(isSelected);
    }

    #endregion

    #region Line Control Specific Methods

    /// <summary>
    /// 라인 컨트롤 초기화 완료 후 호출 (가상 메서드)
    /// </summary>
    protected virtual void OnLineControlInitialized()
    {
        // 상속 클래스에서 구현 가능
    }

    /// <summary>
    /// 라인 심볼 전용 모양 업데이트
    /// </summary>
    protected virtual void UpdateLineAppearance()
    {
        if (Marker == null || _isUpdatingFromMarker) return;

        // LinePattern에 따른 스타일 적용
        if (_mainPolyline != null)
        {
            ApplyLinePattern(_mainPolyline, LinePattern);
        }
    }


    /// <summary>
    /// 라인 기하학 업데이트 (개선된 버전)
    /// </summary>
    private void UpdateLineGeometry()
    {
        if (Marker == null || _mainPolyline == null || _mapControl == null)
            return;

        try
        {
            var points = Marker.RuntimePoints;
            if (points == null || points.Count < 2)
            {
                _mainPolyline.Points = new PointCollection();
                _actualLineBounds = Rect.Empty;
                return;
            }

            var pointCollection = new PointCollection();
            // 라인의 실제 경계 계산을 위한 변수
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;


            // 마커의 Position이 이제 중심점이므로, 각 포인트를 중심 기준으로 변환
            var centerScreenPos = _mapControl.FromLatLngToLocal(Marker.Position);

            foreach (var geoPoint in points)
            {
                var screenPoint = _mapControl.FromLatLngToLocal(geoPoint);

                // 컨트롤의 중심(Width/2, Height/2)을 기준으로 상대 좌표 계산
                var relativePoint = new Point(
                    (screenPoint.X - centerScreenPos.X) + Width / 2,
                    (screenPoint.Y - centerScreenPos.Y) + Height / 2
                );

                pointCollection.Add(relativePoint);

                // 실제 경계 업데이트
                minX = Math.Min(minX, relativePoint.X);
                minY = Math.Min(minY, relativePoint.Y);
                maxX = Math.Max(maxX, relativePoint.X);
                maxY = Math.Max(maxY, relativePoint.Y);
            }

            // 닫힌 경로 처리
            if (IsClosedPath && pointCollection.Count > 2)
            {
                pointCollection.Add(pointCollection[0]);
            }

            _mainPolyline.Points = pointCollection;

            //_mainPolyline.Stroke = MarkerStroke;
            //_mainPolyline.StrokeThickness = MarkerStrokeThickness;
            //_mainPolyline.Opacity = LineOpacity;

            // 실제 라인 경계 저장 (StrokeThickness 고려)
            var strokePadding = MarkerStrokeThickness / 2;
            _actualLineBounds = new Rect(
                minX - strokePadding,
                minY - strokePadding,
                maxX - minX + MarkerStrokeThickness,
                maxY - minY + MarkerStrokeThickness
            );

            // Control 크기를 실제 라인 크기에 맞게 조정
            Width = _actualLineBounds.Width + 20;  // 여백 추가
            Height = _actualLineBounds.Height + 20;

            // Canvas 크기도 조정
            if (_lineCanvas != null)
            {
                _lineCanvas.Width = Width;
                _lineCanvas.Height = Height;
            }

            // 디버깅 로그
            System.Diagnostics.Debug.WriteLine($"라인 실제 경계: {_actualLineBounds}");
            System.Diagnostics.Debug.WriteLine($"컨트롤 크기: {Width}x{Height}");

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"라인 업데이트 오류: {ex.Message}");
        }
    }

    private void UpdateCanvasSize()
    {
        if (_lineCanvas == null || _mainPolyline?.Points == null || _mainPolyline.Points.Count < 2)
            return;

        // 바운딩 박스 계산
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var point in _mainPolyline.Points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        // Canvas 크기와 위치 조정 (여유 공간 추가)
        double padding = 50;
        _lineCanvas.Width = Math.Max(200, maxX - minX + padding * 2);
        _lineCanvas.Height = Math.Max(200, maxY - minY + padding * 2);

        // Canvas를 중앙에 배치하기 위한 마진 조정
        Canvas.SetLeft(_lineCanvas, minX - padding);
        Canvas.SetTop(_lineCanvas, minY - padding);

        System.Diagnostics.Debug.WriteLine($"Canvas 크기 조정: {_lineCanvas.Width}x{_lineCanvas.Height}, 위치: ({minX - padding}, {minY - padding})");
    }

    private void UpdateMainPolyline(GMapCustomControl mapControl)
    {
        System.Diagnostics.Debug.WriteLine($"=== UpdateMainPolyline 시작 ===");

        var points = Marker.RuntimePoints;
        System.Diagnostics.Debug.WriteLine($"RuntimePoints 개수: {points?.Count ?? 0}");

        if (points == null || points.Count < 2)
        {
            _mainPolyline.Points = new PointCollection();
            return;
        }

        var pointCollection = new PointCollection();

        // 첫 번째 포인트를 기준으로 상대 좌표 계산
        var firstPoint = points[0];
        var firstScreenPoint = mapControl.FromLatLngToLocal(firstPoint);

        foreach (var geoPoint in points)
        {
            var screenPoint = mapControl.FromLatLngToLocal(geoPoint);
            var relativePoint = new Point(
                screenPoint.X - firstScreenPoint.X,
                screenPoint.Y - firstScreenPoint.Y
            );
            pointCollection.Add(relativePoint);
        }

        // 닫힌 경로 처리
        if (IsClosedPath && pointCollection.Count > 2)
        {
            pointCollection.Add(pointCollection[0]);
        }

        _mainPolyline.Points = pointCollection;
        _mainPolyline.Stroke = MarkerStroke;
        _mainPolyline.StrokeThickness = MarkerStrokeThickness;
        _mainPolyline.Opacity = LineOpacity;

        ApplyLinePattern(_mainPolyline, LinePattern);

        // 화살표 처리 (필요시)
        if (ShowArrowHead && pointCollection.Count >= 2)
        {
            ApplyArrowHead();
        }
    }


    /// <summary>
    /// 라인 패턴 적용
    /// </summary>
    private void ApplyLinePattern(Polyline polyline, EnumLinePattern pattern)
    {
        if (polyline == null) return;

        switch (pattern)
        {
            case EnumLinePattern.Solid:
                polyline.StrokeDashArray = null;
                break;
            case EnumLinePattern.Dashed:
                polyline.StrokeDashArray = new DoubleCollection { 10, 5 };
                break;
            case EnumLinePattern.Dotted:
                polyline.StrokeDashArray = new DoubleCollection { 2, 3 };
                break;
            case EnumLinePattern.DashDot:
                polyline.StrokeDashArray = new DoubleCollection { 10, 3, 2, 3 };
                break;
            case EnumLinePattern.DoubleLine:
                // 이중선은 두 개의 Polyline 필요 (향후 구현)
                polyline.StrokeThickness = MarkerStrokeThickness * 1.5;
                break;
        }
    }

    /// <summary>
    /// 화살표 머리 적용
    /// </summary>
    private void ApplyArrowHead()
    {
        // 화살표 구현 (필요시)
        if (_mainPolyline?.Points?.Count >= 2)
        {
            var lastIndex = _mainPolyline.Points.Count - 1;
            var endPoint = _mainPolyline.Points[lastIndex];
            var prevPoint = _mainPolyline.Points[lastIndex - 1];

            // 화살표 그리기 로직
            // TODO: 구현 필요
        }
    }


    /// <summary>
    /// 투명도 적용
    /// </summary>
    private void ApplyLineOpacity()
    {
        if (_mainPolyline != null)
        {
            _mainPolyline.Opacity = LineOpacity;
        }
    }
    #endregion

    #region Static Property Changed Callbacks

    /// <summary>
    /// LinePattern 변경 시 호출
    /// </summary>
    protected static void OnLinePatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerLineControl control && control.Marker != null)
        {
            // UI 업데이트
            control.UpdateLineAppearance();

        }
    }

    /// <summary>
    /// 투명도 변경 시 호출
    /// </summary>
    protected static void OnLineOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerLineControl control && control.Marker != null)
        {
            // UI 투명도 적용
            control._mainPolyline.Opacity = (double)e.NewValue;
        }
    }

    /// <summary>
    /// 닫힌 경로 변경 시 호출
    /// </summary>
    protected static void OnIsClosedPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerLineControl control && control.Marker != null)
        {
            // UI 업데이트
            control.UpdateLineGeometry();
            
        }
    }

    /// <summary>
    /// 화살표 표시 변경 시 호출
    /// </summary>
    protected static void OnShowArrowHeadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerLineControl control && control.Marker != null)
        {
            // UI 업데이트
            control.UpdateLineAppearance();
           
        }
    }

    /// <summary>
    /// 드로잉 상태 변경 시 호출
    /// </summary>
    protected static void OnIsDrawingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerLineControl control && control.Marker != null)
        {
            // UI 업데이트
            control.UpdateLineGeometry();
        }
    }

    #endregion

   
}

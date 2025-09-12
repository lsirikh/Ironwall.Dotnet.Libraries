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
    private Polyline _previewPolyline;
    private Point? _currentMousePosition = null;  // 추가 필드
    private bool _isUpdatingFromMarker = false;  // 순환 방지 플래그

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
    public bool IsDrawing
    {
        get { return (bool)GetValue(IsDrawingProperty); }
        set { SetValue(IsDrawingProperty, value); }
    }

    public static readonly DependencyProperty IsDrawingProperty =
        DependencyProperty.Register("IsDrawing", typeof(bool), typeof(GMapMarkerLineControl),
            new PropertyMetadata(false, OnIsDrawingChanged));

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
        IsDrawing = Marker.IsDrawing;

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
        SetupPropertyBinding(IsDrawingProperty, nameof(Marker.IsDrawing));
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
        _previewPolyline = GetTemplateChild("PART_PreviewPolyline") as Polyline;

        UpdateLineGeometry();
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
        if (Marker == null || _mainPolyline == null) return;

        try
        {
            var mapControl = FindParent<GMapControl>(this);
            if (mapControl == null) return;

            // 1. 확정된 라인 포인트들 렌더링
            UpdateMainPolyline(mapControl);

            // 2. 드로잉 중인 경우 미리보기 라인 렌더링
            if (Marker.IsDrawing && _previewPolyline != null)
            {
                UpdatePreviewPolyline(mapControl);
            }
            else if (_previewPolyline != null)
            {
                _previewPolyline.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"라인 기하학 업데이트 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 확정된 라인 업데이트
    /// </summary>
    private void UpdateMainPolyline(GMapControl mapControl)
    {
        var points = Marker.RuntimePoints;
        var pointCollection = new PointCollection();

        foreach (var geoPoint in points)
        {
            var localPoint = mapControl.FromLatLngToLocal(geoPoint);
            pointCollection.Add(new Point(localPoint.X, localPoint.Y));
        }

        _mainPolyline.Points = pointCollection;
        _mainPolyline.Stroke = MarkerStroke;
        _mainPolyline.StrokeThickness = MarkerStrokeThickness;
        _mainPolyline.Opacity = Marker.IsDrawing ? 0.7 : LineOpacity;
        ApplyLinePattern(_mainPolyline, LinePattern);
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
    /// 미리보기 라인 업데이트 (마우스 이동시 호출)
    /// </summary>
    public void UpdatePreviewLine(Point mousePosition)
    {
        _currentMousePosition = mousePosition;

        if (Marker?.IsDrawing == true && _previewPolyline != null)
        {
            var mapControl = FindParent<GMapControl>(this);
            if (mapControl != null)
            {
                UpdatePreviewPolyline(mapControl);
            }
        }
    }

    /// <summary>
    /// 미리보기 라인 업데이트
    /// </summary>
    private void UpdatePreviewPolyline(GMapControl mapControl)
    {
        var points = Marker.RuntimePoints;
        if (points.Count == 0 || _currentMousePosition == null)
        {
            _previewPolyline.Visibility = Visibility.Collapsed;
            return;
        }

        // 마지막 확정 포인트에서 현재 마우스 위치까지 미리보기 라인
        var lastPoint = mapControl.FromLatLngToLocal(points.Last());
        var previewPoints = new PointCollection
        {
            new Point(lastPoint.X, lastPoint.Y),
            _currentMousePosition.Value
        };

        _previewPolyline.Points = previewPoints;
        _previewPolyline.Stroke = new SolidColorBrush(Colors.Gray);
        _previewPolyline.StrokeThickness = MarkerStrokeThickness;
        _previewPolyline.Opacity = 0.5;
        _previewPolyline.StrokeDashArray = new DoubleCollection { 5, 3 };
        _previewPolyline.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 드로잉 시작 시 초기화
    /// </summary>
    public void StartDrawingMode()
    {
        _currentMousePosition = null;
        if (_previewPolyline != null)
        {
            _previewPolyline.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 드로잉 종료 시 정리
    /// </summary>
    public void EndDrawingMode()
    {
        _currentMousePosition = null;
        if (_previewPolyline != null)
        {
            _previewPolyline.Visibility = Visibility.Collapsed;
        }

        // 최종 라인 렌더링
        UpdateLineGeometry();
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

    /// <summary>
    /// 부모 컨트롤 찾기 헬퍼
    /// </summary>
    private T FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;

        if (parentObject is T parent)
            return parent;

        return FindParent<T>(parentObject);
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

            // 마커 데이터와 동기화 (타입 안전)
            if (control.Marker.LinePattern != (EnumLinePattern)e.NewValue)
                control.Marker.LinePattern = (EnumLinePattern)e.NewValue;
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
            control.ApplyLineOpacity();

            // 마커 데이터와 동기화 (타입 안전)
            if (control.Marker.LineOpacity != (double)e.NewValue)
                control.Marker.LineOpacity = (double)e.NewValue;
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

            // 마커 데이터와 동기화 (타입 안전)
            if (control.Marker.IsClosedPath != (bool)e.NewValue)
                control.Marker.IsClosedPath = (bool)e.NewValue;
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

            // 마커 데이터와 동기화 (타입 안전)
            if (control.Marker.ShowArrowHead != (bool)e.NewValue)
                control.Marker.ShowArrowHead = (bool)e.NewValue;
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

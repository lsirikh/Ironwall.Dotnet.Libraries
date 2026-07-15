using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.ComponentModel;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.Enums;
using System.Windows.Input;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

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
    private Canvas? _lineCanvas;
    private GMapCustomControl? _mapControl;

    public Polyline? MainPolyline { get; private set; }
    
    // Adorner에서 접근할 수 있도록 Public 속성 추가
    public PointCollection LinePoints => MainPolyline?.Points ?? new PointCollection();

    // 라인의 실제 경계를 반환하는 속성 추가
    public Rect ActualLineBounds
    {
        get
        {
            if (MainPolyline?.Points == null || MainPolyline.Points.Count < 2)
                return new Rect(0, 0, Width, Height);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var point in MainPolyline.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

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
        //System.Diagnostics.Debug.WriteLine("=== GMapMarkerLineControl Loaded ===");

        // Visual Tree가 완성된 후 MapControl 찾기
        _mapControl = FindParentMapControl();

        if (_mapControl != null)
        {
            //System.Diagnostics.Debug.WriteLine($"MapControl 찾음: {_mapControl.GetType().Name}");

            // 지도 이벤트 구독
            _mapControl.OnMapZoomChanged += OnMapChanged;
            _mapControl.OnMapDrag += OnMapChanged;
            _mapControl.OnPositionChanged += OnMapPositionChanged;
            // [FR-C4] 창 리사이즈/모니터 이동 시 폴리라인 픽셀캐시 stale → 재계산(줌/팬 없이도). 2026-07-15 멀티모니터 소실.
            _mapControl.SizeChanged += OnMapSizeChanged;

            // 초기 라인 그리기
            UpdateLineGeometry();
        }
        else
        {
            //System.Diagnostics.Debug.WriteLine("MapControl을 찾을 수 없음!");
        }

        // IsVisible 변경 감지: Visibility=false 후 true로 복원 시 기하 재계산
        if (Marker != null)
            Marker.PropertyChanged += OnMarkerPropertyChanged;
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        if (_mapControl != null)
        {
            _mapControl.OnMapZoomChanged -= OnMapChanged;
            _mapControl.OnMapDrag -= OnMapChanged;
            _mapControl.OnPositionChanged -= OnMapPositionChanged;
            _mapControl.SizeChanged -= OnMapSizeChanged;
        }

        if (Marker != null)
            Marker.PropertyChanged -= OnMarkerPropertyChanged;
    }

    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_mapControl == null) return;

        // 점 변경(리사이즈 스케일·Undo·정점편집)에 폴리라인 재계산 — NotifyPointsChanged가 RuntimePoints/LinePoints 발화.
        // 이전엔 IsVisible만 반응해 ApplyGeometry로 점을 바꿔도 UpdateLineGeometry 미실행 → 화면 stale(창 전환 전까지 안 보임),
        // Undo 후 라인/adorner 어긋남. LineArea_Symbol_Resize 리사이즈 렌더 갱신 수정.
        if (e.PropertyName == nameof(GMapLineMarker.RuntimePoints)
            || e.PropertyName == nameof(GMapLineMarker.LinePoints))
        {
            Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);
            return;
        }

        // Visibility가 false→true로 바뀔 때 형상 재계산(RestoreLayerVisibility 이후 stale 복구).
        if (e.PropertyName == nameof(GMapLineMarker.IsVisible) && Marker?.IsVisible == true)
        {
            Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    private void OnMapChanged()
    {
        //System.Diagnostics.Debug.WriteLine("지도 변경 감지 - 라인 업데이트");
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

        // Polyline 속성 강제 업데이트 (중요!)
        if (MainPolyline != null)
        {
            MainPolyline.Stroke = ColorHelper.ToBrush(Marker.StrokeColor);
            MainPolyline.StrokeThickness = Marker.StrokeThickness;
            MainPolyline.Opacity = Marker.LineOpacity;
        }

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

        // Polyline에 직접 바인딩 추가
        if (MainPolyline != null)
        {
            var strokeBinding = new Binding(nameof(Marker.StrokeColor))
            {
                Source = Marker,
                Mode = BindingMode.OneWay,
                Converter = new ColorTypeToBrushConverter()
            };
            MainPolyline.SetBinding(Polyline.StrokeProperty, strokeBinding);

            var thicknessBinding = new Binding(nameof(Marker.StrokeThickness))
            {
                Source = Marker,
                Mode = BindingMode.OneWay
            };
            MainPolyline.SetBinding(Polyline.StrokeThicknessProperty, thicknessBinding);
        }
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 오버라이드
    /// </summary>
    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();

        // Polyline이 있으면 속성 동기화
        if (MainPolyline != null && Marker != null)
        {
            MainPolyline.Stroke = ColorHelper.ToBrush(Marker.StrokeColor);
            MainPolyline.StrokeThickness = Marker.StrokeThickness;
            MainPolyline.Opacity = Marker.LineOpacity;
        }
        //System.Diagnostics.Debug.WriteLine("GMapMarkerLineControl 초기화 완료");
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
        MainPolyline = GetTemplateChild("PART_MainPolyline") as Polyline;

        // 초기값 설정 및 바인딩 복구
        if (MainPolyline != null && Marker != null)
        {
            MainPolyline.Stroke = ColorHelper.ToBrush(Marker.StrokeColor);
            MainPolyline.StrokeThickness = Marker.StrokeThickness;
            MainPolyline.Opacity = Marker.LineOpacity;
        }

        //System.Diagnostics.Debug.WriteLine($"Template 적용: Canvas={_lineCanvas != null}, Polyline={MainPolyline != null}");
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
        if (MainPolyline != null)
        {
            ApplyLinePattern(MainPolyline, LinePattern);
        }
    }


    /// <summary>
    /// 라인 기하학 업데이트 (개선된 버전)
    /// </summary>
    private void UpdateLineGeometry()
    {
        if (Marker == null || MainPolyline == null || _mapControl == null)
            return;

        try
        {
            var points = Marker.RuntimePoints;
            if (points == null || points.Count < 2)
            {
                MainPolyline.Points = new PointCollection();
                return;
            }

            // 1. 먼저 모든 포인트의 화면 좌표 계산
            var screenPoints = new List<Point>();
            foreach (var geoPoint in points)
            {
                var gPoint = _mapControl.FromLatLngToLocal(geoPoint);
                var screenPoint = new Point(gPoint.X, gPoint.Y);
                screenPoints.Add(screenPoint);
            }

            // 2. 경계 박스 계산
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var point in screenPoints)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            // 3. 실제 필요한 크기 계산
            double actualWidth = maxX - minX;
            double actualHeight = maxY - minY;


            // 최소 크기와 여백 추가
            const double padding = 5;
            const double minSize = 40;

            // 실제 크기 + 여백, 최소 100 보장
            Width = Math.Max(minSize, actualWidth + padding * 2);
            Height = Math.Max(minSize, actualHeight + padding * 2);

            // 4. 중심점 기준으로 상대 좌표 계산 (수정된 부분)
            var centerGPoint = _mapControl.FromLatLngToLocal(Marker.Position);
            var centerScreenPos = new Point(centerGPoint.X, centerGPoint.Y);

            var pointCollection = new PointCollection();

            foreach (var screenPoint in screenPoints)
            {
                // 컨트롤의 중심(Width/2, Height/2)을 기준으로 배치
                var relativePoint = new Point(
                    (screenPoint.X - centerScreenPos.X) + Width / 2,
                    (screenPoint.Y - centerScreenPos.Y) + Height / 2
                );
                pointCollection.Add(relativePoint);
            }

            // 닫힌 경로 처리
            if (IsClosedPath && pointCollection.Count > 2)
            {
                pointCollection.Add(pointCollection[0]);
            }

            MainPolyline.Points = pointCollection;
           
            // Canvas 크기도 업데이트
            if (_lineCanvas != null)
            {
                _lineCanvas.Width = Width;
                _lineCanvas.Height = Height;
            }

            // 디버깅 로그
            //System.Diagnostics.Debug.WriteLine($"라인 경계: ({minX:F0},{minY:F0})-({maxX:F0},{maxY:F0})");
            //System.Diagnostics.Debug.WriteLine($"실제 크기: {actualWidth:F0}x{actualHeight:F0}");
            //System.Diagnostics.Debug.WriteLine($"컨트롤 크기: {Width:F0}x{Height:F0}");
            //System.Diagnostics.Debug.WriteLine($"중심점: ({centerScreenPos.X:F0},{centerScreenPos.Y:F0})");

        }
        catch (Exception ex)
        {
            // [FR-C4] 무음 삼킴 금지 — 기하 실패는 화면 소실로 직결(2026-07-15 분석 §6.2).
            _log?.Warning($"[Line] UpdateLineGeometry 실패 '{Marker?.Title}': {ex.Message}");
        }
    }

    /// <summary>[FR-C4] 맵 크기 변경(창 리사이즈·최대화·모니터 이동 반영) → 폴리라인 재계산(레이아웃 안정 후).</summary>
    private void OnMapSizeChanged(object sender, SizeChangedEventArgs e)
        => Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);

    /// <summary>[FR-C4] 모니터 간 이동 등 DPI 변경 → 픽셀 좌표계 변화 반영(줌/팬 없이도 재계산).</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.InvokeAsync(UpdateLineGeometry, System.Windows.Threading.DispatcherPriority.Render);
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
            // _mainPolyline이 아직 초기화되지 않았으면 리턴
            if (control.MainPolyline == null) return;

            // UI 투명도 적용
            control.MainPolyline.Opacity = (double)e.NewValue;
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

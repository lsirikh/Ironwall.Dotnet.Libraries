using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using GMap.NET.WindowsPresentation;
using System;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/19/2025 7:40:54 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// Generic 기본 마커 컨트롤
/// - GMapCustomMarker를 상속받는 모든 마커 타입 지원
/// - 기존 코드 패턴 유지하면서 타입 안전성 확보
/// </summary>
/// <typeparam name="T">GMapCustomMarker를 상속받는 마커 타입</typeparam>
public abstract class GMapMarkerBaseControl<T> : Control, IMarkerControl where T 
    : GMapMarker, IEditableMarker
{
    #region Static Constructor
    static GMapMarkerBaseControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerBaseControl<T>),
            new FrameworkPropertyMetadata(typeof(GMapMarkerBaseControl<T>)));
    }
    #endregion

    #region Dependency Properties

    /// <summary>
    /// 연결된 마커 객체 (강타입)
    /// </summary>
    public T Marker
    {
        get { return (T)GetValue(MarkerProperty); }
        set { SetValue(MarkerProperty, value); }
    }

    public static readonly DependencyProperty MarkerProperty =
        DependencyProperty.Register("Marker", typeof(T), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(null, OnMarkerChanged));

    /// <summary>
    /// 마커 제목
    /// </summary>
    public string MarkerTitle
    {
        get { return (string)GetValue(MarkerTitleProperty); }
        set { SetValue(MarkerTitleProperty, value); }
    }

    public static readonly DependencyProperty MarkerTitleProperty =
        DependencyProperty.Register("MarkerTitle", typeof(string), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata("Marker"));

    /// <summary>
    /// 제목 사이즈
    /// </summary>
    public double TitleSize
    {
        get { return (double)GetValue(TitleSizeProperty); }
        set { SetValue(TitleSizeProperty, value); }
    }

    public static readonly DependencyProperty TitleSizeProperty =
        DependencyProperty.Register("TitleSize", typeof(double), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(10.0));

    /// <summary>
    /// 마커 메인 색상
    /// </summary>
    public Brush MarkerFill
    {
        get { return (Brush)GetValue(MarkerFillProperty); }
        set { SetValue(MarkerFillProperty, value); }
    }

    public static readonly DependencyProperty MarkerFillProperty =
        DependencyProperty.Register("MarkerFill", typeof(Brush), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(Brushes.Red, OnMarkerFillChanged));

    

    /// <summary>
    /// 마커 테두리 색상
    /// </summary>
    public Brush MarkerStroke
    {
        get { return (Brush)GetValue(MarkerStrokeProperty); }
        set { SetValue(MarkerStrokeProperty, value); }
    }

    public static readonly DependencyProperty MarkerStrokeProperty =
        DependencyProperty.Register("MarkerStroke", typeof(Brush), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(Brushes.White, OnMarkerStrokeChanged));

    

    /// <summary>
    /// 마커 테두리 두께
    /// </summary>
    public double MarkerStrokeThickness
    {
        get { return (double)GetValue(MarkerStrokeThicknessProperty); }
        set { SetValue(MarkerStrokeThicknessProperty, value); }
    }

    public static readonly DependencyProperty MarkerStrokeThicknessProperty =
        DependencyProperty.Register("MarkerStrokeThickness", typeof(double), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(1.0, OnMarkerStrokeThicknessChanged));

    

    /// <summary>
    /// 선택 상태
    /// </summary>
    public bool IsSelected
    {
        get { return (bool)GetValue(IsSelectedProperty); }
        set { SetValue(IsSelectedProperty, value); }
    }

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register("IsSelected", typeof(bool), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(false, OnIsSelectedChanged));

    /// <summary>
    /// 마커 상태 (Active, Inactive 등)
    /// </summary>
    public EnumOperationState MarkerState
    {
        get { return (EnumOperationState)GetValue(MarkerStateProperty); }
        set { SetValue(MarkerStateProperty, value); }
    }

    public static readonly DependencyProperty MarkerStateProperty =
        DependencyProperty.Register("MarkerState", typeof(EnumOperationState), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(EnumOperationState.ACTIVATED, OnMarkerStateChanged));

    /// <summary>
    /// 라벨 표시 여부
    /// </summary>
    public bool ShowTitle
    {
        get { return (bool)GetValue(ShowTitleProperty); }
        set { SetValue(ShowTitleProperty, value); }
    }

    public static readonly DependencyProperty ShowTitleProperty =
        DependencyProperty.Register("ShowTitle", typeof(bool), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(false, OnShowTitleChanged));

    /// <summary>
    /// Shape 표시 여부
    /// </summary>
    public bool ShowShape
    {
        get { return (bool)GetValue(ShowShapeProperty); }
        set { SetValue(ShowShapeProperty, value); }
    }

    public static readonly DependencyProperty ShowShapeProperty =
       DependencyProperty.Register("ShowShape", typeof(bool), typeof(GMapMarkerBaseControl<T>),
           new PropertyMetadata(true));

    // RotationAngle -> Bearing에 Dp연결 및 Callback연결
    public double RotationAngle
    {
        get { return (double)GetValue(RotationAngleProperty); }
        set { SetValue(RotationAngleProperty, value); }
    }

    // RotationAngle Property
    public static readonly DependencyProperty RotationAngleProperty =
        DependencyProperty.Register("RotationAngle", typeof(double), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(0.0, OnRotationAngleChanged, CoerceDoubleValue));

    /// <summary>
    /// 형태 변경 애니메이션 활성화 여부
    /// </summary>
    public bool EnableShapeAnimation
    {
        get { return (bool)GetValue(EnableShapeAnimationProperty); }
        set { SetValue(EnableShapeAnimationProperty, value); }
    }
    public static readonly DependencyProperty EnableShapeAnimationProperty =
        DependencyProperty.Register("EnableShapeAnimation", typeof(bool), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(true));
    #endregion

    #region Events

    /// <summary>
    /// 마커 클릭 이벤트 - 간단한 이벤트 아규먼트 사용
    /// </summary>
    public event EventHandler<MarkerClickEventArgs>? MarkerClick;

    /// <summary>
    /// 마커 더블클릭 이벤트
    /// </summary>
    public event EventHandler<MarkerClickEventArgs>? MarkerDoubleClick;

    /// <summary>
    /// 마커 선택 상태 변경 이벤트
    /// </summary>
    public event EventHandler<MarkerSelectionChangedEventArgs>? SelectionChanged;


    #endregion

    #region Protected Fields

    /// <summary>
    /// 마지막 클릭 시간 (더블클릭 감지용)
    /// </summary>
    protected DateTime LastClickTime { get; set; }

    /// <summary>
    /// 더블클릭 간격 (밀리초)
    /// </summary>
    protected const int DoubleClickInterval = 500;

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    protected GMapMarkerBaseControl()
    {
        _log = IoC.Get<ILogService>();
    }

    /// <summary>
    /// 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="marker">연결할 마커</param>
    protected GMapMarkerBaseControl(T marker) : this()
    {
        Marker = marker;
        //OnControlInitialized();
        UpdateFromMarker();
        SetupDataBindings();

    }

    #endregion

    #region Abstract Methods (하위에서 구현)

    /// <summary>
    /// 마커별 전용 UI 업데이트 (추상 메서드)
    /// </summary>
    protected abstract void UpdateFromSpecificMarker();

    /// <summary>
    /// 마커별 전용 바인딩 설정 (추상 메서드)
    /// </summary>
    protected abstract void SetupSpecificBindings();

    #endregion

    #region Virtual Methods 
    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 (가상 메서드)
    /// </summary>
    protected virtual void OnControlInitialized()
    {
        if (Marker == null) return;

        _log?.Info($"[OnControlInitialized] Marker.ShowTitle: {Marker.ShowTitle}");
        _log?.Info($"[OnControlInitialized] Marker 타입: {Marker.GetType().Name}");

        MarkerTitle = Marker.Title ?? "Unnamed Marker";
        Width = Marker.Width;
        Height = Marker.Height;
        IsSelected = Marker.IsSelected;
        MarkerState = Marker.OperationState;
        RotationAngle = Marker.Bearing;
        Visibility = VisibilityHelper.ToVisibility(Marker.IsVisible);
        ShowShape = Marker.ShowShape;
        ShowTitle = Marker.ShowTitle;
        TitleSize = Marker.TitleSize;
        MarkerFill = ColorHelper.ToBrush(Marker.FillColor);
        MarkerStroke = ColorHelper.ToBrush(Marker.StrokeColor);
        MarkerStrokeThickness = Marker.StrokeThickness;
        EnableShapeAnimation = Marker.EnableShapeAnimation;
        _log?.Info($"[OnControlInitialized] 설정 후 ShowTitle: {ShowTitle}");


    }

    /// <summary>
    /// 마커 정보로부터 UI 업데이트 (기존 패턴 유지)
    /// </summary>
    protected virtual void UpdateFromMarker()
    {
        if (Marker == null || _isUpdatingFromMarker) return;

        _isUpdatingFromMarker = true;

        try
        {
            //다음 초기값 설정 (바인딩이 덮어쓰지 않음)
            OnControlInitialized();

            // 마커별 전용 업데이트 (추상 메서드 호출)
            UpdateFromSpecificMarker();

            // 상태에 따른 색상 설정
            UpdateMarkerAppearance();
        }
        finally
        {
            _isUpdatingFromMarker = false;
        }
    }

    /// <summary>
    /// 데이터 바인딩 설정 (기존 패턴 유지)
    /// </summary>
    protected virtual void SetupDataBindings()
    {
        if (Marker == null || _isUpdatingFromMarker) return;

        _isUpdatingFromMarker = true;

        try
        {
            //_log?.Info($"[SetupDataBindings] 바인딩 전 ShowTitle: {ShowTitle}");
            //_log?.Info($"[SetupDataBindings] 바인딩 전 Marker.ShowTitle: {Marker.ShowTitle}");


            // 공통 바인딩 (GMapCustomMarker 기본 속성)
            SetupPropertyBinding(MarkerTitleProperty, nameof(Marker.Title));
            SetupPropertyBinding(TitleSizeProperty, nameof(Marker.TitleSize));
            SetupPropertyBinding(WidthProperty, nameof(Marker.Width));
            SetupPropertyBinding(HeightProperty, nameof(Marker.Height));
            SetupPropertyBinding(IsSelectedProperty, nameof(Marker.IsSelected));
            SetupPropertyBinding(MarkerStateProperty, nameof(Marker.OperationState), BindingMode.OneWay);
            SetupPropertyBinding(RotationAngleProperty, nameof(Marker.Bearing));
            SetupPropertyBinding(ShowShapeProperty, nameof(Marker.ShowShape));
            SetupPropertyBinding(ShowTitleProperty, nameof(Marker.ShowTitle));

            //_log?.Info($"[SetupDataBindings] 바인딩 후 ShowTitle: {ShowTitle}");

            var colorConverter = new ColorTypeToBrushConverter();
            var visibilityConverter = new System.Windows.Controls.BooleanToVisibilityConverter();
            // 색상은 단방향 바인딩 (Marker -> Control)
            var fillBinding = new Binding(nameof(Marker.FillColor))
            {
                Source = Marker,
                Mode = BindingMode.OneWay,
                Converter = colorConverter
            };

            var strokeBinding = new Binding(nameof(Marker.StrokeColor))
            {
                Source = Marker,
                Mode = BindingMode.OneWay,
                Converter = colorConverter
            };

            var visibilityBinding = new Binding(nameof(Marker.IsVisible))
            {
                Source = Marker,
                Mode = BindingMode.OneWay,
                Converter = visibilityConverter
            };

            SetBinding(MarkerFillProperty, fillBinding);
            SetBinding(MarkerStrokeProperty, strokeBinding);
            SetBinding(VisibilityProperty, visibilityBinding);
            SetupPropertyBinding(MarkerStrokeThicknessProperty, nameof(Marker.StrokeThickness));

            // 마커별 전용 바인딩 (추상 메서드 호출)
            SetupSpecificBindings();
        }
        finally
        {
            _isUpdatingFromMarker = false;
        }

        
    }

    /// <summary>
    /// 마커 모양 업데이트 (기존 패턴 유지)
    /// </summary>
    protected virtual void UpdateMarkerAppearance()
    {
        MarkerFill = MarkerState switch
        {
            EnumOperationState.ACTIVATED => Brushes.Green,
            EnumOperationState.DEACTIVATED => Brushes.Gray,
            _ => Brushes.Red
        };
    }

    

    /// <summary>
    /// 마커 클릭 처리 (기존 패턴 유지)
    /// </summary>
    protected virtual void OnMarkerClicked(MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        var timeDiff = (now - LastClickTime).TotalMilliseconds;

        if (timeDiff <= DoubleClickInterval)
        {
            OnMarkerDoubleClicked(e);
        }
        else
        {
            OnMarkerSingleClicked(e);
        }

        LastClickTime = now;
    }

    /// <summary>
    /// 단일 클릭 처리 (Generic 이벤트 발생)
    /// </summary>
    protected virtual void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        ToggleSelection();
        if (EnableShapeAnimation)
        {
            TriggerClickAnimation();
        }
        MarkerClick?.Invoke(this, new MarkerClickEventArgs(Marker, e));
    }

    /// <summary>
    /// 더블클릭 처리 (Generic 이벤트 발생)
    /// </summary>
    protected virtual void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        MarkerDoubleClick?.Invoke(this, new MarkerClickEventArgs(Marker, e));
    }

    /// <summary>
    /// 선택 상태 변경 처리 (Generic 이벤트 발생)
    /// </summary>
    protected virtual void OnSelectionChanged(bool isSelected)
    {
        if (Marker == null) return;

        Marker.IsSelected = isSelected;
        SelectionChanged?.Invoke(this, new MarkerSelectionChangedEventArgs(Marker, isSelected));
    }
    #endregion

    #region Public Override Methods
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (ShowTitle)
        {
            var labelContainer = GetTemplateChild("PART_LabelContainer") as Border;
            _log?.Info($"ShowTitle=true, PART_LabelContainer Visibility: {labelContainer?.Visibility}");
            _log?.Info($"MarkerTitle: '{MarkerTitle}'");
        }

        InvalidateVisual();
    }
    #endregion

    #region Protected Utility Methods

    /// <summary>
    /// 속성 바인딩 설정
    /// </summary>
    protected void SetupPropertyBinding(DependencyProperty targetProperty, string sourcePropertyName, BindingMode mode = BindingMode.TwoWay)
    {
        var binding = new Binding(sourcePropertyName)
        {
            Source = Marker,
            Mode = mode,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        SetBinding(targetProperty, binding);
    }

    /// <summary>
    /// 선택 상태 토글
    /// </summary>
    protected void ToggleSelection()
    {
        IsSelected = !IsSelected;
    }

    // IMarkerControl 구현
    public IEditableMarker EditableMarker => Marker;
    public FrameworkElement VisualElement => this;

    public virtual void RefreshFromMarker()
    {
        UpdateFromMarker(); // 기존 메서드 활용
    }

    #endregion

    #region Override Methods

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (Marker != null)
        {
            Marker.Width = ActualWidth;
            Marker.Height = ActualHeight;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // EditMode OFF 시 이벤트 투과 — 맵 패닝/줌 허용 (방어적 안전장치)
        var mapControl = FindParentMapControl();
        if (mapControl != null && !mapControl.IsEditMode)
            return;

        try
        {
            Focus();
            OnMarkerClicked(e);

            // Generic 이벤트 발생
            MarkerClick?.Invoke(this, new MarkerClickEventArgs(Marker, e));

            if (mapControl != null && Marker != null)
            {
                _log?.Info($"마커 컨트롤에서 부모에게 클릭 이벤트 전달: {Marker.Title} ({Marker.Width} x {Marker.Height})");
                mapControl.TriggerMarkerClicked(Marker);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 클릭 처리 중 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 우클릭 처리 — 컨텍스트 메뉴 트리거 (모든 마커 타입 공통)
    /// EditMode OFF 시 IsHitTestVisible=false이므로 호출 안 됨 → GMapCustomControl 수동 히트테스트가 처리
    /// </summary>
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        var mapControl = FindParentMapControl();
        if (mapControl == null || Marker == null) return;

        e.Handled = true;
        mapControl.TriggerMarkerRightClicked(Marker);
    }

    protected void TriggerClickAnimation()
    {

        if (!EnableShapeAnimation) return;

        try
        {
            TransformGroup transformGroup;
            ScaleTransform scaleTransform;
            RotateTransform existingRotate = null;

            // 기존 Transform 구조 분석
            if (RenderTransform is TransformGroup existingGroup)
            {
                // 기존 TransformGroup 사용
                transformGroup = existingGroup;
                existingRotate = transformGroup.Children.OfType<RotateTransform>().FirstOrDefault();
                scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
            }
            else if (RenderTransform is RotateTransform rotateOnly)
            {
                // 기존 RotateTransform만 있는 경우
                existingRotate = rotateOnly;
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(existingRotate); // 기존 회전 보존
                scaleTransform = null;
            }
            else
            {
                // Transform이 없는 경우
                transformGroup = new TransformGroup();
                scaleTransform = null;
            }

            // ScaleTransform 추가/수정
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                transformGroup.Children.Add(scaleTransform);
            }

            // TransformGroup 적용 (기존 회전 유지됨)
            RenderTransform = transformGroup;
            RenderTransformOrigin = new Point(0.5, 0.5);

            // 애니메이션 실행
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            System.Diagnostics.Debug.WriteLine($"애니메이션 실행 - 기존 회전 보존: {existingRotate?.Angle ?? 0:F1}°");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"클릭 애니메이션 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 부모 GMapCustomControl 찾기
    /// </summary>
    protected GMapCustomControl? FindParentMapControl()
    {
        DependencyObject parent = this;
        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            if (parent is GMapCustomControl mapControl)
            {
                return mapControl;
            }
        }
        return null;
    }
    #endregion

    #region Static Property Changed Callbacks

    protected static void OnMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBaseControl<T> control && !control._isUpdatingFromMarker)
        {
            control._isUpdatingFromMarker = true;
            try
            {
                control.UpdateFromMarker();
            }
            finally
            {
                control._isUpdatingFromMarker = false;
            }
        }
    }

    private static void OnMarkerFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBaseControl<T> control && !control._isUpdatingFromMarker)
        {
            
        }
    }
    private static void OnMarkerStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //if (d is GMapMarkerBaseControl<T> control && !control._isUpdatingFromMarker)
        //{
        //    // 마커에 값 전파
        //    if (control.Marker != null)
        //    {
        //        // ColorHelper로 Brush를 EnumColorType으로 변환
        //        control.Marker.StrokeColor = ColorHelper.HexToColorType(((Brush)e.NewValue).ToString());
        //    }

        //    // UI 강제 새로고침
        //    control.InvalidateVisual();
        //}

        if (d is GMapMarkerLineControl lineControl && lineControl.MainPolyline != null)
        {
            lineControl.MainPolyline.Stroke = e.NewValue as Brush;
        }
        else if(d is GMapMarkerPidsGroupControl pGroupControl && pGroupControl.MainPolyline != null)
        {
            pGroupControl.MainPolyline.Stroke = e.NewValue as Brush;
        }
    }

    private static void OnMarkerStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBaseControl<T> control)
        {
            System.Diagnostics.Debug.WriteLine($"OnMarkerStrokeThicknessChanged: {e.OldValue} → {e.NewValue}");

            // 마커 객체가 있으면 동기화
            if (control.Marker != null && !control._isUpdatingFromMarker)
            {
                control.Marker.StrokeThickness = (double)e.NewValue;
                System.Diagnostics.Debug.WriteLine($"마커 StrokeThickness 업데이트: {control.Marker.StrokeThickness}");
            }


            // LineControl인 경우 Polyline 직접 업데이트
            if (control is GMapMarkerLineControl lineControl && lineControl.MainPolyline != null)
            {
                lineControl.MainPolyline.StrokeThickness = (double)e.NewValue;
                System.Diagnostics.Debug.WriteLine($"Polyline StrokeThickness 업데이트: {e.NewValue}");
            }
            else if (d is GMapMarkerPidsGroupControl pGroupControl && pGroupControl.MainPolyline != null)
            {
                pGroupControl.MainPolyline.StrokeThickness = (double)e.NewValue;
                System.Diagnostics.Debug.WriteLine($"Polyline StrokeThickness 업데이트: {e.NewValue}");
            }

            // UI 강제 새로고침
            control.InvalidateVisual();
        }
    }

    protected static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBaseControl<T> control)
        {
            control.OnSelectionChanged((bool)e.NewValue);
        }
    }

    protected static void OnMarkerStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBaseControl<T> control)
        {
            control.UpdateMarkerAppearance();
        }
    }

    protected static void OnRotationAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"OnRotationAngleChanged: {e.OldValue} -> {e.NewValue}");

        if (d is GMapMarkerBaseControl<T> control)
        {
            control.RotationAngle = (double)e.NewValue;
            //var angle = (double)e.NewValue; // 이미 CoerceValueCallback에서 반올림됨
            var rotateTransform = new RotateTransform(control.RotationAngle);
            control.RenderTransform = rotateTransform;
            control.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }

    private static void OnShowTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"OnShowTitleChanged: {e.OldValue} -> {e.NewValue}");

        if (d is GMapMarkerBaseControl<T> control)
        {
            System.Diagnostics.Debug.WriteLine($"  마커에 전파: Marker.ShowTitle = {control.ShowTitle}");

            control.ShowTitle = (bool)e.NewValue;
        }
    }

    // 공통 값 강제 변환 메서드
    private static object CoerceDoubleValue(DependencyObject d, object value)
    {
        return Math.Round((double)value, 2);
    }
    #endregion
    protected bool _isUpdatingFromMarker = false;  // 필드 추가
    protected ILogService? _log;
}
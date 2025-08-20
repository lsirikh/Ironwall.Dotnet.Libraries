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
            new PropertyMetadata(Brushes.Red));

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
            new PropertyMetadata(Brushes.White));

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
            new PropertyMetadata(2.0));

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
            new PropertyMetadata(EnumOperationState.ACTIVE, OnMarkerStateChanged));

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
            new PropertyMetadata(false));

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

    /// <summary>
    /// 회전 각도
    /// </summary>
    public double RotationAngle
    {
        get { return (double)GetValue(RotationAngleProperty); }
        set { SetValue(RotationAngleProperty, value); }
    }

    public static readonly DependencyProperty RotationAngleProperty =
        DependencyProperty.Register("RotationAngle", typeof(double), typeof(GMapMarkerBaseControl<T>),
            new PropertyMetadata(0.0, OnRotationAngleChanged));

    #endregion

    #region Events

    /// <summary>
    /// 마커 클릭 이벤트 - 간단한 이벤트 아규먼트 사용
    /// </summary>
    public event EventHandler<MarkerClickEventArgs> MarkerClick;

    /// <summary>
    /// 마커 더블클릭 이벤트
    /// </summary>
    public event EventHandler<MarkerClickEventArgs> MarkerDoubleClick;

    /// <summary>
    /// 마커 선택 상태 변경 이벤트
    /// </summary>
    public event EventHandler<MarkerSelectionChangedEventArgs> SelectionChanged;


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
        InitializeControl();
    }

    /// <summary>
    /// 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="marker">연결할 마커</param>
    protected GMapMarkerBaseControl(T marker) : this()
    {
        Marker = marker;
        OnControlInitialized();
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

    #region Virtual Methods (기존 패턴 유지)

    /// <summary>
    /// 컨트롤 초기화 (가상 메서드)
    /// </summary>
    protected virtual void InitializeControl()
    {
        // 기본값 설정
        Width = 32;
        Height = 32;
        MarkerFill = Brushes.Red;
        MarkerStroke = Brushes.White;
        MarkerStrokeThickness = 2;
        MarkerTitle = "Marker";

        // 마우스 이벤트 활성화
        IsHitTestVisible = true;

        
    }

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 (가상 메서드)
    /// </summary>
    protected virtual void OnControlInitialized()
    {
        
        // 상속 클래스에서 구현
    }

    /// <summary>
    /// 마커 정보로부터 UI 업데이트 (기존 패턴 유지)
    /// </summary>
    protected virtual void UpdateFromMarker()
    {
        if (Marker == null) return;

        try
        {
            // 공통 속성 동기화 (GMapCustomMarker 기본 속성)
            MarkerTitle = Marker.Title ?? "Unnamed Marker";
            Width = Marker.Width;
            Height = Marker.Height;
            IsSelected = Marker.IsSelected;
            MarkerState = Marker.OperationState;
            RotationAngle = Marker.Bearing;
            ShowShape = Marker.ShowShape;
            ShowTitle = Marker.ShowTitle;

            // 마커별 전용 업데이트 (추상 메서드 호출)
            UpdateFromSpecificMarker();

            // 상태에 따른 색상 설정
            UpdateMarkerAppearance();

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    /// <summary>
    /// 데이터 바인딩 설정 (기존 패턴 유지)
    /// </summary>
    protected virtual void SetupDataBindings()
    {
        if (Marker == null) return;

        // 공통 바인딩 (GMapCustomMarker 기본 속성)
        SetupPropertyBinding(WidthProperty, nameof(Marker.Width));
        SetupPropertyBinding(HeightProperty, nameof(Marker.Height));
        SetupPropertyBinding(IsSelectedProperty, nameof(Marker.IsSelected));
        SetupPropertyBinding(RotationAngleProperty, nameof(Marker.Bearing));
        SetupPropertyBinding(ShowShapeProperty, nameof(Marker.ShowShape));
        SetupPropertyBinding(ShowTitleProperty, nameof(Marker.ShowTitle));

        // 마커별 전용 바인딩 (추상 메서드 호출)
        SetupSpecificBindings();
    }

    /// <summary>
    /// 마커 모양 업데이트 (기존 패턴 유지)
    /// </summary>
    protected virtual void UpdateMarkerAppearance()
    {
        MarkerFill = MarkerState switch
        {
            EnumOperationState.ACTIVE => Brushes.Green,
            EnumOperationState.DEACTIVE => Brushes.Gray,
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
            HandleSingleClick(e);
        }

        LastClickTime = now;
    }

    /// <summary>
    /// 단일 클릭 처리 (Generic 이벤트 발생)
    /// </summary>
    protected virtual void HandleSingleClick(MouseButtonEventArgs e)
    {
        ToggleSelection();
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
        if (Marker != null)
        {
            Marker.IsSelected = isSelected;
        }

        SelectionChanged?.Invoke(this, new MarkerSelectionChangedEventArgs(Marker, isSelected));
    }

    #endregion

    #region Protected Utility Methods

    /// <summary>
    /// 속성 바인딩 설정
    /// </summary>
    protected void SetupPropertyBinding(DependencyProperty targetProperty, string sourcePropertyName)
    {
        var binding = new Binding(sourcePropertyName)
        {
            Source = Marker,
            Mode = BindingMode.TwoWay,
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

        try
        {
            Focus();
            OnMarkerClicked(e);

            // Generic 이벤트 발생
            MarkerClick?.Invoke(this, new MarkerClickEventArgs(Marker, e));

            var mapControl = FindParentMapControl();
            if (mapControl != null && Marker != null)
            {
                System.Diagnostics.Debug.WriteLine($"마커 컨트롤에서 부모에게 클릭 이벤트 전달: {Marker.Title}");
                mapControl.TriggerMarkerClicked(Marker);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"마커 클릭 처리 중 오류: {ex.Message}");
        }
    }


    #endregion

    #region Private Methods

    /// <summary>
    /// 부모 GMapCustomControl 찾기
    /// </summary>
    private GMapCustomControl FindParentMapControl()
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
        if (d is GMapMarkerBaseControl<T> control)
        {
            control.UpdateFromMarker();
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
        if (d is GMapMarkerBaseControl<T> control)
        {
            var angle = (double)e.NewValue;
            var rotateTransform = new RotateTransform(angle);
            control.RenderTransform = rotateTransform;
            control.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }

    #endregion
}
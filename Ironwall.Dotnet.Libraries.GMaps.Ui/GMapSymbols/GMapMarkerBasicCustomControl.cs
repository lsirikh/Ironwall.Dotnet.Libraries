using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>
/// 지도 마커를 위한 확장 가능한 기본 UI 컨트롤
/// - 상속을 통한 커스텀 마커 구현을 위한 기반 클래스
/// - 가상 메서드와 보호된 멤버를 통한 확장성 제공
/// </summary>
public class GMapMarkerBasicCustomControl : Control
{
    #region Static Constructor
    static GMapMarkerBasicCustomControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerBasicCustomControl),
            new FrameworkPropertyMetadata(typeof(GMapMarkerBasicCustomControl)));
    }
    #endregion

    #region Dependency Properties

    /// <summary>
    /// 연결된 마커 객체
    /// </summary>
    public GMapCustomMarker Marker
    {
        get { return (GMapCustomMarker)GetValue(MarkerProperty); }
        set { SetValue(MarkerProperty, value); }
    }

    public static readonly DependencyProperty MarkerProperty =
        DependencyProperty.Register("Marker", typeof(GMapCustomMarker), typeof(GMapMarkerBasicCustomControl),
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
        DependencyProperty.Register("MarkerTitle", typeof(string), typeof(GMapMarkerBasicCustomControl),
            new PropertyMetadata("Marker"));

    /// <summary>
    /// 마커 메인 색상
    /// </summary>
    public Brush MarkerFill
    {
        get { return (Brush)GetValue(MarkerFillProperty); }
        set { SetValue(MarkerFillProperty, value); }
    }

    public static readonly DependencyProperty MarkerFillProperty =
        DependencyProperty.Register("MarkerFill", typeof(Brush), typeof(GMapMarkerBasicCustomControl),
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
        DependencyProperty.Register("MarkerStroke", typeof(Brush), typeof(GMapMarkerBasicCustomControl),
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
        DependencyProperty.Register("MarkerStrokeThickness", typeof(double), typeof(GMapMarkerBasicCustomControl),
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
        DependencyProperty.Register("IsSelected", typeof(bool), typeof(GMapMarkerBasicCustomControl),
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
        DependencyProperty.Register("MarkerState", typeof(EnumOperationState), typeof(GMapMarkerBasicCustomControl),
            new PropertyMetadata(EnumOperationState.ACTIVE, OnMarkerStateChanged));

    /// <summary>
    /// 라벨 표시 여부
    /// </summary>
    public bool ShowLabel
    {
        get { return (bool)GetValue(ShowLabelProperty); }
        set { SetValue(ShowLabelProperty, value); }
    }

    public static readonly DependencyProperty ShowLabelProperty =
        DependencyProperty.Register("ShowLabel", typeof(bool), typeof(GMapMarkerBasicCustomControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 회전 각도
    /// </summary>
    public double RotationAngle
    {
        get { return (double)GetValue(RotationAngleProperty); }
        set { SetValue(RotationAngleProperty, value); }
    }

    public static readonly DependencyProperty RotationAngleProperty =
        DependencyProperty.Register("RotationAngle", typeof(double), typeof(GMapMarkerBasicCustomControl),
            new PropertyMetadata(0.0, OnRotationAngleChanged));

    #endregion

    #region Events

    /// <summary>
    /// 마커 클릭 이벤트
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

    #region Protected Fields (상속 클래스에서 접근 가능)
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
    public GMapMarkerBasicCustomControl()
    {
        InitializeControl();
    }

    /// <summary>
    /// 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="marker">연결할 마커</param>
    public GMapMarkerBasicCustomControl(GMapCustomMarker marker) : this()
    {
        Marker = marker;
        UpdateFromMarker();
        // 데이터 바인딩 설정
        SetupDataBindings();
    }

    #endregion

    #region Virtual Methods (상속 클래스에서 오버라이드 가능)

    /// <summary>
    /// 컨트롤 초기화 (가상 메서드 - 상속 클래스에서 오버라이드 가능)
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
               
        OnControlInitialized();
    }

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출 (상속 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnControlInitialized()
    {
        // 상속 클래스에서 구현
    }

    /// <summary>
    /// 마커 정보로부터 UI 업데이트 (가상 메서드)
    /// </summary>
    protected virtual void UpdateFromMarker()
    {
        if (Marker == null) return;

        try
        {
            // 기본 속성 동기화
            MarkerTitle = Marker.Title ?? "Unnamed Marker";
            Width = Marker.Width;
            Height = Marker.Height;

            IsSelected = Marker.IsSelected;
            MarkerState = Marker.OperationState;
            RotationAngle = Marker.Bearing;

            // 상태에 따른 색상 설정
            UpdateMarkerAppearance();

            OnMarkerUpdated();
        }
        catch (Exception ex)
        {
            OnUpdateError(ex);
        }
    }

    /// <summary>
    /// 마커 모양 업데이트 (상속 클래스에서 오버라이드 가능)
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
    /// 마커 업데이트 완료 후 호출 (상속 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnMarkerUpdated()
    {
        // 상속 클래스에서 구현
    }

    /// <summary>
    /// 업데이트 에러 처리 (상속 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnUpdateError(Exception ex)
    {
        // 기본 에러 처리
        System.Diagnostics.Debug.WriteLine($"마커 업데이트 오류: {ex.Message}");
    }

    /// <summary>
    /// 마커 클릭 처리 (가상 메서드)
    /// </summary>
    protected virtual void OnMarkerClicked(MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        var timeDiff = (now - LastClickTime).TotalMilliseconds;

        if (timeDiff <= DoubleClickInterval)
        {
            // 더블클릭
            OnMarkerDoubleClicked(e);
        }
        else
        {
            // 단일클릭
            HandleSingleClick(e);
        }

        LastClickTime = now;
    }

    /// <summary>
    /// 단일 클릭 처리 (상속 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual void HandleSingleClick(MouseButtonEventArgs e)
    {
        // 선택 상태 토글
        ToggleSelection();

        // 이벤트 발생
        MarkerClick?.Invoke(this, new MarkerClickEventArgs(this, e));
    }

    /// <summary>
    /// 더블클릭 처리 (상속 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
       
        // 이벤트 발생
        MarkerDoubleClick?.Invoke(this, new MarkerClickEventArgs(this, e));
    }

    /// <summary>
    /// 선택 상태 변경 처리 (가상 메서드)
    /// </summary>
    protected virtual void OnSelectionChanged(bool isSelected)
    {
        // 마커 객체와 동기화
        if (Marker != null)
        {
            Marker.IsSelected = isSelected;
        }


        // 이벤트 발생
        SelectionChanged?.Invoke(this, new MarkerSelectionChangedEventArgs(this, isSelected));
    }

    #endregion
    
    #region Protected Utility Methods

    /// <summary>
    /// 데이터 바인딩 설정
    /// </summary>
    protected virtual void SetupDataBindings()
    {
        if (Marker == null) return;

        // Width/Height 개별 바인딩 (MarkerSize 바인딩 제거)
        SetupPropertyBinding(WidthProperty, nameof(Marker.Width));
        SetupPropertyBinding(HeightProperty, nameof(Marker.Height));

        // 바인딩 설정을 메서드로 분리
        SetupPropertyBinding(IsSelectedProperty, nameof(Marker.IsSelected));
        SetupPropertyBinding(RotationAngleProperty, nameof(Marker.Bearing));
    }

    private void SetupPropertyBinding(DependencyProperty targetProperty, string sourcePropertyName)
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
    #endregion

    #region Override Methods
    // GMapMarkerBasicCustomControl.cs - 크기 변경 즉시 반영
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // 마커 데이터와 동기화
        if (Marker != null)
        {
            Marker.Width = ActualWidth;
            Marker.Height = ActualHeight;
        }
    }

    // 또는 PropertyChanged 핸들러 추가
    private void OnMarkerPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GMapCustomMarker.Width) ||
            e.PropertyName == nameof(GMapCustomMarker.Height))
        {
            UpdateFromMarker();
        }
    }

    /// <summary>
    /// 마우스 왼쪽 버튼 클릭 처리
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        try
        {
            Focus();
            OnMarkerClicked(e);

            // 단순히 이벤트만 발생 (책임 분리)
            MarkerClick?.Invoke(this, new MarkerClickEventArgs(this, e));

            // 부모 컨트롤에 알리기 (기존 방식 유지)
            var mapControl = FindParentMapControl() ;
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

    /// <summary>
    /// 부모 GMapCustomControl 찾기
    /// </summary>
    private GMapCustomControl? FindParentMapControl()
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

    /// <summary>
    /// 마우스 진입 처리
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

    }

    /// <summary>
    /// 마우스 떠남 처리
    /// </summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

    }

    #endregion
    #region Static Property Changed Callbacks

    private static void OnMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBasicCustomControl control)
        {
            control.UpdateFromMarker(); // 마커 데이터로 UI 업데이트
        }
    }
       
    private static void OnMarkerSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBasicCustomControl control)
        {
            var newSize = (double)e.NewValue;
            control.Width = newSize;
            control.Height = newSize;

            // 마커 데이터도 동기화
            if (control.Marker != null)
            {
                control.Marker.Width = newSize;
                control.Marker.Height = newSize;
            }
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBasicCustomControl control)
        {
            control.OnSelectionChanged((bool)e.NewValue);
        }
    }

    private static void OnMarkerStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBasicCustomControl control)
        {
            control.UpdateMarkerAppearance();
        }
    }

    private static void OnRotationAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerBasicCustomControl control)
        {
            var angle = (double)e.NewValue;
            var rotateTransform = new RotateTransform(angle);
            control.RenderTransform = rotateTransform;
            control.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }

    #endregion
}

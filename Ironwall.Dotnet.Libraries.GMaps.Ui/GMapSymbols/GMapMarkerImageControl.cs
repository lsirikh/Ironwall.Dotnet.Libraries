using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
   Purpose      : Image Overlay 마커용 CustomControl (Phase 24)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Note: GMapImageMarker는 IEditableMarker를 구현하지 않으므로
         GMapMarkerBaseControl<T>를 상속하지 않고 독립적으로 구현
****************************************************************************/

/// <summary>
/// Image Overlay 마커용 WPF CustomControl
/// </summary>
/// <remarks>
/// - GMapImageMarker와 바인딩
/// - 이미지 표시, 투명도, 회전 지원
/// - 선택 상태 표시
/// </remarks>
public class GMapMarkerImageControl : Control
{
    #region Static Constructor

    static GMapMarkerImageControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerImageControl),
            new FrameworkPropertyMetadata(typeof(GMapMarkerImageControl)));
    }

    #endregion

    #region Dependency Properties

    /// <summary>
    /// 연결된 마커 객체
    /// </summary>
    public GMapImageMarker? Marker
    {
        get => (GMapImageMarker?)GetValue(MarkerProperty);
        set => SetValue(MarkerProperty, value);
    }

    public static readonly DependencyProperty MarkerProperty =
        DependencyProperty.Register(nameof(Marker), typeof(GMapImageMarker), typeof(GMapMarkerImageControl),
            new PropertyMetadata(null, OnMarkerChanged));

    /// <summary>
    /// 이미지 소스
    /// </summary>
    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(GMapMarkerImageControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 이미지 투명도 (0.0 ~ 1.0)
    /// </summary>
    public double ImageOpacity
    {
        get => (double)GetValue(ImageOpacityProperty);
        set => SetValue(ImageOpacityProperty, value);
    }

    public static readonly DependencyProperty ImageOpacityProperty =
        DependencyProperty.Register(nameof(ImageOpacity), typeof(double), typeof(GMapMarkerImageControl),
            new PropertyMetadata(1.0));

    /// <summary>
    /// 이미지 회전 각도 (도)
    /// </summary>
    public double ImageRotation
    {
        get => (double)GetValue(ImageRotationProperty);
        set => SetValue(ImageRotationProperty, value);
    }

    public static readonly DependencyProperty ImageRotationProperty =
        DependencyProperty.Register(nameof(ImageRotation), typeof(double), typeof(GMapMarkerImageControl),
            new PropertyMetadata(0.0, OnImageRotationChanged));

    /// <summary>
    /// 마커 제목
    /// </summary>
    public string? MarkerTitle
    {
        get => (string?)GetValue(MarkerTitleProperty);
        set => SetValue(MarkerTitleProperty, value);
    }

    public static readonly DependencyProperty MarkerTitleProperty =
        DependencyProperty.Register(nameof(MarkerTitle), typeof(string), typeof(GMapMarkerImageControl),
            new PropertyMetadata("Image"));

    /// <summary>
    /// 선택 상태
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(GMapMarkerImageControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    /// <summary>
    /// 라벨 표시 여부
    /// </summary>
    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public static readonly DependencyProperty ShowTitleProperty =
        DependencyProperty.Register(nameof(ShowTitle), typeof(bool), typeof(GMapMarkerImageControl),
            new PropertyMetadata(false));

    #endregion

    #region Events

    /// <summary>
    /// 마커 클릭 이벤트
    /// </summary>
    public event EventHandler<ImageMarkerClickEventArgs>? MarkerClick;

    /// <summary>
    /// 마커 더블클릭 이벤트
    /// </summary>
    public event EventHandler<ImageMarkerClickEventArgs>? MarkerDoubleClick;

    /// <summary>
    /// 마커 선택 상태 변경 이벤트
    /// </summary>
    public event EventHandler<bool>? SelectionChanged;

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMarkerImageControl()
    {
        _log = IoC.Get<ILogService>();
    }

    /// <summary>
    /// 마커와 함께 생성
    /// </summary>
    /// <param name="marker">연결할 GMapImageMarker</param>
    public GMapMarkerImageControl(GMapImageMarker marker) : this()
    {
        Marker = marker;
        UpdateFromMarker();
        SetupDataBindings();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// IImageEditableMarker 접근용 속성
    /// </summary>
    public IImageEditableMarker? ImageEditableMarker => Marker;

    /// <summary>
    /// Visual Element 접근용
    /// </summary>
    public FrameworkElement VisualElement => this;

    #endregion

    #region Public Methods

    /// <summary>
    /// 마커 정보로 UI 새로고침
    /// </summary>
    public void RefreshFromMarker()
    {
        UpdateFromMarker();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// 마커 정보로부터 UI 업데이트
    /// </summary>
    protected virtual void UpdateFromMarker()
    {
        if (Marker == null || _isUpdatingFromMarker) return;

        _isUpdatingFromMarker = true;
        try
        {
            MarkerTitle = Marker.Title ?? "Image";
            Width = Marker.Width;
            Height = Marker.Height;
            ImageOpacity = Marker.Opacity;
            ImageRotation = Marker.Bearing;
            ImageSource = Marker.ImageSource;
            IsSelected = Marker.IsSelected;
            Visibility = Marker.IsVisible ? Visibility.Visible : Visibility.Collapsed;

            _log?.Info($"GMapMarkerImageControl 업데이트: {MarkerTitle}, {Width}x{Height}, Opacity={ImageOpacity}");
        }
        finally
        {
            _isUpdatingFromMarker = false;
        }
    }

    /// <summary>
    /// 데이터 바인딩 설정
    /// </summary>
    protected virtual void SetupDataBindings()
    {
        if (Marker == null) return;

        // 기본 속성 바인딩
        SetupPropertyBinding(MarkerTitleProperty, nameof(Marker.Title));
        SetupPropertyBinding(WidthProperty, nameof(Marker.Width));
        SetupPropertyBinding(HeightProperty, nameof(Marker.Height));
        SetupPropertyBinding(ImageOpacityProperty, nameof(Marker.Opacity));
        SetupPropertyBinding(ImageRotationProperty, nameof(Marker.Bearing));
        SetupPropertyBinding(IsSelectedProperty, nameof(Marker.IsSelected));

        // Visibility 바인딩 (Converter 사용)
        var visibilityBinding = new Binding(nameof(Marker.IsVisible))
        {
            Source = Marker,
            Mode = BindingMode.OneWay,
            Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
        };
        SetBinding(VisibilityProperty, visibilityBinding);

        // ImageSource는 직접 바인딩 (읽기 전용)
        var imageSourceBinding = new Binding(nameof(Marker.ImageSource))
        {
            Source = Marker,
            Mode = BindingMode.OneWay
        };
        SetBinding(ImageSourceProperty, imageSourceBinding);
    }

    /// <summary>
    /// 속성 바인딩 헬퍼
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

    #endregion

    #region Override Methods

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        try
        {
            Focus();

            var now = DateTime.Now;
            var timeDiff = (now - _lastClickTime).TotalMilliseconds;

            if (timeDiff <= DoubleClickInterval)
            {
                // 더블클릭
                MarkerDoubleClick?.Invoke(this, new ImageMarkerClickEventArgs(Marker, e));
            }
            else
            {
                // 단일 클릭 - 선택 토글
                IsSelected = !IsSelected;
                MarkerClick?.Invoke(this, new ImageMarkerClickEventArgs(Marker, e));
            }

            _lastClickTime = now;

            // 부모 맵 컨트롤에 이벤트 전달
            var mapControl = FindParentMapControl();
            if (mapControl != null && Marker != null)
            {
                _log?.Info($"Image 마커 클릭 이벤트 전달: {Marker.Title}");
                // mapControl.TriggerMarkerClicked(Marker); // IEditableMarker가 아니므로 별도 처리 필요
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            _log?.Error($"Image 마커 클릭 처리 중 오류: {ex.Message}");
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        InvalidateVisual();
    }

    #endregion

    #region Private Methods

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

    #endregion

    #region Static Callbacks

    private static void OnMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerImageControl control && !control._isUpdatingFromMarker)
        {
            control._isUpdatingFromMarker = true;
            try
            {
                control.UpdateFromMarker();
                control.SetupDataBindings();
            }
            finally
            {
                control._isUpdatingFromMarker = false;
            }
        }
    }

    private static void OnImageRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerImageControl control)
        {
            var rotateTransform = new RotateTransform((double)e.NewValue);
            control.RenderTransform = rotateTransform;
            control.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerImageControl control)
        {
            var isSelected = (bool)e.NewValue;
            if (control.Marker != null)
            {
                control.Marker.IsSelected = isSelected;
            }
            control.SelectionChanged?.Invoke(control, isSelected);
        }
    }

    #endregion

    #region Fields

    private bool _isUpdatingFromMarker;
    private DateTime _lastClickTime;
    private const int DoubleClickInterval = 500;
    private readonly ILogService? _log;

    #endregion
}

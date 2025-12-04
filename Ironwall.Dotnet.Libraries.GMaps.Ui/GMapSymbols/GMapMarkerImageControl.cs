using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
   Purpose      : Image Overlay 마커용 CustomControl (Phase 24, Phase 29 개선)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Note: GMapCustomImage와 동일한 직접 렌더링 방식 적용 (Phase 29)
         - OnRender에서 ImageBounds → 화면좌표 직접 변환
         - Position을 TopLeft로 설정하여 Offset 동기화 문제 해결
****************************************************************************/

/// <summary>
/// Image Overlay 마커용 WPF CustomControl
/// </summary>
/// <remarks>
/// Phase 29: GMapCustomImage와 동일한 직접 렌더링 방식 적용
/// - OnRender()에서 ImageBounds를 화면 좌표로 직접 변환하여 이미지 그리기
/// - 줌 변경 시 InvalidateVisual()로 재렌더링 트리거
/// - Position을 ImageBounds.LocationTopLeft로 설정, Offset = (0,0)
/// </remarks>
public class GMapMarkerImageControl : GMapMarkerBaseControl<GMapImageMarker>
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
    /// 이미지 소스
    /// </summary>
    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(GMapMarkerImageControl),
            new PropertyMetadata(null, OnImageSourceChanged));

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerImageControl control)
        {
            control.InvalidateVisual();
        }
    }

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
            new PropertyMetadata(1.0, OnImageOpacityChanged));

    private static void OnImageOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerImageControl control)
        {
            control.InvalidateVisual();
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMarkerImageControl() : base()
    {
        InitializeRenderingSystem();
    }

    /// <summary>
    /// 마커와 함께 생성
    /// </summary>
    /// <param name="marker">연결할 GMapImageMarker</param>
    public GMapMarkerImageControl(GMapImageMarker marker) : base(marker)
    {
        InitializeRenderingSystem();
    }

    /// <summary>
    /// 렌더링 시스템 초기화 (Phase 29)
    /// </summary>
    private void InitializeRenderingSystem()
    {
        Loaded += OnControlLoaded;
        Unloaded += OnControlUnloaded;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// IImageEditableMarker 접근용 속성
    /// </summary>
    public IImageEditableMarker? ImageEditableMarker => Marker;

    #endregion

    #region Abstract Methods Implementation

    /// <summary>
    /// GMapImageMarker 전용 UI 업데이트 구현 (Phase 29 개선)
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        if (Marker == null) return;

        // Image 전용 속성 업데이트
        ImageOpacity = Marker.Opacity;
        ImageSource = Marker.ImageSource;

        // Phase 29: 초기 크기 설정 (OnRender에서 실제 크기 계산)
        // 지도 컨트롤이 있으면 정확한 크기 계산
        var mapControl = FindParentMapControl();
        if (mapControl != null)
        {
            UpdateGeometryFromBounds(mapControl);
        }
        else
        {
            // 지도 컨트롤 없으면 기본 크기 사용
            Width = Math.Max(Marker.Width, 50);
            Height = Math.Max(Marker.Height, 50);
        }

        _log?.Info($"GMapMarkerImageControl 업데이트: {MarkerTitle}, Size={Width:F0}x{Height:F0}, Opacity={ImageOpacity}");
    }

    /// <summary>
    /// GMapImageMarker 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        if (Marker == null) return;

        // Image 전용 바인딩
        SetupPropertyBinding(ImageOpacityProperty, nameof(Marker.Opacity));

        // ImageSource는 직접 바인딩 (읽기 전용)
        var imageSourceBinding = new Binding(nameof(Marker.ImageSource))
        {
            Source = Marker,
            Mode = BindingMode.OneWay
        };
        SetBinding(ImageSourceProperty, imageSourceBinding);
    }

    #endregion

    #region Override Methods - Direct Rendering (Phase 29)

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        InvalidateVisual();
    }

    /// <summary>
    /// 직접 렌더링 (Phase 29) - GMapCustomImage와 동일한 방식
    /// </summary>
    /// <remarks>
    /// GMapCustomControl.RenderSingleImageOverlay()와 동일한 원리:
    /// 1. ImageBounds의 TopLeft, BottomRight를 화면 좌표로 변환
    /// 2. 변환된 화면 좌표로 Rect 계산
    /// 3. DrawingContext.DrawImage()로 직접 그리기
    /// </remarks>
    protected override void OnRender(DrawingContext dc)
    {
        // 기본 렌더링 (배경, 테두리 등)
        base.OnRender(dc);

        if (Marker == null || ImageSource == null) return;

        var mapControl = FindParentMapControl();
        if (mapControl == null) return;

        try
        {
            // Phase 34: 픽셀 고정 모드 - Marker.Width/Height를 직접 사용
            // 변경 전: ImageBounds → FromLatLngToLocal() → 줌에 따라 다른 크기
            // 변경 후: Marker.Width/Height (사용자 설정 픽셀 값) 직접 사용
            double screenWidth = Marker.Width;
            double screenHeight = Marker.Height;

            // 크기 제한 적용 (최소 10px)
            const double MinSize = 10.0;
            if (screenWidth < MinSize) screenWidth = MinSize;
            if (screenHeight < MinSize) screenHeight = MinSize;

            // 컨트롤 크기 동기화 (Adorner 선택 영역용)
            Width = screenWidth;
            Height = screenHeight;

            // Phase 34: Offset은 Marker.Width/Height 기준으로 계산
            // Position(Center)에서 좌상단까지의 거리 = (-Width/2, -Height/2)
            Marker.Offset = new Point(-screenWidth / 2, -screenHeight / 2);

            // 이미지 그리기 영역 (컨트롤 로컬 좌표 기준)
            var imageRect = new Rect(0, 0, screenWidth, screenHeight);

            // 회전 처리
            if (Marker.Bearing != 0)
            {
                var centerX = screenWidth / 2;
                var centerY = screenHeight / 2;
                var rotateTransform = new RotateTransform(Marker.Bearing, centerX, centerY);
                dc.PushTransform(rotateTransform);
            }

            // 투명도 처리
            if (ImageOpacity < 1.0)
            {
                dc.PushOpacity(ImageOpacity);
            }

            // 이미지 그리기
            dc.DrawImage(ImageSource, imageRect);

            // Transform 스택 정리
            if (ImageOpacity < 1.0) dc.Pop();
            if (Marker.Bearing != 0) dc.Pop();
            
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GMapMarkerImageControl.OnRender 오류: {ex.Message}");
        }
    }

    #endregion

    #region Zoom Event Handling (Phase 29)

    private GMapCustomControl? _mapControl;

    /// <summary>
    /// 컨트롤 로드 시 지도 이벤트 구독
    /// </summary>
    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        _mapControl = FindParentMapControl();
        if (_mapControl != null)
        {
            // 줌 변경 시 재렌더링 트리거
            _mapControl.OnMapZoomChanged += OnMapZoomChanged;

            // 초기 렌더링
            UpdateGeometryFromBounds(_mapControl);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 컨트롤 언로드 시 이벤트 구독 해제
    /// </summary>
    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        if (_mapControl != null)
        {
            _mapControl.OnMapZoomChanged -= OnMapZoomChanged;
            _mapControl = null;
        }
    }

    /// <summary>
    /// 줌 변경 이벤트 핸들러 - 재렌더링 트리거
    /// </summary>
    private void OnMapZoomChanged()
    {
        // Phase 29 핵심: 줌 변경 시 InvalidateVisual() 호출
        // OnRender()가 다시 호출되어 최신 화면 좌표로 이미지 그리기
        InvalidateVisual();
    }

    /// <summary>
    /// ImageBounds 기반 크기 계산 (Phase 29)
    /// </summary>
    private void UpdateGeometryFromBounds(GMapControl mapControl)
    {
        if (Marker == null) return;

        try
        {
            // Phase 34: 픽셀 고정 모드 - Marker.Width/Height를 직접 사용
            // 변경 전: ImageBounds → FromLatLngToLocal() → 줌에 따라 다른 크기
            // 변경 후: Marker.Width/Height (사용자 설정 픽셀 값) 직접 사용
            double screenWidth = Marker.Width;
            double screenHeight = Marker.Height;

            // 크기 제한
            const double MinSize = 10.0;
            screenWidth = Math.Max(screenWidth, MinSize);
            screenHeight = Math.Max(screenHeight, MinSize);
            Width = screenWidth;
            Height = screenHeight;

            // Phase 34: Offset은 Marker.Width/Height 기준으로 계산
            Marker.Offset = new Point(-screenWidth / 2, -screenHeight / 2);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateGeometryFromBounds 오류: {ex.Message}");
        }
    }

    #endregion

    #region Legacy Method for Test Compatibility

    /// <summary>
    /// 지도 줌 변경 시 이미지 크기 업데이트 (테스트 호환성 유지)
    /// </summary>
    /// <remarks>
    /// Phase 29에서 OnRender 직접 렌더링으로 변경되었으나,
    /// 기존 테스트 호환성을 위해 메서드 시그니처 유지
    /// </remarks>
    protected void UpdateImageGeometry(GMapControl? mapControl)
    {
        if (mapControl != null)
        {
            UpdateGeometryFromBounds(mapControl);
            InvalidateVisual();
        }
    }

    #endregion
}

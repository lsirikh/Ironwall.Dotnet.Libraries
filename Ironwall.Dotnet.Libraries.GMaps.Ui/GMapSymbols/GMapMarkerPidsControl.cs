using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System.Windows.Data;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
   Purpose      : PIDS 마커 전용 컨트롤
   Created By   : GHLee                                                
   Created On   : 8/26/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// PIDS 마커 전용 컨트롤
/// - GMapMarkerBaseControl<GMapPidsMarker>를 상속받아 PIDS 특화 기능 제공
/// - FOV 시각화, 장비 타입별 UI 차별화
/// </summary>
public class GMapMarkerPidsControl : GMapMarkerBaseControl<GMapPidsMarker>
{
    #region Static Constructor
    static GMapMarkerPidsControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerPidsControl),
            new FrameworkPropertyMetadata(typeof(GMapMarkerPidsControl)));
    }
    #endregion

    #region Additional Dependency Properties

    /// <summary>
    /// 장비 타입
    /// </summary>
    public EnumDeviceType DeviceType
    {
        get { return (EnumDeviceType)GetValue(DeviceTypeProperty); }
        set { SetValue(DeviceTypeProperty, value); }
    }

    public static readonly DependencyProperty DeviceTypeProperty =
        DependencyProperty.Register("DeviceType", typeof(EnumDeviceType), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(EnumDeviceType.Fence, OnDeviceTypeChanged));

    /// <summary>
    /// 이벤트 상태 (애니메이션 트리거)
    /// </summary>
    public EnumEventStatus EventStatus
    {
        get { return (EnumEventStatus)GetValue(EventStatusProperty); }
        set { SetValue(EventStatusProperty, value); }
    }

    public static readonly DependencyProperty EventStatusProperty =
        DependencyProperty.Register("EventStatus", typeof(EnumEventStatus), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(EnumEventStatus.Normal, OnEventStatusChanged));

    /// <summary>
    /// FOV 표시 여부
    /// </summary>
    public bool ShowFOV
    {
        get { return (bool)GetValue(ShowFOVProperty); }
        set { SetValue(ShowFOVProperty, value); }
    }

    public static readonly DependencyProperty ShowFOVProperty =
        DependencyProperty.Register("ShowFOV", typeof(bool), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(false, OnShowFOVChanged));

    /// <summary>
    /// 감지 범위 (미터)
    /// </summary>
    public Brush FOVColor
    {
        get { return (Brush)GetValue(FOVColorProperty); }
        set { SetValue(FOVColorProperty, value); }
    }

    public static readonly DependencyProperty FOVColorProperty =
        DependencyProperty.Register("FOVColor", typeof(Brush), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(Brushes.Blue));


    /// <summary>
    /// 감지 영역 투명도
    /// </summary>
    public double FOVOpacity
    {
        get { return (double)GetValue(FOVOpacityProperty); }
        set { SetValue(FOVOpacityProperty, value); }
    }
    
    public static readonly DependencyProperty FOVOpacityProperty =
        DependencyProperty.Register("FOVOpacity", typeof(double), typeof(GMapMarkerPidsControl), new PropertyMetadata(0.0, OnFOVOpacityChanged));

   



    /// <summary>
    /// 감지 범위 (미터)
    /// </summary>
    public double DetectionRange
    {
        get { return (double)GetValue(DetectionRangeProperty); }
        set { SetValue(DetectionRangeProperty, value); }
    }

    public static readonly DependencyProperty DetectionRangeProperty =
        DependencyProperty.Register("DetectionRange", typeof(double), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(200.0, OnFOVParameterChanged));

    /// <summary>
    /// 감지 각도 (도)
    /// </summary>
    public double DetectionAngle
    {
        get { return (double)GetValue(DetectionAngleProperty); }
        set { SetValue(DetectionAngleProperty, value); }
    }

    public static readonly DependencyProperty DetectionAngleProperty =
        DependencyProperty.Register("DetectionAngle", typeof(double), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(120.0, OnFOVParameterChanged));

    /// <summary>
    /// 감지 방향 (도, 북쪽 기준)
    /// </summary>
    public double DetectionBearing
    {
        get { return (double)GetValue(DetectionBearingProperty); }
        set { SetValue(DetectionBearingProperty, value); }
    }

    public static readonly DependencyProperty DetectionBearingProperty =
        DependencyProperty.Register("DetectionBearing", typeof(double), typeof(GMapMarkerPidsControl),
            new PropertyMetadata(0.0, OnFOVParameterChanged));

    #endregion

    #region Constructors

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GMapMarkerPidsControl()
    {
    }

    /// <summary>
    /// PIDS 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="pidsMarker">연결할 PIDS 마커</param>
    public GMapMarkerPidsControl(GMapPidsMarker pidsMarker) : base(pidsMarker)
    {
        this.Loaded += GMapMarkerPidsControl_Loaded;
    }

    private void GMapMarkerPidsControl_Loaded(object sender, RoutedEventArgs e)
    {
        var mapControl = FindParentMapControl();
        if (mapControl != null)
        {
            mapControl.OnMapZoomChanged += OnMapZoomChanged;
        }
    }

    private void OnMapZoomChanged()
    {
        if (ShowFOV && DeviceType == EnumDeviceType.IpCamera)
        {
            UpdateFOVPath();
        }
    }

    #endregion

    #region Abstract Methods Implementation

    /// <summary>
    /// PIDS 마커 전용 UI 업데이트 구현
    /// </summary>
    protected override void UpdateFromSpecificMarker()
    {
        if (Marker == null) return;

        // PIDS 마커 전용 속성 동기화
        DeviceType = Marker.DeviceType;
        EventStatus = Marker.EventStatus;

        ShowFOV = Marker.ShowFOV;
        FOVOpacity = Marker.FOVOpacity;
        FOVColor = ColorHelper.ToBrush(Marker.FOVColor);
        DetectionRange = Marker.Model.DetectionRange;
        DetectionAngle = Marker.Model.DetectionAngle;
        DetectionBearing = Marker.Model.DetectionBearing;

        // FOV 업데이트
        if (ShowFOV)
        {
            UpdateFOVPath(); // 추가
        }

        // 장비 타입별 기본 설정
        ApplyDeviceTypeDefaults();
    }

    /// <summary>
    /// PIDS 마커 전용 바인딩 설정 구현
    /// </summary>
    protected override void SetupSpecificBindings()
    {
        if (Marker == null) return;

        // PIDS 마커 전용 바인딩
        SetupPropertyBinding(DeviceTypeProperty, nameof(Marker.DeviceType));
        SetupPropertyBinding(EventStatusProperty, nameof(Marker.EventStatus));
        SetupPropertyBinding(ShowFOVProperty, nameof(Marker.ShowFOV));
        SetupPropertyBinding(FOVOpacityProperty, nameof(Marker.FOVOpacity));
        SetupPropertyBinding(DetectionRangeProperty, nameof(Marker.DetectionRange));
        SetupPropertyBinding(DetectionAngleProperty, nameof(Marker.DetectionAngle));
        SetupPropertyBinding(DetectionBearingProperty, nameof(Marker.DetectionBearing));

        var colorConverter = new ColorTypeToBrushConverter();
        var visibilityConverter = new System.Windows.Controls.BooleanToVisibilityConverter();

        var fovColorBinding = new Binding(nameof(Marker.FOVColor))
        {
            Source = Marker,
            Mode = BindingMode.OneWay,
            Converter = colorConverter
        };
        SetBinding(FOVColorProperty, fovColorBinding);
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 컨트롤 초기화 완료 후 호출
    /// </summary>
    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();
        ApplyDeviceTypeDefaults();
        System.Diagnostics.Debug.WriteLine("GMapMarkerPidsControl 초기화 완료");
    }

    /// <summary>
    /// 마커 모양 업데이트 (장비 타입 고려)
    /// </summary>
    protected override void UpdateMarkerAppearance()
    {
        //// 이벤트 상태에 따른 색상 설정 (기본 상태 색상 무시)
        //MarkerFill = GetEventStatusBrush(EventStatus);

        //// 장비 타입별 테두리 색상
        //MarkerStroke = GetDeviceTypeBorderBrush(DeviceType);

        UpdateFOVPath();
    }

    /// <summary>
    /// 단일 클릭 처리 (PIDS 전용 로직)
    /// </summary>
    protected override void OnMarkerSingleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerSingleClicked(e);
        OnPidsMarkerClicked(e);
    }

    /// <summary>
    /// 더블클릭 처리 (FOV 토글 등)
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

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (Marker != null)
        {
            Marker.Width = ActualWidth;
            Marker.Height = ActualHeight;

            UpdateFOVPath();
        }
    }

    /// <summary>
    /// 컨트롤 템플릿 적용후
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 템플릿 요소들이 로드된 후 FOV 업데이트
        UpdateFOVPath();
    }
    #endregion

    #region PIDS Specific Methods
    /// <summary>
    /// 장비 타입별 기본 설정 적용
    /// </summary>
    private void ApplyDeviceTypeDefaults()
    {
        switch (DeviceType)
        {
            case EnumDeviceType.IpCamera:
                Width = Height = 40;
                break;
            case EnumDeviceType.PIR:
                Width = Height = 35;
                break;
            case EnumDeviceType.Fence:
                Width = Height = 30;
                break;
            default:
                Width = Height = 32;
                break;
        }
    }

    /// <summary>
    /// 이벤트 상태별 색상 브러시 반환
    /// </summary>
    private Brush GetEventStatusBrush(EnumEventStatus eventStatus)
    {
        return eventStatus switch
        {
            EnumEventStatus.Normal => Brushes.Blue,
            EnumEventStatus.Detecting => Brushes.Red,
            EnumEventStatus.Fault => Brushes.Orange,
            EnumEventStatus.Connection => Brushes.Blue,
            _ => Brushes.Gray
        };
    }

    /// <summary>
    /// 장비 타입별 테두리 색상 브러시 반환
    /// </summary>
    private Brush GetDeviceTypeBorderBrush(EnumDeviceType deviceType)
    {
        return deviceType switch
        {
            EnumDeviceType.IpCamera => Brushes.Yellow,
            EnumDeviceType.PIR => Brushes.Orange,
            EnumDeviceType.Fence => Brushes.White,
            _ => Brushes.White
        };
    }

    /// <summary>
    /// PIDS 마커 클릭 처리
    /// </summary>
    protected virtual void OnPidsMarkerClicked(MouseButtonEventArgs e)
    {
        // 장비 타입별 특별한 처리
        switch (DeviceType)
        {
            case EnumDeviceType.IpCamera:
                // 카메라 클릭 시 FOV 표시
                if (!ShowFOV && Marker != null)
                {
                    Marker.ToggleFOVDisplay();
                }
                break;

            case EnumDeviceType.PIR:
                // PIR 센서 클릭 시 감지 범위 표시
                // TODO: 감지 범위 시각화
                break;
        }
    }

    ///// <summary>
    ///// FOV Path 업데이트 (핵심 메서드)
    ///// </summary>
    //private void UpdateFOVPath()
    //{
    //    if (!Dispatcher.CheckAccess())
    //    {
    //        Dispatcher.BeginInvoke(() => UpdateFOVPath());
    //        return;
    //    }

    //    try
    //    {
    //        if (GetTemplateChild("PART_FOVFigure") is PathFigure figure &&
    //            GetTemplateChild("PART_FOVLine1") is LineSegment line1 &&
    //            GetTemplateChild("PART_FOVArc") is ArcSegment arc &&
    //            GetTemplateChild("PART_FOVCenter") is Ellipse centerEllipse)
    //        {
    //            // 카메라 아이콘의 실제 크기 기준으로 중심점 계산
    //            var centerX = ActualWidth;        // 카메라 렌즈는 오른쪽 끝
    //            var centerY = ActualHeight / 2.0; // 카메라 중심은 세로 중앙

    //            // 각도를 라디안으로 변환 (0도 = 동쪽, 시계방향 양수)
    //            var bearingRad = DetectionBearing * Math.PI / 180.0;
    //            var halfAngleRad = DetectionAngle / 2.0 * Math.PI / 180.0;

    //            // 시작각과 끝각 계산
    //            var startAngleRad = bearingRad - halfAngleRad;
    //            var endAngleRad = bearingRad + halfAngleRad;

    //            // 반지름 (픽셀 단위로 스케일)
    //            var radius = Math.Max(DetectionRange * 0.2, 20);

    //            // 첫 번째 경계점 (시작각)
    //            var startX = centerX + radius * Math.Cos(startAngleRad);
    //            var startY = centerY + radius * Math.Sin(startAngleRad);

    //            // 두 번째 경계점 (끝각)
    //            var endX = centerX + radius * Math.Cos(endAngleRad);
    //            var endY = centerY + radius * Math.Sin(endAngleRad);

    //            // PathFigure 업데이트 (중심점에서 시작)
    //            figure.StartPoint = new Point(centerX, centerY);

    //            // 첫 번째 LineSegment: 중심 → 첫 번째 경계점
    //            line1.Point = new Point(startX, startY);

    //            // ArcSegment: 첫 번째 경계점 → 두 번째 경계점 (호로 연결)
    //            arc.Point = new Point(endX, endY);
    //            arc.Size = new Size(radius, radius);
    //            arc.IsLargeArc = DetectionAngle > 180;
    //            arc.SweepDirection = SweepDirection.Clockwise;

    //            // 마지막 LineSegment는 XAML에 하드코딩되어 있어서 자동으로 중심점으로 돌아감

    //            // 중심점 Ellipse 위치 업데이트
    //            Canvas.SetLeft(centerEllipse, centerX - 2);
    //            Canvas.SetTop(centerEllipse, centerY - 2);

    //            System.Diagnostics.Debug.WriteLine($"FOV 업데이트:");
    //            System.Diagnostics.Debug.WriteLine($"  중심점: ({centerX:F1}, {centerY:F1})");
    //            System.Diagnostics.Debug.WriteLine($"  시작각: {DetectionBearing - DetectionAngle / 2:F1}° → ({startX:F1}, {startY:F1})");
    //            System.Diagnostics.Debug.WriteLine($"  끝각: {DetectionBearing + DetectionAngle / 2:F1}° → ({endX:F1}, {endY:F1})");
    //            System.Diagnostics.Debug.WriteLine($"  반지름: {radius:F1}px");
    //        }
    //        else
    //        {
    //            System.Diagnostics.Debug.WriteLine("FOV 템플릿 요소를 찾을 수 없음");
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"FOV Path 업데이트 실패: {ex.Message}");
    //    }
    //}

    //private void DebugFOVProperties()
    //{
    //    System.Diagnostics.Debug.WriteLine($"=== FOV Debug Info ===");
    //    System.Diagnostics.Debug.WriteLine($"ShowFOV: {ShowFOV}");
    //    System.Diagnostics.Debug.WriteLine($"FOVColor: {FOVColor}");
    //    System.Diagnostics.Debug.WriteLine($"FOVOpacity: {FOVOpacity}");
    //    System.Diagnostics.Debug.WriteLine($"DetectionRange: {DetectionRange}");
    //    System.Diagnostics.Debug.WriteLine($"DetectionAngle: {DetectionAngle}");
    //    System.Diagnostics.Debug.WriteLine($"DetectionBearing: {DetectionBearing}");
    //}

    private void UpdateFOVPath()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateFOVPath());
            return;
        }

        try
        {
            if (GetTemplateChild("PART_FOVFigure") is PathFigure figure &&
                GetTemplateChild("PART_FOVLine1") is LineSegment line1 &&
                GetTemplateChild("PART_FOVArc") is ArcSegment arc &&
                GetTemplateChild("PART_FOVCenter") is Ellipse centerEllipse)
            {
                // 카메라 아이콘의 실제 크기 기준으로 중심점 계산
                var centerX = ActualWidth;
                var centerY = ActualHeight / 2.0;

                // GMap 컨트롤 찾기
                var mapControl = FindParentMapControl();
                if (mapControl == null || Marker?.Position == null)
                {
                    System.Diagnostics.Debug.WriteLine("GMap 컨트롤 또는 마커 위치를 찾을 수 없음");
                    return;
                }

                // 실제 거리를 픽셀로 변환
                var radiusInPixels = ConvertMetersToPixels(
                    DetectionRange,
                    Marker.Position,
                    mapControl);

                // 거리 제한 (10m ~ 5km)
                radiusInPixels = ConvertMetersToPixels(DetectionRange, Marker.Position, mapControl);

                // 최소/최대 픽셀 크기 제한
                //radiusInPixels = Math.Max(20, Math.Min(500, radiusInPixels));

                // 각도를 라디안으로 변환
                var bearingRad = DetectionBearing * Math.PI / 180.0;
                var halfAngleRad = DetectionAngle / 2.0 * Math.PI / 180.0;

                // 시작각과 끝각 계산
                var startAngleRad = bearingRad - halfAngleRad;
                var endAngleRad = bearingRad + halfAngleRad;

                // 첫 번째 경계점 (시작각)
                var startX = centerX + radiusInPixels * Math.Cos(startAngleRad);
                var startY = centerY + radiusInPixels * Math.Sin(startAngleRad);

                // 두 번째 경계점 (끝각)
                var endX = centerX + radiusInPixels * Math.Cos(endAngleRad);
                var endY = centerY + radiusInPixels * Math.Sin(endAngleRad);

                // PathFigure 업데이트
                figure.StartPoint = new Point(centerX, centerY);
                line1.Point = new Point(startX, startY);
                arc.Point = new Point(endX, endY);
                arc.Size = new Size(radiusInPixels, radiusInPixels);
                arc.IsLargeArc = DetectionAngle > 180;
                arc.SweepDirection = SweepDirection.Clockwise;

                // 중심점 Ellipse 위치 업데이트
                Canvas.SetLeft(centerEllipse, centerX - 2);
                Canvas.SetTop(centerEllipse, centerY - 2);

                System.Diagnostics.Debug.WriteLine($"FOV 업데이트:");
                System.Diagnostics.Debug.WriteLine($"  실제 거리: {DetectionRange:F0}m → 픽셀: {radiusInPixels:F1}px");
                System.Diagnostics.Debug.WriteLine($"  중심점: ({centerX:F1}, {centerY:F1})");
                System.Diagnostics.Debug.WriteLine($"  줌 레벨: {mapControl.Zoom}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FOV Path 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 미터 단위 거리를 현재 줌 레벨에서의 픽셀로 변환
    /// </summary>
    private double ConvertMetersToPixels(double meters, PointLatLng position, GMapCustomControl mapControl)
    {
        try
        {
            // 현재 위치에서 동쪽으로 meters만큼 떨어진 지점 계산
            var earthRadius = 6371000; // 지구 반지름 (미터)
            var lat1Rad = position.Lat * Math.PI / 180.0;
            var deltaLon = meters / (earthRadius * Math.Cos(lat1Rad)) * 180.0 / Math.PI;

            var targetPoint = new PointLatLng(position.Lat, position.Lng + deltaLon);

            // 두 지점을 화면 좌표로 변환
            var centerPixel = mapControl.FromLatLngToLocal(position);
            var targetPixel = mapControl.FromLatLngToLocal(targetPoint);

            // 픽셀 거리 계산
            var pixelDistance = Math.Sqrt(
                Math.Pow(targetPixel.X - centerPixel.X, 2) +
                Math.Pow(targetPixel.Y - centerPixel.Y, 2));

            return pixelDistance;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"거리 변환 실패: {ex.Message}");
            // 폴백: 간단한 스케일 사용
            return meters * 0.1; // 대략적인 스케일
        }
    }
   
    #endregion

    #region Static Property Changed Callbacks

    /// <summary>
    /// 장비 타입 변경 시 호출
    /// </summary>
    protected static void OnDeviceTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            control.ApplyDeviceTypeDefaults();
            control.UpdateMarkerAppearance();

            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.DeviceType != (EnumDeviceType)e.NewValue)
            {
                control.Marker.DeviceType = (EnumDeviceType)e.NewValue;
            }
        }
    }

    /// <summary>
    /// 이벤트 상태 변경 시 호출 (애니메이션 트리거 핵심)
    /// </summary>
    protected static void OnEventStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            // UI 색상 즉시 업데이트
            control.UpdateMarkerAppearance();

            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.EventStatus != (EnumEventStatus)e.NewValue)
            {
                control.Marker.EventStatus = (EnumEventStatus)e.NewValue;
            }

            System.Diagnostics.Debug.WriteLine($"EventStatus 변경: {e.OldValue} → {e.NewValue}");
        }
    }

    /// <summary>
    /// FOV 표시 여부 변경 시 호출
    /// </summary>
    protected static void OnShowFOVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            bool isVisible = (bool)e.NewValue;
            
            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.ShowFOV != isVisible)
            {
                control.Marker.ShowFOV = isVisible;
            }

            if (isVisible)
            {
                control.UpdateFOVPath(); // 추가
            }

        }
    }

    /// <summary>
    /// Opacity 변경 시 
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    /// <exception cref="NotImplementedException"></exception>
    protected static void OnFOVOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null)
        {
            System.Diagnostics.Debug.WriteLine($"OnFOVOpacityChanged: {e.OldValue} → {e.NewValue}");
            control.Marker.FOVOpacity = (double)e.NewValue;
        }
    }

    /// <summary>
    /// FOV 매개변수 변경 시 호출
    /// </summary>
    protected static void OnFOVParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.Marker != null && control.ShowFOV)
        {
            control.UpdateFOVPath(); // 추가
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// FOV 매개변수 업데이트
    /// </summary>
    public void UpdateFOVParameters(double range, double angle, double bearing)
    {
        DetectionRange = range;
        DetectionAngle = angle;
        DetectionBearing = bearing;

        if (ShowFOV)
        {
            UpdateFOVPath(); // 추가
        }
    }

    /// <summary>
    /// 장비 상태 초기화
    /// </summary>
    public void ResetDeviceState()
    {
        EventStatus = EnumEventStatus.Normal;
        ShowFOV = false;
        ApplyDeviceTypeDefaults();
    }

    #endregion

}

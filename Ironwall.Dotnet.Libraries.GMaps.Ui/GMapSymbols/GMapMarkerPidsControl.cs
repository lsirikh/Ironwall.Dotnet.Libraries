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
        InitializeFOVSystem();
    }

    /// <summary>
    /// PIDS 마커와 함께 생성하는 생성자
    /// </summary>
    /// <param name="pidsMarker">연결할 PIDS 마커</param>
    public GMapMarkerPidsControl(GMapPidsMarker pidsMarker) : base(pidsMarker)
    {
        InitializeFOVSystem();
    }

    #endregion

    #region FOV System
    private List<PointLatLng>? _fovPoints;
    private readonly object _fovLock = new();

    /// <summary>
    /// FOV 시스템 초기화
    /// </summary>
    private void InitializeFOVSystem()
    {
        _fovPoints = new List<PointLatLng>();
    }

    /// <summary>
    /// FOV 점들 계산
    /// </summary>
    private void CalculateFOVPoints()
    {
        lock (_fovLock)
        {
            try
            {
                if (Marker == null) return;

                //_fovPoints = PidsFOVHelper.CalculateFOVPoints(
                //    Marker.Position,
                //    DetectionRange,
                //    DetectionAngle,
                //    DetectionBearing + RotationAngle, // 마커 회전 + FOV 방향
                //    GetFOVPointCount(DeviceType)
                //);

                // TODO: GMapCustomControl에 FOV 업데이트 신호 전달
                NotifyFOVChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FOV 계산 실패: {ex.Message}");
            }
        }
    }

    
    /// <summary>
    /// FOV 변경 알림 (부모 컨트롤에 전달)
    /// </summary>
    private void NotifyFOVChanged()
    {
        // TODO: 부모 GMapCustomControl에 FOV 변경 이벤트 전달
        System.Diagnostics.Debug.WriteLine($"FOV 변경됨: 점 개수={_fovPoints.Count}");
    }

    /// <summary>
    /// 현재 FOV 점들 반환 (읽기 전용)
    /// </summary>
    public IReadOnlyList<PointLatLng>? GetFOVPoints()
    {
        lock (_fovLock)
        {
            return _fovPoints?.AsReadOnly();
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
        DetectionRange = Marker.Model.DetectionRange;
        DetectionAngle = Marker.Model.DetectionAngle;
        DetectionBearing = Marker.Model.DetectionBearing;

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
        SetupPropertyBinding(DetectionRangeProperty, nameof(Marker.DetectionRange));
        SetupPropertyBinding(DetectionAngleProperty, nameof(Marker.DetectionAngle));
        SetupPropertyBinding(DetectionBearingProperty, nameof(Marker.DetectionBearing));
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
        // 이벤트 상태에 따른 색상 설정 (기본 상태 색상 무시)
        MarkerFill = GetEventStatusBrush(EventStatus);

        // 장비 타입별 테두리 색상
        MarkerStroke = GetDeviceTypeBorderBrush(DeviceType);
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
        // PIDS 전용 클릭 효과
        TriggerClickAnimation();

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

    /// <summary>
    /// 장비 클릭 애니메이션
    /// </summary>
    private void TriggerClickAnimation()
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
                transformGroup.Children.Add(existingRotate); // ✅ 기존 회전 보존
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
    #endregion

    #region Static Property Changed Callbacks

    /// <summary>
    /// 장비 타입 변경 시 호출
    /// </summary>
    protected static void OnDeviceTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control)
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
        if (d is GMapMarkerPidsControl control)
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
        if (d is GMapMarkerPidsControl control)
        {
            bool isVisible = (bool)e.NewValue;

            if (isVisible)
            {
                control.CalculateFOVPoints();
            }
            else
            {
                // FOV 숨김
                lock (control._fovLock)
                {
                    control._fovPoints?.Clear();
                    control.NotifyFOVChanged();
                }
            }

            // 마커 데이터와 동기화
            if (control.Marker != null && control.Marker.ShowFOV != isVisible)
            {
                control.Marker.ShowFOV = isVisible;
            }
        }
    }

    /// <summary>
    /// FOV 매개변수 변경 시 호출
    /// </summary>
    protected static void OnFOVParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapMarkerPidsControl control && control.ShowFOV)
        {
            control.CalculateFOVPoints();
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
            CalculateFOVPoints();
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

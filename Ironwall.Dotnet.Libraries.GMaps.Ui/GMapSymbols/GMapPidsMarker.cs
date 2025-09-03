using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Windows;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
  Purpose      : PIDS 장비 전용 마커 (최적화된 버전)
  Created By   : GHLee                                                
  Created On   : 8/26/2025                                                    
  Department   : SW Team                                                   
  Company      : Sensorway Co., Ltd.                                       
  Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapPidsMarker : GMapBaseMarker<IPidsSymbolModel>, IPidsEditableMarker
{
    #region - Ctors -
    public GMapPidsMarker(ILogService log, IPidsSymbolModel pidsModel)
        : base(log, pidsModel)
    {
        pidsModel.Update += PidsModel_Update;
    }
    #endregion

    #region - Animation System -
    private DispatcherTimer? _animationTimer;
    private EnumColorType _originalColor = EnumColorType.Blue;
    private bool _isAnimating = false;


    private void PidsModel_Update(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EventStatus));
        OnPropertyChanged(nameof(OperationState));
    }

    /// <summary>
    /// 모델 속성 변경 감지 (EventStatus 변경 시 자동 애니메이션)
    /// </summary>
    private void OnModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPidsSymbolModel.EventStatus))
        {
            UpdateAnimationFromEventStatus();
        }
    }

    /// <summary>
    /// EventStatus에 따른 애니메이션 자동 업데이트
    /// </summary>
    private void UpdateAnimationFromEventStatus()
    {
        try
        {
            switch (_model.EventStatus)
            {
                case EnumEventStatus.Normal:
                    StopAnimation();
                    SetStaticColor(_originalColor);
                    break;

                case EnumEventStatus.Detecting:
                    StartBlinkAnimation(EnumColorType.Green, TimeSpan.FromMilliseconds(500));
                    break;

                case EnumEventStatus.Fault:
                    StartBlinkAnimation(EnumColorType.Red, TimeSpan.FromMilliseconds(200));
                    break;

                case EnumEventStatus.Connection:
                    SetStaticColor(EnumColorType.Blue);
                    break;

                default:
                    SetStaticColor(EnumColorType.Gray);
                    break;
            }

            _log?.Info($"애니메이션 상태 변경: {_model.Title} → {_model.EventStatus}");
        }
        catch (Exception ex)
        {
            _log?.Error($"애니메이션 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 깜빡임 애니메이션 시작
    /// </summary>
    private void StartBlinkAnimation(EnumColorType targetColor, TimeSpan interval)
    {
        StopAnimation();

        _isAnimating = true;
        bool isHighlighted = false;

        _animationTimer = new DispatcherTimer
        {
            Interval = interval
        };

        _animationTimer.Tick += (s, e) =>
        {
            try
            {
                FillColor = isHighlighted ? _originalColor : targetColor;
                isHighlighted = !isHighlighted;
            }
            catch (Exception ex)
            {
                _log?.Error($"애니메이션 틱 오류: {ex.Message}");
            }
        };

        _animationTimer.Start();
    }

    /// <summary>
    /// 정적 색상 설정
    /// </summary>
    private void SetStaticColor(EnumColorType color)
    {
        StopAnimation();
        FillColor = color;
    }

    /// <summary>
    /// 애니메이션 중지
    /// </summary>
    private void StopAnimation()
    {
        if (_animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer = null;
        }
        _isAnimating = false;
    }

    #endregion

    #region - Overrides -
    /// <summary>
    /// PIDS 마커 전용 컨트롤 생성
    /// </summary>
    protected override UIElement CreateMarkerControl()
    {
        var markerControl = new GMapMarkerPidsControl(this);
        _log?.Info($"GMapMarkerPidsControl 생성: {_model.Title}");
        return markerControl;
    }

    protected override void ConfigureMarkerControl(UIElement marker)
    {
        base.ConfigureMarkerControl(marker);
    }

    /// <summary>
    /// 상태 변경 시 EventStatus 동기화
    /// </summary>
    protected override void OnStatusChanged(EnumOperationState status)
    {
        base.OnStatusChanged(status);

        // OperationState → EventStatus 매핑
        _model.EventStatus = status switch
        {
            EnumOperationState.ACTIVE => EnumEventStatus.Normal,
            EnumOperationState.DEACTIVE => EnumEventStatus.Fault,
            EnumOperationState.FAULT => EnumEventStatus.Fault,
            _ => EnumEventStatus.Normal
        };
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAnimation();
        }
        base.Dispose(disposing);
    }
    #endregion

    #region - FOV Management -
    /// <summary>
    /// FOV 설정 업데이트
    /// </summary>
    public void UpdateFOVSettings(double range, double angle, double bearing)
    {
        try
        {
            _model.DetectionRange = range;
            _model.DetectionAngle = angle;
            _model.DetectionBearing = bearing;

            _log?.Info($"FOV 업데이트: 범위={range}m, 각도={angle}°, 방향={bearing}°");
        }
        catch (Exception ex)
        {
            _log?.Error($"FOV 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// FOV 표시 토글
    /// </summary>
    public void ToggleFOVDisplay()
    {
        _model.ShowFOV = !_model.ShowFOV;
        _log?.Info($"FOV 표시 토글: {_model.ShowFOV}");
    }
    #endregion

    #region - Properties -
    /// <summary>
    /// 연결된 장치 ID
    /// </summary>
    public int LinkedDeviceId
    {
        get => _model.LinkedDeviceId;
        set
        {
            _model.LinkedDeviceId = value;
            OnPropertyChanged(nameof(LinkedDeviceId));
        }
    }

    /// <summary>
    /// 장치 타입
    /// </summary>
    public EnumDeviceType DeviceType
    {
        get => _model.DeviceType;
        set
        {
            _model.DeviceType = value;
            OnPropertyChanged(nameof(DeviceType));
        }
    }

    public double DetectionRange
    {
        get => _model.DetectionRange;
        set
        {
            _model.DetectionRange = value;
            OnPropertyChanged(nameof(DetectionRange));
        }
    }

    public double DetectionAngle
    {
        get => _model.DetectionAngle;
        set
        {
            _model.DetectionAngle = value;
            OnPropertyChanged(nameof(DetectionAngle));
        }
    }

    public double DetectionBearing
    {
        get => _model.DetectionBearing;
        set
        {
            _model.DetectionBearing = value;
            OnPropertyChanged(nameof(DetectionBearing));
        }
    }

    public bool ShowFOV
    {
        get => _model.ShowFOV;
        set
        {
            _model.ShowFOV = value;
            OnPropertyChanged(nameof(ShowFOV));
        }
    }

    public EnumColorType FOVColor
    {
        get => _model.FOVColor;
        set
        {
            _model.FOVColor = value;
            OnPropertyChanged(nameof(FOVColor));
        }
    }

    public double FOVOpacity
    {
        get => _model.FOVOpacity;
        set
        {
            _model.FOVOpacity = value;
            OnPropertyChanged(nameof(FOVOpacity));
        }
    }

    /// <summary>
    /// 이벤트 상태 (애니메이션 트리거)
    /// </summary>
    public EnumEventStatus EventStatus
    {
        get => _model.EventStatus;
        set
        {
            _model.EventStatus = value;
            OnPropertyChanged(nameof(EventStatus));
            // PropertyChanged 이벤트로 자동 애니메이션 처리됨
        }
    }

    /// <summary>
    /// 애니메이션 실행 중 여부
    /// </summary>
    public bool IsAnimating => _isAnimating;

    EnumDeviceType IPidsEditableMarker.DeviceType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    #endregion
}
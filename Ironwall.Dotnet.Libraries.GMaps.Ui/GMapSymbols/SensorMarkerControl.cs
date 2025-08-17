using System;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025 9:32:15 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 센서 데이터를 표시하는 커스텀 마커
/// </summary>
public class SensorMarkerControl : GMapMarkerBasicCustomControl
{
    #region Dependency Properties

    /// <summary>
    /// 센서 값
    /// </summary>
    public double SensorValue
    {
        get { return (double)GetValue(SensorValueProperty); }
        set { SetValue(SensorValueProperty, value); }
    }

    public static readonly DependencyProperty SensorValueProperty =
        DependencyProperty.Register("SensorValue", typeof(double), typeof(SensorMarkerControl),
            new PropertyMetadata(0.0, OnSensorValueChanged));

    /// <summary>
    /// 임계값
    /// </summary>
    public double ThresholdValue
    {
        get { return (double)GetValue(ThresholdValueProperty); }
        set { SetValue(ThresholdValueProperty, value); }
    }

    public static readonly DependencyProperty ThresholdValueProperty =
        DependencyProperty.Register("ThresholdValue", typeof(double), typeof(SensorMarkerControl),
            new PropertyMetadata(100.0, OnThresholdValueChanged));

    /// <summary>
    /// 센서 타입
    /// </summary>
    public string SensorType
    {
        get { return (string)GetValue(SensorTypeProperty); }
        set { SetValue(SensorTypeProperty, value); }
    }

    public static readonly DependencyProperty SensorTypeProperty =
        DependencyProperty.Register("SensorType", typeof(string), typeof(SensorMarkerControl),
            new PropertyMetadata("Unknown"));

    #endregion

    #region Events

    /// <summary>
    /// 임계값 초과 이벤트
    /// </summary>
    public event EventHandler<ThresholdExceededEventArgs> ThresholdExceeded;

    #endregion

    #region Fields

    private DispatcherTimer _blinkTimer;
    private bool _isBlinking;

    #endregion

    #region Constructors

    public SensorMarkerControl() : base()
    {
        InitializeSensorMarker();
    }

    public SensorMarkerControl(GMapCustomMarker marker) : base(marker)
    {
        InitializeSensorMarker();
    }

    #endregion

    #region Protected Override Methods

    protected override void OnControlInitialized()
    {
        base.OnControlInitialized();

        // 센서 마커 전용 설정
        SensorType = "Temperature";
        ThresholdValue = 80.0;
    }

    protected override void UpdateMarkerAppearance()
    {
        // 센서 값에 따른 색상 변경
        if (SensorValue > ThresholdValue)
        {
            MarkerFill = Brushes.Red;
            StartBlinking();
        }
        else if (SensorValue > ThresholdValue * 0.8)
        {
            MarkerFill = Brushes.Orange;
            StopBlinking();
        }
        else
        {
            MarkerFill = Brushes.Green;
            StopBlinking();
        }
    }

    protected override void HandleSingleClick(MouseButtonEventArgs e)
    {
        base.HandleSingleClick(e);

        // 센서 상세 정보 표시
        ShowSensorDetails();
    }

    protected override void OnMarkerDoubleClicked(MouseButtonEventArgs e)
    {
        base.OnMarkerDoubleClicked(e);

        // 센서 설정 다이얼로그 열기
        OpenSensorSettings();
    }

    #endregion

    #region Private Methods

    private void InitializeSensorMarker()
    {
        // 깜빡임 타이머 초기화
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += BlinkTimer_Tick;
    }

    private void StartBlinking()
    {
        if (!_isBlinking)
        {
            _isBlinking = true;
            _blinkTimer.Start();
        }
    }

    private void StopBlinking()
    {
        if (_isBlinking)
        {
            _isBlinking = false;
            _blinkTimer.Stop();
            Opacity = 1.0; // 원래 투명도로 복원
        }
    }

    private void BlinkTimer_Tick(object sender, EventArgs e)
    {
        // 깜빡임 효과
        Opacity = Opacity == 1.0 ? 0.3 : 1.0;
    }

    private void ShowSensorDetails()
    {
        // TODO: 센서 상세 정보 팝업 표시
        // 센서 값, 임계값, 마지막 업데이트 시간 등
    }

    private void OpenSensorSettings()
    {
        // TODO: 센서 설정 다이얼로그 열기
        // 임계값 설정, 알람 설정 등
    }

    #endregion

    #region Static Property Changed Callbacks

    private static void OnSensorValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SensorMarkerControl control)
        {
            var oldValue = (double)e.OldValue;
            var newValue = (double)e.NewValue;

            control.UpdateMarkerAppearance();

            // 임계값 초과 체크
            if (newValue > control.ThresholdValue && oldValue <= control.ThresholdValue)
            {
                control.ThresholdExceeded?.Invoke(control, new ThresholdExceededEventArgs(newValue, control.ThresholdValue));
            }
        }
    }

    private static void OnThresholdValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SensorMarkerControl control)
        {
            control.UpdateMarkerAppearance();
        }
    }

    #endregion
}

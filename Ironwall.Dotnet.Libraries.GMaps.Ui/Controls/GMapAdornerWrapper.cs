using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 2:22:20 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// GMap 객체를 기존 AdornerDecorator 시스템과 연결하는 추상 래퍼 클래스
/// FrameworkElement의 기본 이벤트들을 최대한 활용
/// </summary>
public abstract class GMapAdornerWrapper : ContentControl
{
    protected ILogService? _log;

    #region Constructor
    protected GMapAdornerWrapper()
    {
        _log = IoC.Get<ILogService>();

        // 기존 DesignerItemDecorator와 동일한 기본 설정
        MinWidth = 50;
        MinHeight = 50;
        RenderTransformOrigin = new Point(0.5, 0.5);
        SnapsToDevicePixels = true;
        Background = System.Windows.Media.Brushes.Transparent;

        // FrameworkElement 기본 이벤트 구독
        InitializeFrameworkEvents();
    }
    #endregion

    #region Framework Events Initialization

    /// <summary>
    /// FrameworkElement의 기본 이벤트들 초기화
    /// </summary>
    private void InitializeFrameworkEvents()
    {
        // 크기 변경 이벤트 (FrameworkElement 기본 제공)
        SizeChanged += OnSizeChangedInternal;

        // 위치 변경 감지를 위한 레이아웃 업데이트 이벤트
        LayoutUpdated += OnLayoutUpdatedInternal;

        // 언로드 이벤트
        Unloaded += OnWrapperUnloaded;

        // 로드 완료 이벤트
        Loaded += OnWrapperLoaded;
    }

    #endregion

    #region Common Dependency Properties

    /// <summary>
    /// GMapCustomControl 참조
    /// </summary>
    public GMapCustomControl MapControl
    {
        get { return (GMapCustomControl)GetValue(MapControlProperty); }
        set { SetValue(MapControlProperty, value); }
    }

    public static readonly DependencyProperty MapControlProperty =
        DependencyProperty.Register("MapControl", typeof(GMapCustomControl),
            typeof(GMapAdornerWrapper), new PropertyMetadata(null, OnMapControlChanged));

    /// <summary>
    /// Adorner 표시 여부 (기존 DesignerItemDecorator와 연동)
    /// </summary>
    public bool IsSelected
    {
        get { return (bool)GetValue(IsSelectedProperty); }
        set { SetValue(IsSelectedProperty, value); }
    }

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register("IsSelected", typeof(bool),
            typeof(GMapAdornerWrapper), new PropertyMetadata(false));

    #endregion

    #region Property Change Handlers

    private static void OnMapControlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapAdornerWrapper wrapper)
        {
            // 이전 맵에서 이벤트 해제
            if (e.OldValue is GMapCustomControl oldMap)
            {
                oldMap.OnMapZoomChanged -= wrapper.OnMapZoomChanged;
                oldMap.OnAreaChange -= wrapper.OnMapAreaChanged;
            }

            // 새 맵에 이벤트 등록
            if (e.NewValue is GMapCustomControl newMap)
            {
                newMap.OnMapZoomChanged += wrapper.OnMapZoomChanged;
                newMap.OnAreaChange += wrapper.OnMapAreaChanged;

                // 초기 위치 설정
                wrapper.UpdatePosition();
            }
        }
    }

    #endregion

    #region Framework Event Handlers

    /// <summary>
    /// FrameworkElement.SizeChanged 이벤트 핸들러
    /// WPF에서 크기가 실제로 변경되었을 때 자동 호출
    /// </summary>
    private void OnSizeChangedInternal(object sender, SizeChangedEventArgs e)
    {
        try
        {
            _log?.Info($"크기 변경 감지: {e.PreviousSize} → {e.NewSize}");

            // 지리적 좌표 동기화
            SyncToGeoCoordinates();

            // 사용자 정의 크기 변경 처리
            OnSizeChanged(e);
        }
        catch (Exception ex)
        {
            _log?.Error($"크기 변경 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 레이아웃 업데이트 이벤트 핸들러 (위치 변경 감지용)
    /// </summary>
    private void OnLayoutUpdatedInternal(object? sender, EventArgs e)
    {
        try
        {
            // 위치 변경 감지 로직
            var currentLeft = Canvas.GetLeft(this);
            var currentTop = Canvas.GetTop(this);

            if (!double.IsNaN(currentLeft) && !double.IsNaN(currentTop))
            {
                var currentPosition = new Point(currentLeft, currentTop);

                if (_previousPosition != currentPosition)
                {
                    _log?.Info($"위치 변경 감지: {_previousPosition} → {currentPosition}");

                    _previousPosition = currentPosition;

                    // 지리적 좌표 동기화
                    SyncToGeoCoordinates();

                    // 사용자 정의 위치 변경 처리
                    OnPositionChanged();
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"위치 변경 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 로드 완료 이벤트 핸들러
    /// </summary>
    private void OnWrapperLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _log?.Info($"Adorner 래퍼 로드 완료: {GetDebugInfo()}");

            // 초기 위치 설정
            UpdatePosition();

            // 사용자 정의 로드 처리
            OnLoaded();
        }
        catch (Exception ex)
        {
            _log?.Error($"로드 처리 실패: {ex.Message}");
        }
    }

    #endregion

    #region Abstract Methods (구체 클래스에서 구현)

    /// <summary>
    /// 지리적 좌표를 화면 좌표로 변환하여 위치 업데이트
    /// </summary>
    public abstract void UpdatePosition();

    /// <summary>
    /// 현재 화면 위치를 지리적 좌표로 변환하여 업데이트
    /// </summary>
    public abstract void SyncToGeoCoordinates();

    /// <summary>
    /// 디버그 정보 문자열 (ToString용)
    /// </summary>
    protected abstract string GetDebugInfo();

    #endregion

    #region Virtual Methods (구체 클래스에서 선택적 오버라이드)

    /// <summary>
    /// 크기 변경 시 호출 (구체 클래스에서 추가 처리 가능)
    /// </summary>
    protected virtual void OnSizeChanged(SizeChangedEventArgs e)
    {
        // 기본 구현 없음 - 필요시 구체 클래스에서 오버라이드
    }

    /// <summary>
    /// 위치 변경 시 호출 (구체 클래스에서 추가 처리 가능)
    /// </summary>
    protected virtual void OnPositionChanged()
    {
        // 기본 구현 없음 - 필요시 구체 클래스에서 오버라이드
    }

    /// <summary>
    /// 로드 완료 시 호출 (구체 클래스에서 추가 처리 가능)
    /// </summary>
    protected virtual void OnLoaded()
    {
        // 기본 구현 없음 - 필요시 구체 클래스에서 오버라이드
    }

    /// <summary>
    /// 회전 변경 처리 (구체 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnRotationChanged(double angle)
    {
        // 기본 구현 없음 - 구체 클래스에서 필요시 오버라이드
    }

    #endregion

    #region Map Event Handlers

    /// <summary>
    /// 맵 줌 변경 시 위치 업데이트
    /// </summary>
    protected virtual void OnMapZoomChanged()
    {
        UpdatePosition();
    }

    /// <summary>
    /// 맵 영역 변경 시 위치 업데이트
    /// </summary>
    protected virtual void OnMapAreaChanged(RectLatLng selection, double zoom, bool zoomToFit)
    {
        UpdatePosition();
    }

    #endregion

    #region Adorner Integration Methods (기존 AdornerDecorator와 연동)

    /// <summary>
    /// Adorner에서 크기 변경 시 호출 (기존 ResizeThumb에서 호출)
    /// 이제 FrameworkElement.SizeChanged가 자동으로 처리하므로 단순화됨
    /// </summary>
    public void OnAdornerSizeChanged()
    {
        // FrameworkElement.SizeChanged 이벤트가 자동으로 처리하므로
        // 여기서는 추가 작업만 수행
        _log?.Info("Adorner 크기 변경 완료");
    }

    /// <summary>
    /// Adorner에서 위치 변경 시 호출 (기존 MoveThumb에서 호출)
    /// 이제 LayoutUpdated가 자동으로 처리하므로 단순화됨
    /// </summary>
    public void OnAdornerPositionChanged()
    {
        // LayoutUpdated 이벤트가 자동으로 처리하므로
        // 여기서는 추가 작업만 수행
        _log?.Info("Adorner 위치 변경 완료");
    }

    /// <summary>
    /// Adorner에서 회전 변경 시 호출 (기존 RotateThumb에서 호출)
    /// </summary>
    public void OnAdornerRotationChanged()
    {
        if (RenderTransform is RotateTransform rotateTransform)
        {
            OnRotationChanged(rotateTransform.Angle);
        }
        _log?.Info("Adorner 회전 변경 완료");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 표시 스케일 계산 (줌 레벨 및 객체 크기 고려)
    /// </summary>
    protected double CalculateDisplayScale()
    {
        if (MapControl == null) return 0.3;

        // 줌 레벨에 따른 기본 스케일
        var baseScale = Math.Pow(2, MapControl.Zoom - 15) * 0.25;

        // 크기에 따른 조정
        var size = Math.Max(ActualWidth, ActualHeight);  // ActualWidth/Height 사용
        if (size > 2000)
            baseScale *= 0.3;
        else if (size > 1000)
            baseScale *= 0.5;
        else if (size < 200)
            baseScale *= 2.0;

        return Math.Max(0.1, Math.Min(1.5, baseScale));
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 리소스 정리 (Unloaded 이벤트 핸들러)
    /// </summary>
    protected virtual void OnWrapperUnloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 맵 이벤트 해제
            if (MapControl != null)
            {
                MapControl.OnMapZoomChanged -= OnMapZoomChanged;
                MapControl.OnAreaChange -= OnMapAreaChanged;
            }

            // FrameworkElement 이벤트 해제
            SizeChanged -= OnSizeChangedInternal;
            LayoutUpdated -= OnLayoutUpdatedInternal;
            Loaded -= OnWrapperLoaded;

            _log?.Info($"GMapAdornerWrapper 언로드 완료: {GetDebugInfo()}");
        }
        catch (Exception ex)
        {
            _log?.Error($"GMapAdornerWrapper 언로드 실패: {ex.Message}");
        }
    }

    #endregion

    #region Debug Information

    /// <summary>
    /// 디버그 정보 문자열
    /// </summary>
    public override string ToString()
    {
        return GetDebugInfo();
    }

    #endregion

    #region Private Fields

    private Point _previousPosition = new Point(double.NaN, double.NaN);

    #endregion
}


using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Enums;
using System.Net.NetworkInformation;
using System.Buffers;
using Autofac.Core;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 3:54:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapCustomMarker : GMapMarker, IDisposable
{
    #region - Ctors -
    /// <summary>
    /// 생성자
    /// </summary>
    public GMapCustomMarker(ILogService log, SymbolModel symbolModel)
        : base(new PointLatLng(symbolModel.Latitude, symbolModel.Longitude))
    {
        _log = log;
        _model = symbolModel;

        // 기본 설정
        ZIndex = 10;
        UpdateOffset(); // 중심 기준 오프셋 설정

        // Shape 설정 추가
        CreateMarkerShape();

        // 명령어 초기화
        InitializeCommands();
        Initializer();
        _log?.Info($"마커 생성: {symbolModel.Title} ({symbolModel.Latitude:F6}, {symbolModel.Longitude:F6})");
    }
    #endregion
    #region - Dispose Pattern -

    // Dispose method
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        try
        {
            if (!_disposed && disposing)

            {
                // Managable resources was disposed...
                if (_monitorTimer != null)
                {
                    _monitorTimer.Stop();
                    _monitorTimer.Tick -= MonitorTick;
                    _monitorTimer = null;
                }

                if (_eventToken != null)
                {
                    _eventToken.Cancel();
                    _eventToken.Dispose();
                    _eventToken = null;
                }

                // 관리 리소스 정리
                _model = null;
                Clear();
            }

            // Dispose flag set true
            _disposed = true;
        }
        catch
        {
        }
    }

    // Dtor
    ~GMapCustomMarker()
    {
        Dispose(false);
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    private void Initializer()
    {
        if (Category == EnumMarkerCategory.PIDS_EQUIPMENT)
        {
            _eventToken = new CancellationTokenSource();
            _monitorTimer = new DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromSeconds(1);
            _monitorTimer.Tick += MonitorTick;
            StartTimer();
        }
    }

    public void StartTimer()
    {
        if (_monitorTimer != null && !_monitorTimer.IsEnabled)
        {
            _monitorTimer.Start();
            _refreshTime = DateTime.Now;
        }
    }

    public void StopTimer()
    {
        if (_monitorTimer != null && !_monitorTimer.IsEnabled)
        {
            _monitorTimer.Stop();
        }
    }

    private void MonitorTick(object? sender, EventArgs e)
    {
        if (DateTime.Now > _refreshTime + TimeSpan.FromSeconds(TIMEOUT))
        {
            OperationState = EnumOperationState.DEACTIVE;
        }
    }
    #endregion
    #region - Processes -
    /// <summary>
    /// 마커 Shape 생성
    /// </summary>
    private void CreateMarkerShape()
    {
        try
        {
            // 기본 마커 UI 컨트롤 생성
            var markerControl = new GMapMarkerBasicCustomControl(this);

            // 마커 모양 설정
            markerControl.Width = _model.Width;
            markerControl.Height = _model.Height;
            markerControl.MarkerTitle = _model.Title;

            // Shape 속성에 할당
            Shape = markerControl;

            _log?.Info($"마커 '{_model.Title}' Shape 생성 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 Shape 생성 실패: {ex.Message}");

            // 대체 Shape (기본 Rectangle)
            CreateFallbackShape();
        }
    }

    /// <summary>
    /// 대체 Shape 생성 (기본 사각형)
    /// </summary>
    private void CreateFallbackShape()
    {
        try
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = _model.Width,
                Height = _model.Height,
                Fill = System.Windows.Media.Brushes.Red,
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2
            };

            Shape = rect;
            _log?.Info($"마커 '{_model.Title}' 대체 Shape 생성 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"대체 Shape 생성도 실패: {ex.Message}");
        }
    }
    /// <summary>
    /// 상태 변경 처리
    /// </summary>
    private void OnStatusChanged(EnumOperationState status)
    {
        // 상태별 추가 처리
        switch (status)
        {
            case EnumOperationState.ACTIVE:
                StartTimer();
                break;
            case EnumOperationState.DEACTIVE:
                StopTimer();
                break;
        }
    }

    /// <summary>
    /// 마커 위치 업데이트
    /// </summary>
    public void UpdateLocation(PointLatLng newPosition)
    {
        try
        {
            Position = newPosition;
            _model.UpdatePosition(newPosition);

            OnPropertyChanged(nameof(Position));
            _log?.Info($"마커 '{Title}' 위치 업데이트: ({newPosition.Lat:F6}, {newPosition.Lng:F6})");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 위치 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 크기 업데이트
    /// </summary>
    public void UpdateSize(double width, double height)
    {
        try
        {
            _model.SetSize(width, height);

            // 🔧 UI 컨트롤 크기도 즉시 업데이트
            if (Shape is GMapMarkerBasicCustomControl markerControl)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    markerControl.Width = width;
                    markerControl.Height = height;
                    //markerControl.MarkerSize = Math.Max(width, height);
                });
            }

            // 오프셋 재계산 (중심 기준)
            UpdateOffset();

            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));

            _log?.Info($"마커 '{Title}' 크기 업데이트: {Width:F0}x{Height:F0}");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 크기 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 회전 업데이트
    /// </summary>
    public void UpdateRotation(double newBearing)
    {
        try
        {
            _model.SetBearing(newBearing);

            OnPropertyChanged(nameof(Bearing));
            _log?.Info($"마커 '{Title}' 회전 업데이트: {Bearing:F1}°");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 회전 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// GMapControl과 위치 강제 동기화
    /// </summary>
    public override void ForceUpdateLocalPosition(GMapControl mapControl)
    {
        if (mapControl == null) return;
        base.ForceUpdateLocalPosition(mapControl);
        _log?.Info($"마커 '{Title}' 화면좌표 동기화: ({LocalPositionX}, {LocalPositionY})");
    }

    /// <summary>
    /// Model의 위치를 GMapMarker Position으로 동기화
    /// </summary>
    public void SyncPositionFromModel()
    {
        var newPosition = new PointLatLng(_model.Latitude, _model.Longitude);
        if (Position != newPosition)
        {
            Position = newPosition;
            OnPropertyChanged(nameof(Position));
        }
    }

    /// <summary>
    /// GMapMarker Position을 Model로 동기화
    /// </summary>
    public void SyncPositionToModel()
    {
        if (_model.Latitude != Position.Lat || _model.Longitude != Position.Lng)
        {
            _model.Latitude = Position.Lat;
            _model.Longitude = Position.Lng;
        }
    }

    #endregion
    #region Adorner Support Methods

    /// <summary>
    /// 편집 핸들 위치 계산 (간단 버전)
    /// </summary>
    public Point[] GetEditHandlePositions(GMapControl mapControl)
    {
        if (mapControl == null) return Array.Empty<Point>();

        try
        {
            var screenPos = mapControl.FromLatLngToLocal(Position);
            double editRadius = Math.Max(Width, Height) / 2 + MarkerEditSettings.EditAreaOffset;

            return new Point[]
            {
                new Point(screenPos.X, screenPos.Y),                                    // Move (중심)
                new Point(screenPos.X, screenPos.Y - editRadius - MarkerEditSettings.RotateHandleDistance), // Rotate (북쪽)
                new Point(screenPos.X + editRadius, screenPos.Y),                      // ResizeEast
                new Point(screenPos.X - editRadius, screenPos.Y),                      // ResizeWest  
                new Point(screenPos.X, screenPos.Y + editRadius),                      // ResizeSouth
                new Point(screenPos.X, screenPos.Y - editRadius)                       // ResizeNorth
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"편집 핸들 위치 계산 실패: {ex.Message}");
            return new Point[0];
        }
    }

    /// <summary>
    /// 특정 화면 좌표가 어떤 핸들에 해당하는지 확인
    /// </summary>
    public MarkerHandle GetHandleAtPoint(Point screenPoint, GMapControl mapControl)
    {
        if (mapControl == null) return MarkerHandle.None;

        try
        {
            var handles = GetEditHandlePositions(mapControl);
            if (handles.Length < 6) return MarkerHandle.None;

            double tolerance = MarkerEditSettings.HandleTolerance;

            // 핸들 순서: Move, Rotate, ResizeEast, ResizeWest, ResizeSouth, ResizeNorth
            for (int i = 0; i < handles.Length; i++)
            {
                if (MarkerEditUtils.IsPointNear(screenPoint, handles[i], tolerance))
                {
                    return (MarkerHandle)(i + 1);
                }
            }

            return MarkerHandle.None;
        }
        catch (Exception ex)
        {
            _log?.Error($"핸들 감지 실패: {ex.Message}");
            return MarkerHandle.None;
        }
    }
    #endregion
    #region Helper Methods

    /// <summary>
    /// 오프셋 업데이트 (중심 기준)
    /// </summary>
    private void UpdateOffset()
    {
        Offset = new Point(-(_model.Width / 2.0), -(_model.Height / 2.0));
    }

    /// <summary>
    /// 명령어 초기화
    /// </summary>
    private void InitializeCommands()
    {
        ShowPropertyCommand = new RelayCommand(ExecuteShowProperties, CanExecuteShowProperties);
        DeleteMarkerCommand = new RelayCommand(ExecuteDeleteMarker, CanExecuteDeleteMarker);
    }

    private bool CanExecuteShowProperties(object arg) => true;
    private void ExecuteShowProperties(object obj)
    {
        throw new NotImplementedException();
    }
    
    private bool CanExecuteDeleteMarker(object arg) => true;
    private void ExecuteDeleteMarker(object obj)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// 마커 삭제
    /// </summary>
    private void DeleteMarker()
    {
        // TODO: 삭제 로직 구현
        _log?.Info($"마커 삭제: {Title}");
    }

    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -

    /// <summary>
    /// 객체 고유 ID
    /// </summary>
    public int Id
    {
        get { return _model.Id; }
        set
        {
            _model.Id = value;
            OnPropertyChanged(nameof(Id));
        }
    }

    /// <summary>
    /// 부모 객체 ID
    /// </summary>
    public int Pid
    {
        get { return _model.Pid; }
        set
        {
            _model.Pid = value;
            OnPropertyChanged(nameof(Pid));
        }
    }

    /// <summary>
    /// 객체 제목/이름
    /// </summary>
    public string Title
    {
        get { return _model.Title; }
        set
        {
            _model.Title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    
    /// <summary>
    /// 객체 활성/비활성 상태
    /// </summary>
    public EnumOperationState OperationState
    {
        get { return _model.OperationState; }
        set
        {
            _model.OperationState = value;
            OnPropertyChanged(nameof(OperationState));

            // 상태 변경 이벤트 발생
            StatusChanged?.Invoke();
            OnStatusChanged(value);
        }
    }

    /// <summary>
    /// 고도 (미터)
    /// </summary>
    public float Altitude
    {
        get { return _model.Altitude; }
        set
        {
            _model.Altitude = value;
            OnPropertyChanged(nameof(Altitude)); ;
        }
    }

    /// <summary>
    /// 피치 각도 (도)
    /// </summary>
    public double Pitch
    {
        get { return (double)_model.Pitch; }
        set
        {
            _model.Pitch = (float)value;
            OnPropertyChanged(nameof(Pitch)); ;
        }
    }

    /// <summary>
    /// 롤 각도 (도)
    /// </summary>
    public double Roll
    {
        get { return (double)_model.Roll; }
        set
        {
            _model.Roll = (float)value;
            OnPropertyChanged(nameof(Roll)); ;
        }
    }

    /// <summary>
    /// 마커 너비 (픽셀)
    /// </summary>
    public double Width
    {
        get { return _model.Width; }
        set
        {
            _model.Width = value;
            OnPropertyChanged(nameof(Width)); ;

            // Offset 재계산
            UpdateOffset();
        }
    }

    /// <summary>
    /// 마커 높이 (픽셀)
    /// </summary>
    public double Height
    {
        get { return _model.Height; }
        set
        {
            _model.Height = value;
            OnPropertyChanged(nameof(Height)); ;

            // Offset 재계산
            UpdateOffset();
        }
    }

    /// <summary>
    /// 방향각 (도, 0-360)
    /// </summary>
    public double Bearing
    {
        get { return _model.Bearing; }
        set
        {
            // 0-360도 범위로 정규화
            while (value < 0) value += 360;
            while (value >= 360) value -= 360;

            _model.Bearing = value;
            OnPropertyChanged(nameof(Bearing)); ;
        }
    }

    /// <summary>
    /// 마커 카테고리
    /// </summary>
    public EnumMarkerCategory Category
    {
        get { return _model.Category; }
        set
        {
            _model.Category = value;
            OnPropertyChanged(nameof(Category)); ;
        }
    }


    /// <summary>
    /// 마커 표시 여부
    /// </summary>
    public bool Visibility
    {
        get { return _model.Visibility; }
        set
        {
            _model.Visibility = value;
            OnPropertyChanged(nameof(Visibility)); ;
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected)); ;
        }
    }

    #endregion
    #region - 이벤트 및 명령 -

    /// <summary>
    /// 상태 변경 이벤트
    /// </summary>
    public event System.Action? StatusChanged;
    public RelayCommand ShowPropertyCommand { get; private set; }
    public RelayCommand DeleteMarkerCommand { get; private set; }

    #endregion
    #region - Attributes -
    private ILogService? _log;
    private SymbolModel _model;

    private DispatcherTimer? _monitorTimer;
    private const int TIMEOUT = 60 * 60;
    private const int MAX_BLINK = 11;
    private DateTime _refreshTime;
    private bool _disposed = false;

    private PointLatLng _previousPosition;

    private CancellationTokenSource? _eventToken;
    private bool _isSelected;
    private const double PITCH_MAX = 90d;
    private const double ROLL_MAX = 90d;
    #endregion
}
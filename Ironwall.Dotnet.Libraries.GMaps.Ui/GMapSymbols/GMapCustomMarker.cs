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
    public GMapCustomMarker(ILogService log, SymbolModel symbolModel) : base(new PointLatLng(symbolModel.Latitude, symbolModel.Longitude))
    {
        _log = log;
        _model = symbolModel;

        // Shape 할당
        Offset = new Point(-(_model.Width / 2.0), -(_model.Height / 2.0));

        // Command 초기화
        PropertyCommand = new RelayCommand(ShowProperties);
        DeleteCommand = new RelayCommand(DeleteMarker);
        EraseCommand = new RelayCommand(EraseMarker);

        Initializer();
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

                //Shape Framework element can be disposable...
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
    #endregion
    #region - Processes -
    /// <summary>
    /// Offset 업데이트
    /// </summary>
    private void UpdateOffset()
    {
        Offset = new Point(-(_model.Width / 2.0), -(_model.Height / 2.0));
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
    /// 위치 업데이트 시 Position과 Model 동기화
    /// </summary>
    public void UpdateLocation(PointLatLng newLocation)
    {
        Position = newLocation;              // GMapMarker.Position 업데이트
        _model.Latitude = newLocation.Lat;   // Model 동기화
        _model.Longitude = newLocation.Lng;  // Model 동기화

        OnPropertyChanged(nameof(Position));
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

        //Debug.WriteLine($"Object({Id}) Current Status : {Status}");
    }


    // Command method implementations
    private void ShowProperties()
    {
        DispatcherService.Invoke((System.Action)(() =>
        {
            //var windowManager = IoC.Get<IWindowManager>();
            //if (DType == EnumDType.SENSOR)
            //{
            //    var property = IoC.Get<PropertySensorViewModel>();
            //    if (property.IsActive == true) return;

            //    property.UpdateProperty(this);
            //    await property.ActivateAsync();
            //    await windowManager.ShowWindowAsync(property);
            //}
            //else
            //{
            //    var property = IoC.Get<PropertyViewModel>();
            //    if (property.IsActive == true) return;

            //    property.UpdateProperty(this);
            //    await property.ActivateAsync();
            //    await windowManager.ShowWindowAsync(property);
            //}
        }));
    }

    private void DeleteMarker()
    {
        DispatcherService.Invoke((System.Action)(() =>
        {
            //var target = Map.Markers.OfType<GMapCustomMarker>().Where(entity => entity.Id == Id
            //                            && entity.IconEnum == IconEnum).FirstOrDefault();
            //Map.Markers.Remove(target);
        }));
        Dispose();
    }

    private void EraseMarker(object obj)
    {
        DispatcherService.Invoke((System.Action)(() =>
        {
            //var windowManager = IoC.Get<IWindowManager>();
            //var confirm = IoC.Get<ConfirmViewModel>();
            //if (confirm.IsActive == true) return;

            //var message = new EraseRequestMessage((int)Id);
            //confirm.Update(message);
            //await confirm.ActivateAsync();
            //await windowManager.ShowWindowAsync(confirm);
        }));
    }

    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -

    /// <summary>
    /// 객체 고유 ID
    /// </summary>
    public uint Id
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
    public uint Pid
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

    #endregion

    #region - 이벤트 및 명령 -

    /// <summary>
    /// 상태 변경 이벤트
    /// </summary>
    public event System.Action? StatusChanged;

    /// <summary>
    /// 속성 보기 명령
    /// </summary>
    public ICommand PropertyCommand { get; }

    /// <summary>
    /// 삭제 명령
    /// </summary>
    public ICommand DeleteCommand { get; }

    /// <summary>
    /// 지우기 명령
    /// </summary>
    public ICommand EraseCommand { get; }
    #endregion
    #region - Attributes -
    private ILogService? _log;
    private SymbolModel _model;
    private double _width;
    private double _height;
    private double _bearing;

    private DispatcherTimer? _monitorTimer;
    private const int TIMEOUT = 60 * 60;
    private const int MAX_BLINK = 11;
    private DateTime _refreshTime;
    private bool _disposed = false;
    private bool _visibility;

    private PointLatLng _previousPosition;

    private CancellationTokenSource? _eventToken;

    private const double PITCH_MAX = 90d;
    private const double ROLL_MAX = 90d;
    private const double MINIMUM_APPLIED_VEHICLE_DISTANCE = 15d;
    private const double MAXIMUM_SNAPED_VEHICLE_DISTANCE = 25d;
    private const double MINIMUM_APPLIED_SENSOR_DISTANCE = 50d;
    private const double MINIMUM_APPLIED_MISSION_DISTANCE = 15d;
    //private const double MINIMUM_APPLIED_VEHICLE_DISTANCE = 15d;
    //private const double MAXIMUM_SNAPED_VEHICLE_DISTANCE = 25d;
    #endregion
}
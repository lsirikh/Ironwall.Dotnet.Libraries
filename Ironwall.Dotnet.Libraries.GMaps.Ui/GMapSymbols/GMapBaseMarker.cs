using System;
using System.Windows;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/19/2025 12:33:42 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class GMapBaseMarker<T> : GMapMarker, IDisposable, IEditableMarker where T : ISymbolModel
{
    #region - Ctors -
    /// <summary>
    /// 제네릭 기반 마커 생성자
    /// </summary>
    protected GMapBaseMarker(ILogService log, T symbolModel)
        : base(new PointLatLng(symbolModel.Latitude, symbolModel.Longitude))
    {
        _log = log;
        _model = symbolModel;

        // 기본 설정 — 모델의 ZOrder 사용
        ZIndex = symbolModel.ZOrder;
        UpdateOffset();

        // 가상 메서드들 호출
        InitializeMarker();
        CreateMarkerShape();
        InitializeCommands();

        // Shape 생성 후 Panel.ZIndex 동기화
        if (Shape is System.Windows.UIElement shapeElement)
            System.Windows.Controls.Panel.SetZIndex(shapeElement, ZIndex);

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
                // 관리 리소스 정리
                _model = default(T);
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
    ~GMapBaseMarker()
    {
        Dispose(false);
    }
    #endregion
    #region - Abstract Methods (상속 클래스에서 반드시 구현) -

    /// <summary>
    /// 마커 컨트롤 생성 (추상 메서드)
    /// </summary>
    protected abstract UIElement CreateMarkerControl();

    #endregion

    #region - Virtual Methods (상속 클래스에서 선택적 오버라이드) -

    /// <summary>
    /// 마커 초기화 (가상 메서드)
    /// </summary>
    protected virtual void InitializeMarker()
    {
    }

    /// <summary>
    /// 마커 Shape 생성 (가상 메서드)
    /// </summary>
    protected virtual void CreateMarkerShape()
    {
        try
        {
            var markerControl = CreateMarkerControl();

            ConfigureMarkerControl(markerControl);
            Shape = markerControl;
            //기본적으로 시현하는 것을 기본으로 한다.
            IsVisible = true;
            EnableShapeAnimation = false;
            //_log?.Info($"마커 '{_model.Title}' Shape 생성 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 Shape 생성 실패: {ex.Message}");
            CreateFallbackShape();
        }
    }

    /// <summary>
    /// 마커 컨트롤 설정 (가상 메서드)
    /// </summary>
    protected virtual void ConfigureMarkerControl(UIElement marker)
    {
        // 마커 모양 설정
        //control.Width = _model.Width;
        //control.Height = _model.Height;
        //control.MarkerTitle = _model.Title;
    }

    /// <summary>
    /// 상태 변경 처리 (가상 메서드)
    /// </summary>
    protected virtual void OnStatusChanged(EnumOperationState status)
    {
    }

    /// <summary>
    /// 명령어 초기화 (가상 메서드)
    /// </summary>
    protected virtual void InitializeCommands()
    {
        ShowPropertyCommand = new RelayCommand(ExecuteShowProperties, CanExecuteShowProperties);
    }

    #endregion

    #region - Common Methods (공통 메서드들) -
    /// <summary>
    /// 대체 Shape 생성
    /// </summary>
    protected virtual void CreateFallbackShape()
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
            //_log?.Info($"마커 '{_model.Title}' 대체 Shape 생성 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"대체 Shape 생성도 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 위치 업데이트
    /// </summary>
    public virtual void UpdateLocation(PointLatLng newPosition)
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
    public virtual void UpdateSize(double width, double height)
    {
        try
        {
            _model.SetSize(width, height);

            // UI 컨트롤 크기도 즉시 업데이트
            UpdateShapeSize(width, height);

            // 오프셋 재계산
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
    /// Shape 크기 업데이트 (가상 메서드)
    /// </summary>
    protected virtual void UpdateShapeSize(double width, double height)
    {
        if (Shape is FrameworkElement element)
        {
            DispatcherService.Invoke(() =>
            {
                element.Width = width;
                element.Height = height;
            });
        }
    }

    /// <summary>
    /// 마커 회전 업데이트
    /// </summary>
    public virtual void UpdateRotation(double newBearing)
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

    #endregion
    #region - Processes -
    #endregion
    #region - Command Methods -

    protected virtual bool CanExecuteShowProperties(object arg) => true;
    protected virtual void ExecuteShowProperties(object obj)
    {
        _log?.Info($"ShowProperties was executed!");
    }

    #endregion
    #region - Helper Methods -

    /// <summary>
    /// 오프셋 업데이트 (중심 기준)
    /// </summary>
    protected void UpdateOffset()
    {
        Offset = new Point(-(_model.Width / 2.0), -(_model.Height / 2.0));
    }

    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public int Id
    {
        get => _model.Id;
        set
        {
            _model.Id = value;
            OnPropertyChanged(nameof(Id));
        }
    }

    public int Pid
    {
        get => _model.Pid;
        set
        {
            _model.Pid = value;
            OnPropertyChanged(nameof(Pid));
        }
    }

    public string Title
    {
        get => _model.Title;
        set
        {
            //_log?.Info($"[GMapCustomMarker] Title 변경: '{Title}' -> '{value}'");
            //_log?.Info($"  호출 스택: {Environment.StackTrace}");
            _model.Title = value;
            OnPropertyChanged(nameof(Title));
        }
    }
    public double TitleSize
    {
        get => _model.TitleSize;
        set
        {
            _model.TitleSize = value;
            OnPropertyChanged(nameof(TitleSize));
        }
    }

    public EnumOperationState OperationState
    {
        get => _model.OperationState;
        set
        {
            _model.OperationState = value;
            OnPropertyChanged(nameof(OperationState));
            StatusChanged?.Invoke();
            OnStatusChanged(value);
        }
    }
    public double Latitude
    {
        get => _model.Latitude;
        set
        {
            _model.Latitude = value;
            OnPropertyChanged(nameof(Latitude));
        }
    }
    public double Longitude
    {
        get => _model.Longitude;
        set
        {
            _model.Longitude = value;
            OnPropertyChanged(nameof(Longitude));
        }
    }

    public float Altitude
    {
        get => _model.Altitude;
        set
        {
            _model.Altitude = value;
            OnPropertyChanged(nameof(Altitude));
        }
    }

    public double Zoom
    {
        get => _model.Zoom;
        set
        {
            _model.Zoom = value;
            OnPropertyChanged(nameof(Zoom));
        }
    }

    public double Width
    {
        get => _model.Width;
        set
        {
            //_log?.Info($"[GMapCustomMarker] Width 변경: {Width} -> {value}");
            //_log?.Info($"  호출 스택: {Environment.StackTrace}");
            _model.Width = value;
            OnPropertyChanged(nameof(Width));
            UpdateOffset();
        }
    }

    public double Height
    {
        get => _model.Height;
        set
        {
            //_log?.Info($"[GMapCustomMarker] Width 변경: {Height} -> {value}");
            //_log?.Info($"  호출 스택: {Environment.StackTrace}");
            _model.Height = value;
            OnPropertyChanged(nameof(Height));
            UpdateOffset();
        }
    }
    

    public double Bearing
    {
        get => _model.Bearing;
        set
        {
            while (value < 0) value += 360;
            while (value >= 360) value -= 360;

            _model.Bearing = value;
            OnPropertyChanged(nameof(Bearing));
        }
    }

    public EnumMarkerCategory Category
    {
        get => _model.Category;
        set
        {
            _model.Category = value;
            OnPropertyChanged(nameof(Category));
        }
    }

    public bool ShowShape
    {
        get => _model.ShowShape;
        set
        {
            _model.ShowShape = value;
            OnPropertyChanged(nameof(ShowShape));
        }
    }

    public bool ShowTitle
    {
        get => _model.ShowTitle;
        set
        {
            _model.ShowTitle = value;
            OnPropertyChanged(nameof(ShowTitle));
        }
    }

    /// <summary>라벨 상대 오프셋 — 모델 연동(영속). LabelAdorner가 읽고 드래그 시 갱신(Symbol_Label_Decouple).</summary>
    public double LabelOffsetX
    {
        get => _model?.LabelOffsetX ?? 0d;
        set { if (_model != null) _model.LabelOffsetX = value; OnPropertyChanged(nameof(LabelOffsetX)); }
    }
    public double LabelOffsetY
    {
        get => _model?.LabelOffsetY ?? 0d;
        set { if (_model != null) _model.LabelOffsetY = value; OnPropertyChanged(nameof(LabelOffsetY)); }
    }

    public EnumColorType FillColor
    {
        get => _model.FillColor;
        set
        {
            _model.FillColor = value;
            OnPropertyChanged(nameof(FillColor));
        }
    }

    public EnumColorType StrokeColor
    {
        get => _model.StrokeColor;
        set
        {
            _model.StrokeColor = value;
            OnPropertyChanged(nameof(StrokeColor));
        }
    }

    public double StrokeThickness
    {
        get => _model.StrokeThickness;
        set
        {
            _model.StrokeThickness = value;
            OnPropertyChanged(nameof(StrokeThickness));
        }
    }

    int IEditableMarker.ZOrder
    {
        get => ZIndex;  // GMapMarker.ZIndex (GMap.NET ItemContainerStyle 바인딩 → 렌더 순서)
        set
        {
            ZIndex = value;  // GMap.NET PropertyChanged → ItemContainerStyle 바인딩 갱신
            _model.ZOrder = value;
            if (Shape is System.Windows.UIElement shapeEl)
                System.Windows.Controls.Panel.SetZIndex(shapeEl, value);
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public bool IsLayerEnabled
    {
        get => _isLayerEnabled;
        set
        {
            _isLayerEnabled = value;
            OnPropertyChanged(nameof(IsLayerEnabled));
        }
    }

    /// <summary>잠금 상태 — 모델 IsLocked 연동(영속). 맵 클릭 차단은 GMapCustomControl 히트테스트/선택 가드가 처리.</summary>
    public bool IsLocked
    {
        get => _model?.IsLocked ?? false;
        set
        {
            if (_model != null) _model.IsLocked = value;
            OnPropertyChanged(nameof(IsLocked));
        }
    }

    public bool EnableShapeAnimation
    {
        get => _enableShapeAnimation;
        set 
        {
            _enableShapeAnimation = value;
            OnPropertyChanged(nameof(EnableShapeAnimation));
        }
    }


    /// <summary>
    /// 강타입 모델 속성
    /// </summary>
    public T Model => _model;
    public RelayCommand? ShowPropertyCommand { get; protected set; }
    public bool IsDisposed => _disposed;
    #endregion
    #region - Attributes -
    public event System.Action? StatusChanged;

    protected ILogService? _log;
    protected T _model;

    protected bool _disposed = false;
    protected bool _isSelected;
    private bool _enableShapeAnimation;
    private bool _isVisible;
    private bool _isLayerEnabled = true;
    #endregion
}
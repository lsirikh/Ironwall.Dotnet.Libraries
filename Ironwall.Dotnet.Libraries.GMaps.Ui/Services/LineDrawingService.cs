using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/16/2025 10:44:13 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 라인 드로잉 상태
    /// </summary>
    public enum LineDrawingState
    {
        None,
        FirstClick,
        Drawing,
        Completed,
        Cancelled
    }

    /// <summary>
    /// 라인 드로잉 서비스
    /// Adorner를 사용한 라인 드로잉과 심볼 등록을 통합 관리
    /// </summary>
    public class LineDrawingService : IDisposable
    {
        #region Fields

        private readonly ILogService _log;
        private readonly GMapCustomControl _mapControl;
        private LineDrawingAdorner _currentAdorner;
        private AdornerLayer _adornerLayer;
        private LineDrawingState _currentState = LineDrawingState.None;
        private LineDrawingParameters _parameters;
        private bool _disposed = false;

        // 컨트롤 처리 중 플래그
        private bool _isProcessingControl = false;
        #endregion

        #region Events

        /// <summary>
        /// 드로잉 상태 변경 이벤트
        /// </summary>
        public event EventHandler<LineDrawingState> StateChanged;

        /// <summary>
        /// 포인트 추가 이벤트
        /// </summary>
        public event EventHandler<PointLatLng> PointAdded;

        /// <summary>
        /// 라인 완성 이벤트
        /// </summary>
        public event EventHandler<ILineEditableMarker> LineCompleted;

        /// <summary>
        /// 드로잉 취소 이벤트
        /// </summary>
        public event EventHandler DrawingCancelled;

        #endregion

        #region Constructor

        public LineDrawingService(GMapCustomControl mapControl, ILogService log = null)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            _log = log;

            InitializeService();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 라인 드로잉 시작
        /// </summary>
        public async Task<bool> StartLineDrawingAsync(LineDrawingParameters parameters = null)
        {
            try
            {
                // 이미 드로잉 중이면 취소
                if (_currentState != LineDrawingState.None)
                {
                    await CancelDrawingAsync();
                }

                _parameters = parameters ?? new LineDrawingParameters();

                // AdornerLayer 가져오기
                _adornerLayer = AdornerLayer.GetAdornerLayer(_mapControl);
                if (_adornerLayer == null)
                {
                    _log?.Error("AdornerLayer를 찾을 수 없습니다.");
                    return false;
                }

                // LineDrawingAdorner 생성 및 추가
                _currentAdorner = new LineDrawingAdorner(_mapControl, _mapControl, _log);

                // 컨트롤 UI 이벤트 구독 (누락된 부분!)
                _currentAdorner.CompleteRequested += OnAdornerCompleteRequested;
                _currentAdorner.UndoRequested += OnAdornerUndoRequested;
                _currentAdorner.CancelRequested += OnAdornerCancelRequested;

                // Adorner 스타일 설정
                _currentAdorner.SetLineStyle(
                    ColorHelper.ToBrush(_parameters.Model.StrokeColor),
                    _parameters.Model.StrokeThickness,
                    GetDashStyle(_parameters.Model.LinePattern));

                _adornerLayer.Add(_currentAdorner);

                // 이벤트 구독
                SubscribeToMapEvents();

                // 상태 변경
                SetState(LineDrawingState.FirstClick);
                _mapControl.Cursor = Cursors.Cross;

                _log?.Info($"라인 드로잉 시작: {_parameters.Model.Title}");
                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 시작 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 라인 드로잉 완료
        /// </summary>
        public Task<bool> CompleteDrawingAsync()
        {
            if (_isProcessingControl) return Task.FromResult(false);

            try
            {
                _isProcessingControl = true;

                if (_currentState != LineDrawingState.Drawing || _currentAdorner?.IsValid != true)
                {
                    _log?.Warning("완료할 수 있는 드로잉이 없습니다.");
                    return Task.FromResult(false);
                }

                // 완성된 지리 좌표 가져오기
                var geoPoints = _currentAdorner.GeoPoints;
                var totalDistance = _currentAdorner.TotalDistance;

                // 실제 라인 마커 생성
                if (_parameters.Model == null) throw new NullReferenceException("LineParameter has no model.");

                if(_parameters.Model is PidsGroupSymbolModel pGroup)
                {
                    var pGroupMarker = CreateFinalPidsGroupMarker(geoPoints);
                    if (pGroupMarker == null) throw new NullReferenceException("lineMarker was failed to create a Model.");

                    _log?.Info($"라인 완성: {_parameters.Model.Title}, {geoPoints.Count}개 포인트, 거리: {totalDistance:F1}m");

                    // 완료 이벤트 발생
                    LineCompleted?.Invoke(this, pGroupMarker);
                }
                else if (_parameters.Model is LineSymbolModel line)
                {
                    var lineMarker = CreateFinalLineMarker(geoPoints);
                    if (lineMarker == null) throw new NullReferenceException("lineMarker was failed to create a Model.");

                    _log?.Info($"라인 완성: {_parameters.Model.Title}, {geoPoints.Count}개 포인트, 거리: {totalDistance:F1}m");

                    // 완료 이벤트 발생
                    LineCompleted?.Invoke(this, lineMarker);
                }


                // 정리
                CleanupDrawing();
                SetState(LineDrawingState.Completed);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 완료 실패: {ex.Message}");
                return Task.FromResult(false);
            }
            finally
            {
                _isProcessingControl = false;
            }
        }

        /// <summary>
        /// 라인 드로잉 취소
        /// </summary>
        public Task<bool> CancelDrawingAsync()
        {
            // 컨트롤에서 호출된 경우 중복 방지
            if (_isProcessingControl) return Task.FromResult(false);

            try
            {
                _isProcessingControl = true;

                if (_currentState == LineDrawingState.None)
                {
                    return Task.FromResult(false);
                }

                _log?.Info("라인 드로잉 취소");

                // 취소 이벤트 발생
                DrawingCancelled?.Invoke(this, EventArgs.Empty);

                // 정리
                CleanupDrawing();
                SetState(LineDrawingState.Cancelled);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 취소 실패: {ex.Message}");
                return Task.FromResult(false);
            }
            finally
            {
                _isProcessingControl = false;
            }
        }

        /// <summary>
        /// 마지막 포인트 제거
        /// </summary>
        public bool UndoLastPoint()
        {
            if (_currentAdorner?.RemoveLastPoint() == true)
            {
                // 포인트가 0개가 되면 FirstClick 상태로
                if (_currentAdorner.PointCount == 0)
                {
                    SetState(LineDrawingState.FirstClick);
                }

                _log?.Info($"마지막 포인트 제거 (남은 개수: {_currentAdorner.PointCount})");
                return true;
            }
            return false;
        }

        #endregion

        #region Properties

        /// <summary>
        /// 현재 드로잉 상태
        /// </summary>
        public LineDrawingState CurrentState => _currentState;

        /// <summary>
        /// 드로잉 중 여부
        /// </summary>
        public bool IsDrawing => _currentState == LineDrawingState.FirstClick ||
                                  _currentState == LineDrawingState.Drawing;

        /// <summary>
        /// 현재 포인트 개수
        /// </summary>
        public int PointCount => _currentAdorner?.PointCount ?? 0;

        /// <summary>
        /// 현재 총 거리
        /// </summary>
        public double TotalDistance => _currentAdorner?.TotalDistance ?? 0;

        #endregion
        #region Adorner Event Handlers

        /// <summary>
        /// Adorner 완료 요청 핸들러
        /// </summary>
        private async void OnAdornerCompleteRequested(object? sender, EventArgs e)
        {
            _log?.Info("Adorner에서 완료 요청");
            await CompleteDrawingAsync();
        }

        /// <summary>
        /// Adorner Undo 요청 핸들러
        /// </summary>
        private void OnAdornerUndoRequested(object? sender, EventArgs e)
        {
            _log?.Info("Adorner에서 Undo 요청");
            UndoLastPoint();
        }

        /// <summary>
        /// Adorner 취소 요청 핸들러
        /// </summary>
        private async void OnAdornerCancelRequested(object? sender, EventArgs e)
        {
            _log?.Info("Adorner에서 취소 요청");
            await CancelDrawingAsync();
        }

        #endregion
        #region Private Methods

        /// <summary>
        /// 서비스 초기화
        /// </summary>
        private void InitializeService()
        {
            _log?.Info("LineDrawingService 초기화");
        }

        /// <summary>
        /// 맵 이벤트 구독
        /// </summary>
        private void SubscribeToMapEvents()
        {
            _mapControl.OnMapClicked += OnMapClicked;
            _mapControl.MouseMove += OnMouseMove;
            _mapControl.KeyDown += OnKeyDown;
        }

        /// <summary>
        /// 맵 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromMapEvents()
        {
            _mapControl.OnMapClicked -= OnMapClicked;
            _mapControl.MouseMove -= OnMouseMove;
            _mapControl.KeyDown -= OnKeyDown;
        }

        /// <summary>
        /// 맵 클릭 이벤트 핸들러
        /// </summary>
        private void OnMapClicked(PointLatLng geoPosition, Point screenPosition)
        {
            if (!IsDrawing) return;

            try
            {
                // 포인트 추가
                _currentAdorner?.AddPoint(geoPosition);

                // 상태 업데이트
                if (_currentState == LineDrawingState.FirstClick)
                {
                    SetState(LineDrawingState.Drawing);
                }

                // 이벤트 발생
                PointAdded?.Invoke(this, geoPosition);

                _log?.Info($"포인트 추가: {geoPosition}");
            }
            catch (Exception ex)
            {
                _log?.Error($"맵 클릭 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 마우스 이동 이벤트 핸들러
        /// </summary>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_currentState == LineDrawingState.Drawing)
            {
                var mousePos = e.GetPosition(_mapControl);
                _currentAdorner?.UpdateMousePosition(mousePos);
            }
        }

        /// <summary>
        /// 키보드 이벤트 핸들러
        /// </summary>
        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsDrawing) return;

            try
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        // ESC: 완료 또는 취소
                        if (_currentAdorner?.IsValid == true)
                        {
                            await CompleteDrawingAsync();
                        }
                        else
                        {
                            await CancelDrawingAsync();
                        }
                        e.Handled = true;
                        break;

                    case Key.Enter:
                        // Enter: 완료
                        if (_currentAdorner?.IsValid == true)
                        {
                            await CompleteDrawingAsync();
                        }
                        e.Handled = true;
                        break;

                    case Key.Back:
                    case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                        // Backspace 또는 Ctrl+Z: Undo
                        UndoLastPoint();
                        e.Handled = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"키보드 이벤트 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 최종 라인 마커 생성
        /// </summary>
        private ILineEditableMarker? CreateFinalLineMarker(List<PointLatLng> geoPoints)
        {
            try
            {
                var (width, height, centerPoint) = CalculateBoundingBoxWithCenter(geoPoints);
                var lineModel = new LineSymbolModel
                {
                    Title = _parameters.Model.Title,
                    TitleSize = 10,
                    Latitude = centerPoint.Lat,
                    Longitude = centerPoint.Lng,
                    Width = width,
                    Height = height,
                    Zoom = _mapControl.Zoom,
                    Bearing = 0,
                    Category = _parameters.Model.Category,
                    ShowShape = true,
                    ShowTitle = false,
                    OperationState = EnumOperationState.ACTIVE,
                    StrokeColor = _parameters.Model.StrokeColor,
                    StrokeThickness = _parameters.Model.StrokeThickness,
                    FillColor = _parameters.Model.FillColor,
                    LinePattern = _parameters.Model.LinePattern,
                    LineOpacity = _parameters.Model.LineOpacity,
                    IsClosedPath = _parameters.Model.IsClosedPath,
                    ShowArrowHead = _parameters.Model.ShowArrowHead,
                    LinePoints = geoPoints.Select(p => new GeoPoint
                    {
                        Latitude = p.Lat,
                        Longitude = p.Lng
                    }).ToList()
                };

                return new GMapLineMarker(_log, lineModel);
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 마커 생성 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 최종 PIDS 그룹 마커 생성
        /// </summary>
        private IPidsGroupEditableMarker? CreateFinalPidsGroupMarker(List<PointLatLng> geoPoints)
        {
            try
            {
                var (width, height, centerPoint) = CalculateBoundingBoxWithCenter(geoPoints);

                var pidsGroupModel = new PidsGroupSymbolModel
                {
                    // SymbolModel 기본 속성
                    Title = _parameters.Model.Title,
                    TitleSize = _parameters.Model.TitleSize,
                    Latitude = centerPoint.Lat,
                    Longitude = centerPoint.Lng,
                    Width = width,
                    Height = height,
                    Zoom = _mapControl.Zoom,
                    Bearing = 0,
                    Category = EnumMarkerCategory.AREA_BOUNDARY,
                    ShowShape = true,
                    ShowTitle = _parameters.Model.ShowTitle,
                    OperationState = EnumOperationState.ACTIVE,

                    // 스타일 속성
                    StrokeColor = _parameters.Model.StrokeColor,
                    StrokeThickness = _parameters.Model.StrokeThickness,
                    FillColor = _parameters.Model.FillColor,

                    // LineSymbolModel 속성
                    LinePattern = _parameters.Model.LinePattern,
                    LineOpacity = _parameters.Model.LineOpacity,
                    IsClosedPath = false,  // 그룹은 항상 닫힌 영역
                    ShowArrowHead = false, // 그룹에서는 화살표 불필요
                    LinePoints = geoPoints.Select(p => new GeoPoint
                    {
                        Latitude = p.Lat,
                        Longitude = p.Lng
                    }).ToList(),

                    // PidsGroupSymbolModel 전용 속성
                    LinkedDeviceGroup = 0,  // 나중에 실제 그룹 ID로 설정
                    EventStatus = EnumEventStatus.Normal
                };

                return new GMapPidsGroupMarker(_log, pidsGroupModel);
            }
            catch (Exception ex)
            {
                _log?.Error($"PIDS 그룹 마커 생성 실패: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// 바운딩 박스와 중심점 계산 (수정된 메서드)
        /// </summary>
        private (double Width, double Height, PointLatLng Center) CalculateBoundingBoxWithCenter(List<PointLatLng> geoPoints)
        {
            if (geoPoints == null || geoPoints.Count == 0)
                return (200, 200, new PointLatLng(0, 0));

            double minLat = double.MaxValue, maxLat = double.MinValue;
            double minLng = double.MaxValue, maxLng = double.MinValue;

            foreach (var point in geoPoints)
            {
                minLat = Math.Min(minLat, point.Lat);
                maxLat = Math.Max(maxLat, point.Lat);
                minLng = Math.Min(minLng, point.Lng);
                maxLng = Math.Max(maxLng, point.Lng);
            }

            // 중심점 계산
            var centerLat = (minLat + maxLat) / 2;
            var centerLng = (minLng + maxLng) / 2;
            var centerPoint = new PointLatLng(centerLat, centerLng);

            // 화면상 픽셀 크기 계산
            var topLeft = _mapControl.FromLatLngToLocal(new PointLatLng(maxLat, minLng));
            var bottomRight = _mapControl.FromLatLngToLocal(new PointLatLng(minLat, maxLng));

            double pixelWidth = Math.Abs(bottomRight.X - topLeft.X);
            double pixelHeight = Math.Abs(bottomRight.Y - topLeft.Y);

            // 최소 크기 보장 및 여백 추가
            const double minSize = 100;
            const double padding = 50;

            pixelWidth = Math.Max(minSize, pixelWidth + padding * 2);
            pixelHeight = Math.Max(minSize, pixelHeight + padding * 2);

            return (pixelWidth, pixelHeight, centerPoint);
        }

        /// <summary>
        /// 줌 레벨과 위도에 따른 미터당 픽셀 계산
        /// </summary>
        private double GetMetersPerPixel(double zoom, double latitude)
        {
            // Web Mercator 투영 기준 계산
            var metersPerPixel = 156543.03392 * Math.Cos(latitude * Math.PI / 180) / Math.Pow(2, zoom);
            return metersPerPixel;
        }

        /// <summary>
        /// DashStyle 가져오기
        /// </summary>
        private DashStyle GetDashStyle(EnumLinePattern pattern)
        {
            switch (pattern)
            {
                case EnumLinePattern.Dashed:
                    return new DashStyle(new double[] { 10, 5 }, 0);
                case EnumLinePattern.Dotted:
                    return new DashStyle(new double[] { 2, 3 }, 0);
                case EnumLinePattern.DashDot:
                    return new DashStyle(new double[] { 10, 3, 2, 3 }, 0);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 상태 설정
        /// </summary>
        private void SetState(LineDrawingState newState)
        {
            if (_currentState != newState)
            {
                _currentState = newState;
                StateChanged?.Invoke(this, newState);
                _log?.Info($"드로잉 상태 변경: {newState}");
            }
        }

        /// <summary>
        /// 드로잉 정리
        /// </summary>
        private void CleanupDrawing()
        {
            // 이벤트 구독 해제
            UnsubscribeFromMapEvents();

            // Adorner 이벤트 구독 해제
            if (_currentAdorner != null)
            {
                _currentAdorner.CompleteRequested -= OnAdornerCompleteRequested;
                _currentAdorner.UndoRequested -= OnAdornerUndoRequested;
                _currentAdorner.CancelRequested -= OnAdornerCancelRequested;
            }

            // Adorner 제거
            if (_currentAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_currentAdorner);
                _currentAdorner.Cleanup();
                _currentAdorner = null;
            }

            // 커서 복원
            _mapControl.Cursor = Cursors.Arrow;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                CleanupDrawing();
                _disposed = true;
                _log?.Info("LineDrawingService 리소스 해제");
            }
        }

        #endregion
    }

    /// <summary>
    /// 라인 드로잉 파라미터
    /// </summary>
    public class LineDrawingParameters
    {
        public string Title { get; set; } = "New Line";
        //public EnumMarkerCategory Category { get; set; } = EnumMarkerCategory.AREA_BOUNDARY;
        //public EnumColorType StrokeColor { get; set; } = EnumColorType.Red;
        //public double StrokeThickness { get; set; } = 2;
        //public EnumColorType FillColor { get; set; } = EnumColorType.Transparent;
        //public EnumLinePattern Pattern { get; set; } = EnumLinePattern.Solid;
        //public double Opacity { get; set; } = 1.0;
        //public bool IsClosedPath { get; set; } = false;
        //public bool ShowArrowHead { get; set; } = false;
        public ILineSymbolModel Model { get; set; }
        
    }
}
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Ironwall.Dotnet.Libraries.GMaps.Ui.LineModules;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/12/2025 3:08:52 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/

    /// <summary>
    /// 라인 심볼 등록 완료 이벤트 아규먼트
    /// </summary>
    public class LineSymbolRegisteredEventArgs : EventArgs
    {
        public GMapLineMarker LineMarker { get; }
        public ILineSymbolModel LineModel { get; }
        public DateTime RegisteredTime { get; }

        public LineSymbolRegisteredEventArgs(GMapLineMarker marker, ILineSymbolModel model)
        {
            LineMarker = marker ?? throw new ArgumentNullException(nameof(marker));
            LineModel = model ?? throw new ArgumentNullException(nameof(model));
            RegisteredTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"LineSymbol '{LineModel.Title}' registered with {LineModel.LinePoints.Count} points, " +
                   $"total distance: {LineMarker.TotalDistance:F1}m at {RegisteredTime:HH:mm:ss}";
        }
    }

    /// <summary>
    /// 라인 등록 실패 이벤트 아규먼트
    /// </summary>
    public class LineRegistrationFailedEventArgs : EventArgs
    {
        public string LineTitle { get; }
        public Exception Exception { get; }
        public DateTime FailedTime { get; }

        public LineRegistrationFailedEventArgs(string lineTitle, Exception exception)
        {
            LineTitle = lineTitle ?? "Unknown";
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            FailedTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"LineSymbol '{LineTitle}' registration failed: {Exception.Message} at {FailedTime:HH:mm:ss}";
        }
    }

    /// <summary>
    /// 라인 심볼 등록을 위한 통합 관리 서비스
    /// - LineDrawingManager를 내부적으로 사용
    /// - DB 연동 및 최종 마커 생성 담당
    /// - MapViewModel과 연결하는 브리지 역할
    /// </summary>
    public class LineRegistrationService : IDisposable
    {
        #region Fields

        private readonly ILogService _log;
        private readonly LineDrawingManager _drawingManager;
        private readonly MarkerFactory _markerFactory;
        private GMapCustomControl _mapControl;
        private bool _disposed = false;

        #endregion

        #region Events

        /// <summary>
        /// 라인 심볼 등록 완료 이벤트
        /// </summary>
        public event EventHandler<LineSymbolRegisteredEventArgs> LineSymbolRegistered;

        /// <summary>
        /// 라인 드로잉 상태 변경 이벤트 (DrawingManager에서 전파)
        /// </summary>
        public event EventHandler<LineDrawingEventArgs> DrawingStateChanged;

        /// <summary>
        /// 등록 실패 이벤트
        /// </summary>
        public event EventHandler<LineRegistrationFailedEventArgs> RegistrationFailed;

        #endregion

        #region Constructor

        /// <summary>
        /// LineRegistrationService 생성자
        /// </summary>
        /// <param name="mapControl">지도 컨트롤</param>
        /// <param name="markerFactory">마커 팩토리</param>
        /// <param name="dbService">DB 서비스</param>
        /// <param name="log">로깅 서비스</param>
        public LineRegistrationService(
            GMapCustomControl mapControl,
            MarkerFactory markerFactory,
            ILogService log = null)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            _markerFactory = markerFactory ?? throw new ArgumentNullException(nameof(markerFactory));
            _log = log;

            // LineDrawingManager 초기화
            _drawingManager = new LineDrawingManager(mapControl, log);

            // 이벤트 구독
            SubscribeToDrawingManagerEvents();

            _log?.Info("LineRegistrationService 초기화 완료");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 라인 심볼 등록 시작
        /// </summary>
        /// <param name="lineTitle">라인 제목</param>
        /// <param name="linePattern">라인 패턴</param>
        /// <param name="lineOpacity">라인 투명도</param>
        /// <param name="strokeColor">선 색상</param>
        /// <param name="strokeThickness">선 두께</param>
        /// <returns>시작 성공 여부</returns>
        public async Task<bool> StartLineRegistrationAsync(
            string lineTitle = "New Line",
            EnumLinePattern linePattern = EnumLinePattern.Solid,
            double lineOpacity = 1.0,
            EnumColorType strokeColor = EnumColorType.Red,
            double strokeThickness = 2.0)
        {
            try
            {
                _log?.Info($"라인 심볼 등록 시작: {lineTitle}");

                // DrawingManager를 통해 드로잉 시작
                bool success = _drawingManager.StartDrawing(lineTitle, linePattern, lineOpacity, strokeColor, strokeThickness);

                if (success)
                {
                    _log?.Info($"라인 드로잉 모드 활성화: {lineTitle}");
                }
                else
                {
                    _log?.Error("라인 드로잉 시작 실패");
                }

                await Task.Delay(1); // 비동기 패턴 유지
                return success;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 심볼 등록 시작 실패: {ex.Message}");
                RegistrationFailed?.Invoke(this, new LineRegistrationFailedEventArgs(lineTitle, ex));
                return false;
            }
        }

        /// <summary>
        /// 현재 라인 등록 취소
        /// </summary>
        /// <returns>취소 성공 여부</returns>
        public async Task<bool> CancelLineRegistrationAsync()
        {
            try
            {
                _log?.Info("라인 등록 취소 시작");

                bool success = _drawingManager.CancelDrawing();

                _log?.Info($"라인 등록 취소 완료: {success}");

                await Task.Delay(1); // 비동기 패턴 유지
                return success;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 등록 취소 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 현재 라인 등록 완료 (강제)
        /// </summary>
        /// <returns>완료 성공 여부</returns>
        public Task<bool> CompleteLineRegistrationAsync()
        {
            try
            {
                _log?.Info("라인 등록 강제 완료 시작");

                // 최소 포인트 수 확인
                if (_drawingManager.ConfirmedPointCount < 2)
                {
                    _log?.Warning("라인 완료 실패: 최소 2개 포인트 필요");
                    return Task.FromResult(false);
                }

                // 드로잉 완료
                bool success = _drawingManager.CompleteDrawing();

                if (success)
                {
                    _log?.Info("라인 등록 강제 완료 성공");
                }

                return Task.FromResult(success);
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 등록 강제 완료 실패: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 마지막 포인트 제거 (Undo)
        /// </summary>
        /// <returns>제거 성공 여부</returns>
        public bool UndoLastPoint()
        {
            try
            {
                return _drawingManager.UndoLastPoint();
            }
            catch (Exception ex)
            {
                _log?.Error($"마지막 포인트 제거 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// DrawingManager 이벤트 구독
        /// </summary>
        private void SubscribeToDrawingManagerEvents()
        {
            _drawingManager.DrawingStateChanged += OnDrawingStateChanged;
            _drawingManager.PointAdded += OnPointAdded;
            _drawingManager.DrawingCompleted += OnDrawingCompleted;
            _drawingManager.DrawingCancelled += OnDrawingCancelled;
        }

        /// <summary>
        /// DrawingManager 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromDrawingManagerEvents()
        {
            _drawingManager.DrawingStateChanged -= OnDrawingStateChanged;
            _drawingManager.PointAdded -= OnPointAdded;
            _drawingManager.DrawingCompleted -= OnDrawingCompleted;
            _drawingManager.DrawingCancelled -= OnDrawingCancelled;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 드로잉 상태 변경 이벤트 핸들러
        /// </summary>
        private void OnDrawingStateChanged(object? sender, LineDrawingEventArgs e)
        {
            try
            {
                _log?.Info($"드로잉 상태 변경: {e.State}");

                // 외부로 이벤트 전파
                DrawingStateChanged?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _log?.Error($"드로잉 상태 변경 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 포인트 추가 이벤트 핸들러
        /// </summary>
        private void OnPointAdded(object? sender, LineDrawingEventArgs e)
        {
            try
            {
                _log?.Info($"라인 포인트 추가: {e.Points.Count}개 포인트, 총 거리: {e.LineMarker.TotalDistance:F1}m");
            }
            catch (Exception ex)
            {
                _log?.Error($"포인트 추가 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 드로잉 완료 이벤트 핸들러
        /// </summary>
        private async void OnDrawingCompleted(object? sender, LineDrawingEventArgs e)
        {
            try
            {
                _log?.Info($"라인 드로잉 완료: {e.LineMarker.Title}");
                _log?.Info($"최종 포인트 수: {e.Points.Count}, 총 거리: {e.LineMarker.TotalDistance:F1}m");

                if (e.LineMarker is GMapLineMarker lineMarker)
                {
                    var lineModel = lineMarker.Model;

                    //// DB에 저장 (옵션)
                    //if (_dbService != null)
                    //{
                    //    try
                    //    {
                    //        var symbolId = await _dbService.InsertLineSymbolAsync(lineModel);
                    //        var savedModel = await _dbService.FetchLineSymbolAsync(symbolId);
                    //        if (savedModel != null)
                    //        {
                    //            lineModel = savedModel;
                    //            _log?.Info($"라인 심볼 DB 저장 완료: ID={symbolId}");
                    //        }
                    //    }
                    //    catch (Exception dbEx)
                    //    {
                    //        _log?.Warning($"DB 저장 실패, 메모리에서만 사용: {dbEx.Message}");
                    //    }
                    //}

                    // 최종 마커가 이미 지도에 있으므로 등록 완료 이벤트만 발생
                    LineSymbolRegistered?.Invoke(this, new LineSymbolRegisteredEventArgs(lineMarker, lineModel));
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 완료 처리 실패: {ex.Message}");
                RegistrationFailed?.Invoke(this, new LineRegistrationFailedEventArgs(e.LineMarker?.Title ?? "Unknown", ex));
            }
        }

        /// <summary>
        /// 드로잉 취소 이벤트 핸들러
        /// </summary>
        private void OnDrawingCancelled(object? sender, LineDrawingEventArgs e)
        {
            try
            {
                _log?.Info($"라인 드로잉 취소됨: {e.LineMarker?.Title ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                _log?.Error($"드로잉 취소 처리 실패: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 리소스 해제 (보호된 메서드)
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // 진행 중인 등록 취소
                    if (IsDrawing)
                    {
                        _ = CancelLineRegistrationAsync(); // 비동기 호출은 완료를 기다리지 않음
                    }

                    // 이벤트 구독 해제
                    UnsubscribeFromDrawingManagerEvents();

                    // DrawingManager 해제
                    _drawingManager?.Dispose();

                    _log?.Info("LineRegistrationService 리소스 해제 완료");
                }
                catch (Exception ex)
                {
                    _log?.Error($"LineRegistrationService 해제 중 오류: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// 소멸자
        /// </summary>
        ~LineRegistrationService()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// 현재 드로잉 중인지 여부
        /// </summary>
        public bool IsDrawing => _drawingManager?.IsDrawing ?? false;

        /// <summary>
        /// 현재 드로잉 상태
        /// </summary>
        public LineDrawingState CurrentState => _drawingManager?.CurrentState ?? LineDrawingState.None;

        /// <summary>
        /// 확정된 포인트 수
        /// </summary>
        public int ConfirmedPointCount => _drawingManager?.ConfirmedPointCount ?? 0;

        /// <summary>
        /// 현재 등록 중인 라인 제목
        /// </summary>
        public string CurrentLineTitle => _drawingManager?.CurrentLineMarker?.Title ?? "";

        /// <summary>
        /// 현재 드로잉 중인 라인의 총 거리 (미터)
        /// </summary>
        public double CurrentTotalDistance => _drawingManager?.CurrentTotalDistance ?? 0.0;

        #endregion
    }
}
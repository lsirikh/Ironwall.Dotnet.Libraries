using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
  Purpose      : 마커 Adorner들의 생명주기를 관리하는 핵심 서비스                                                          
  Created By   : GHLee                                                
  Created On   : 8/12/2025 4:12:08 PM                                                    
  Department   : SW Team                                                   
  Company      : Sensorway Co., Ltd.                                       
  Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 마커 Adorner들의 생명주기를 관리하는 핵심 서비스
/// - 선택 상태에 따른 자동 Adorner 관리
/// - 단일/다중 선택 모드 지원  
/// - 자동 메모리 정리 및 성능 최적화
/// </summary>
public class AdornerManagerService : IDisposable
{
    #region Fields

    private readonly ILogService _log;
    private GMapControl _mapControl;
    private readonly Dictionary<GMapControl, MarkerAdornerLayer> _adornerLayers;
    private readonly object _lock = new object();
    private bool _disposed = false;

    // 설정
    private bool _multiSelectEnabled = false;
    private bool _autoCleanupEnabled = true;
    private TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private System.Threading.Timer _cleanupTimer;

    #endregion

    #region Events

    /// <summary>
    /// 마커 선택 변경 이벤트
    /// </summary>
    public event EventHandler<MarkerSelectionChangedEventArgs> MarkerSelectionChanged;

    /// <summary>
    /// 편집 시작 이벤트
    /// </summary>
    public event EventHandler<MarkerEditStartedEventArgs> MarkerEditStarted;

    /// <summary>
    /// 편집 완료 이벤트
    /// </summary>
    public event EventHandler<MarkerEditCompletedEventArgs> MarkerEditCompleted;

    /// <summary>
    /// 편집 취소 이벤트
    /// </summary>
    public event EventHandler<MarkerEditCancelledEventArgs> MarkerEditCancelled;

    /// <summary>
    /// Adorner 생성 이벤트
    /// </summary>
    public event EventHandler<AdornerLifecycleEventArgs> AdornerCreated;

    /// <summary>
    /// Adorner 제거 이벤트
    /// </summary>
    public event EventHandler<AdornerLifecycleEventArgs> AdornerRemoved;

    #endregion

    #region Constructor

    /// <summary>
    /// AdornerManagerService 생성자
    /// </summary>
    /// <param name="log">로깅 서비스</param>
    public AdornerManagerService(ILogService log = null)
    {
        _log = log;
        _adornerLayers = new Dictionary<GMapControl, MarkerAdornerLayer>();

        // 자동 정리 타이머 시작
        if (_autoCleanupEnabled)
        {
            StartCleanupTimer();
        }

        _log?.Info("AdornerManagerService 초기화 완료");
    }

    /// <summary>
    /// 특정 지도 컨트롤과 함께 생성자
    /// </summary>
    /// <param name="mapControl">지도 컨트롤</param>
    /// <param name="log">로깅 서비스</param>
    public AdornerManagerService(GMapControl mapControl, ILogService log = null) : this(log)
    {
        if (mapControl != null)
        {
            RegisterMapControl(mapControl);
        }
    }

    #endregion

    #region Public Methods - Map Control Registration

    /// <summary>
    /// 지도 컨트롤 등록
    /// </summary>
    /// <param name="mapControl">등록할 지도 컨트롤</param>
    /// <returns>등록 성공 여부</returns>
    public bool RegisterMapControl(GMapControl mapControl)
    {
        if (mapControl == null) return false;

        lock (_lock)
        {
            try
            {
                if (_adornerLayers.ContainsKey(mapControl))
                {
                    _log?.Warning($"지도 컨트롤이 이미 등록됨: {mapControl.Name}");
                    return true;
                }

                var adornerLayer = new MarkerAdornerLayer(mapControl, _log);

                // 이벤트 구독
                SubscribeToAdornerLayerEvents(adornerLayer);

                _adornerLayers[mapControl] = adornerLayer;
                _mapControl = mapControl; // 주 컨트롤로 설정

                _log?.Info($"지도 컨트롤 등록 완료: {mapControl.Name ?? "Unnamed"}");
                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"지도 컨트롤 등록 실패: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 지도 컨트롤 등록 해제
    /// </summary>
    /// <param name="mapControl">등록 해제할 지도 컨트롤</param>
    /// <returns>해제 성공 여부</returns>
    public bool UnregisterMapControl(GMapControl mapControl)
    {
        if (mapControl == null) return false;

        lock (_lock)
        {
            try
            {
                if (!_adornerLayers.TryGetValue(mapControl, out var adornerLayer))
                {
                    return false;
                }

                // 이벤트 구독 해제
                UnsubscribeFromAdornerLayerEvents(adornerLayer);

                // Adorner 레이어 제거
                adornerLayer.Dispose();
                _adornerLayers.Remove(mapControl);

                _log?.Info($"지도 컨트롤 등록 해제 완료: {mapControl.Name ?? "Unnamed"}");
                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"지도 컨트롤 등록 해제 실패: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region Public Methods - Marker Selection Management

    /// <summary>
    /// 마커 선택 (Adorner 자동 관리)
    /// </summary>
    /// <param name="marker">선택할 마커</param>
    /// <param name="markerControl">마커 UI 컨트롤</param>
    /// <param name="mapControl">지도 컨트롤 (null이면 기본 사용)</param>
    /// <returns>성공 여부</returns>
    public bool SelectMarker(GMapCustomMarker marker, GMapMarkerBasicCustomControl markerControl, GMapControl mapControl = null)
    {
        if (marker == null || markerControl == null) return false;

        var targetMapControl = mapControl ?? _mapControl;
        if (targetMapControl == null) return false;

        lock (_lock)
        {
            try
            {
                var adornerLayer = GetAdornerLayer(targetMapControl);
                if (adornerLayer == null) return false;

                // 다중 선택이 비활성화된 경우 다른 마커들 선택 해제
                if (!_multiSelectEnabled)
                {
                    DeselectAllExcept(marker, targetMapControl);
                }

                // 마커 선택 상태 변경
                marker.IsSelected = true;

                // Adorner 생성
                var adorner = adornerLayer.CreateAdorner(marker, markerControl);
                if (adorner != null)
                {
                    _log?.Info($"마커 선택 및 Adorner 생성: {marker.Title}");

                    // 이벤트 발생
                    MarkerSelectionChanged?.Invoke(this, new MarkerSelectionChangedEventArgs(markerControl, true));

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _log?.Error($"마커 선택 실패: {marker?.Title}, {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 마커 선택 해제 (Adorner 자동 제거)
    /// </summary>
    /// <param name="marker">선택 해제할 마커</param>
    /// <param name="mapControl">지도 컨트롤 (null이면 기본 사용)</param>
    /// <returns>성공 여부</returns>
    public bool DeselectMarker(GMapCustomMarker marker, GMapControl mapControl = null)
    {
        if (marker == null) return false;

        var targetMapControl = mapControl ?? _mapControl;
        if (targetMapControl == null) return false;

        lock (_lock)
        {
            try
            {
                var adornerLayer = GetAdornerLayer(targetMapControl);
                if (adornerLayer == null) return false;

                // 마커 선택 상태 변경
                marker.IsSelected = false;

                // Adorner 제거
                if (adornerLayer.RemoveAdorner(marker))
                {
                    _log?.Info($"마커 선택 해제 및 Adorner 제거: {marker.Title}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _log?.Error($"마커 선택 해제 실패: {marker?.Title}, {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 모든 마커 선택 해제
    /// </summary>
    /// <param name="mapControl">지도 컨트롤 (null이면 모든 컨트롤)</param>
    public void DeselectAllMarkers(GMapControl mapControl = null)
    {
        lock (_lock)
        {
            try
            {
                if (mapControl != null)
                {
                    // 특정 지도 컨트롤의 마커들만 선택 해제
                    var adornerLayer = GetAdornerLayer(mapControl);
                    adornerLayer?.RemoveAllAdorners();
                }
                else
                {
                    // 모든 지도 컨트롤의 마커들 선택 해제
                    foreach (var kvp in _adornerLayers.ToList())
                    {
                        kvp.Value.RemoveAllAdorners();
                    }
                }

                _log?.Info("모든 마커 선택 해제 완료");
            }
            catch (Exception ex)
            {
                _log?.Error($"모든 마커 선택 해제 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 특정 마커 외 모든 마커 선택 해제 (단일 선택 모드)
    /// </summary>
    /// <param name="keepMarker">유지할 마커</param>
    /// <param name="mapControl">지도 컨트롤</param>
    private void DeselectAllExcept(GMapCustomMarker keepMarker, GMapControl mapControl)
    {
        var adornerLayer = GetAdornerLayer(mapControl);
        adornerLayer?.RemoveAllExcept(keepMarker);
    }

    #endregion

    #region Public Methods - Edit Management

    /// <summary>
    /// 모든 편집 취소
    /// </summary>
    /// <param name="mapControl">지도 컨트롤 (null이면 모든 컨트롤)</param>
    public void CancelAllEditing(GMapControl mapControl = null)
    {
        lock (_lock)
        {
            try
            {
                if (mapControl != null)
                {
                    var adornerLayer = GetAdornerLayer(mapControl);
                    adornerLayer?.CancelAllEditing();
                }
                else
                {
                    foreach (var adornerLayer in _adornerLayers.Values)
                    {
                        adornerLayer.CancelAllEditing();
                    }
                }

                _log?.Info("모든 편집 취소 완료");
            }
            catch (Exception ex)
            {
                _log?.Error($"모든 편집 취소 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 편집 중인 마커 목록 반환
    /// </summary>
    /// <param name="mapControl">지도 컨트롤 (null이면 모든 컨트롤)</param>
    /// <returns>편집 중인 마커 목록</returns>
    public List<GMapCustomMarker> GetEditingMarkers(GMapControl mapControl = null)
    {
        lock (_lock)
        {
            var editingMarkers = new List<GMapCustomMarker>();

            try
            {
                if (mapControl != null)
                {
                    var adornerLayer = GetAdornerLayer(mapControl);
                    if (adornerLayer != null)
                    {
                        editingMarkers.AddRange(adornerLayer.GetEditingMarkers());
                    }
                }
                else
                {
                    foreach (var adornerLayer in _adornerLayers.Values)
                    {
                        editingMarkers.AddRange(adornerLayer.GetEditingMarkers());
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"편집 중인 마커 조회 실패: {ex.Message}");
            }

            return editingMarkers;
        }
    }

    #endregion

    #region Public Methods - Configuration

    /// <summary>
    /// 다중 선택 모드 설정
    /// </summary>
    /// <param name="enabled">다중 선택 활성화 여부</param>
    public void SetMultiSelectMode(bool enabled)
    {
        if (_multiSelectEnabled == enabled) return;

        _multiSelectEnabled = enabled;
        _log?.Info($"다중 선택 모드: {(enabled ? "활성화" : "비활성화")}");

        // 단일 선택 모드로 전환 시 현재 선택된 마커들 중 첫 번째만 유지
        if (!enabled)
        {
            lock (_lock)
            {
                foreach (var adornerLayer in _adornerLayers.Values)
                {
                    var editingMarkers = adornerLayer.GetEditingMarkers();
                    if (editingMarkers.Count > 1)
                    {
                        var keepMarker = editingMarkers.First();
                        adornerLayer.RemoveAllExcept(keepMarker);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 자동 정리 기능 설정
    /// </summary>
    /// <param name="enabled">자동 정리 활성화 여부</param>
    /// <param name="interval">정리 간격 (null이면 기본값 사용)</param>
    public void SetAutoCleanup(bool enabled, TimeSpan? interval = null)
    {
        _autoCleanupEnabled = enabled;

        if (interval.HasValue)
        {
            _cleanupInterval = interval.Value;
        }

        if (enabled)
        {
            StartCleanupTimer();
        }
        else
        {
            StopCleanupTimer();
        }

        _log?.Info($"자동 정리: {(enabled ? "활성화" : "비활성화")}, 간격: {_cleanupInterval.TotalMinutes}분");
    }

    #endregion

    #region Public Methods - Statistics

    /// <summary>
    /// 관리 통계 반환
    /// </summary>
    /// <returns>AdornerManager 통계</returns>
    public AdornerManagerStatistics GetStatistics()
    {
        lock (_lock)
        {
            var stats = new AdornerManagerStatistics
            {
                RegisteredMapControls = _adornerLayers.Count,
                TotalActiveAdorners = 0,
                TotalEditingAdorners = 0,
                MultiSelectEnabled = _multiSelectEnabled,
                AutoCleanupEnabled = _autoCleanupEnabled
            };

            foreach (var adornerLayer in _adornerLayers.Values)
            {
                var layerStats = adornerLayer.GetStatistics();
                stats.TotalActiveAdorners += layerStats.TotalAdorners;
                stats.TotalEditingAdorners += layerStats.EditingAdorners;
                stats.TotalEditTimeSeconds += layerStats.TotalEditTimeSeconds;
            }

            stats.AverageEditTimeSeconds = stats.TotalEditingAdorners > 0
                ? stats.TotalEditTimeSeconds / stats.TotalEditingAdorners
                : 0;

            return stats;
        }
    }

    /// <summary>
    /// 통계 정보 로그 출력
    /// </summary>
    public string LogStatistics()
    {
        var stats = GetStatistics();
        _log?.Info($"AdornerManager 통계: {stats}");
        return stats.ToString();
    }

    #endregion

    #region Public Methods - Memory Management

    /// <summary>
    /// 메모리 정리
    /// </summary>
    public void TrimMemory()
    {
        lock (_lock)
        {
            try
            {
                foreach (var adornerLayer in _adornerLayers.Values)
                {
                    adornerLayer.TrimMemory();
                }

                _log?.Info("AdornerManager 메모리 정리 완료");
            }
            catch (Exception ex)
            {
                _log?.Error($"메모리 정리 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 수동 정리 실행
    /// </summary>
    public void ManualCleanup()
    {
        TrimMemory();
        GC.Collect(0, GCCollectionMode.Optimized);
        _log?.Info("수동 정리 완료");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Adorner 레이어 반환
    /// </summary>
    /// <param name="mapControl">지도 컨트롤</param>
    /// <returns>Adorner 레이어 (없으면 null)</returns>
    private MarkerAdornerLayer GetAdornerLayer(GMapControl mapControl)
    {
        _adornerLayers.TryGetValue(mapControl, out var adornerLayer);
        return adornerLayer;
    }

    /// <summary>
    /// Adorner 레이어 이벤트 구독
    /// </summary>
    /// <param name="adornerLayer">Adorner 레이어</param>
    private void SubscribeToAdornerLayerEvents(MarkerAdornerLayer adornerLayer)
    {
        adornerLayer.AdornerCreated += OnAdornerCreated;
        adornerLayer.AdornerRemoved += OnAdornerRemoved;
        adornerLayer.EditStarted += OnEditStarted;
        adornerLayer.EditCompleted += OnEditCompleted;
        adornerLayer.EditCancelled += OnEditCancelled;
    }

    /// <summary>
    /// Adorner 레이어 이벤트 구독 해제
    /// </summary>
    /// <param name="adornerLayer">Adorner 레이어</param>
    private void UnsubscribeFromAdornerLayerEvents(MarkerAdornerLayer adornerLayer)
    {
        adornerLayer.AdornerCreated -= OnAdornerCreated;
        adornerLayer.AdornerRemoved -= OnAdornerRemoved;
        adornerLayer.EditStarted -= OnEditStarted;
        adornerLayer.EditCompleted -= OnEditCompleted;
        adornerLayer.EditCancelled -= OnEditCancelled;
    }

    /// <summary>
    /// 자동 정리 타이머 시작
    /// </summary>
    private void StartCleanupTimer()
    {
        StopCleanupTimer(); // 기존 타이머 정지

        _cleanupTimer = new System.Threading.Timer(
            callback: _ => TrimMemory(),
            state: null,
            dueTime: _cleanupInterval,
            period: _cleanupInterval);

        _log?.Info($"자동 정리 타이머 시작: {_cleanupInterval.TotalMinutes}분 간격");
    }

    /// <summary>
    /// 자동 정리 타이머 정지
    /// </summary>
    private void StopCleanupTimer()
    {
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Adorner 생성 이벤트 핸들러
    /// </summary>
    private void OnAdornerCreated(object sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 생성됨: {e.Marker.Title}");
        AdornerCreated?.Invoke(this, e);
    }

    /// <summary>
    /// Adorner 제거 이벤트 핸들러
    /// </summary>
    private void OnAdornerRemoved(object sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 제거됨: {e.Marker.Title}");
        AdornerRemoved?.Invoke(this, e);
    }

    /// <summary>
    /// 편집 시작 이벤트 핸들러
    /// </summary>
    private void OnEditStarted(object sender, MarkerEditStartedEventArgs e)
    {
        _log?.Info($"편집 시작: {e.Marker.Title}, 핸들: {e.Handle}");
        MarkerEditStarted?.Invoke(this, e);
    }

    /// <summary>
    /// 편집 완료 이벤트 핸들러
    /// </summary>
    private void OnEditCompleted(object sender, MarkerEditCompletedEventArgs e)
    {
        _log?.Info($"편집 완료: {e.Marker.Title}, 변경: {e.GetChangesSummary()}");
        MarkerEditCompleted?.Invoke(this, e);
    }

    /// <summary>
    /// 편집 취소 이벤트 핸들러
    /// </summary>
    private void OnEditCancelled(object sender, MarkerEditCancelledEventArgs e)
    {
        _log?.Info($"편집 취소: {e.Marker.Title}, 이유: {e.Reason}");
        MarkerEditCancelled?.Invoke(this, e);
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
                // 타이머 정지
                StopCleanupTimer();

                // 모든 Adorner 레이어 정리
                lock (_lock)
                {
                    foreach (var kvp in _adornerLayers.ToList())
                    {
                        UnsubscribeFromAdornerLayerEvents(kvp.Value);
                        kvp.Value.Dispose();
                    }
                    _adornerLayers.Clear();
                }

                _log?.Info("AdornerManagerService 리소스 해제 완료");
            }
            catch (Exception ex)
            {
                _log?.Error($"AdornerManagerService 해제 중 오류: {ex.Message}");
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
    ~AdornerManagerService()
    {
        Dispose(false);
    }

    #endregion

    #region Properties

    /// <summary>
    /// 다중 선택 모드 활성화 여부
    /// </summary>
    public bool MultiSelectEnabled => _multiSelectEnabled;

    /// <summary>
    /// 자동 정리 활성화 여부
    /// </summary>
    public bool AutoCleanupEnabled => _autoCleanupEnabled;

    /// <summary>
    /// 정리 간격
    /// </summary>
    public TimeSpan CleanupInterval => _cleanupInterval;

    /// <summary>
    /// 등록된 지도 컨트롤 개수
    /// </summary>
    public int RegisteredMapControlCount
    {
        get
        {
            lock (_lock)
            {
                return _adornerLayers.Count;
            }
        }
    }

    /// <summary>
    /// 총 활성 Adorner 개수
    /// </summary>
    public int TotalActiveAdornerCount
    {
        get
        {
            lock (_lock)
            {
                return _adornerLayers.Values.Sum(layer => layer.ActiveAdornerCount);
            }
        }
    }

    /// <summary>
    /// 편집 중인 Adorner 개수
    /// </summary>
    public int EditingAdornerCount
    {
        get
        {
            lock (_lock)
            {
                return _adornerLayers.Values.Sum(layer => layer.EditingAdornerCount);
            }
        }
    }

    #endregion
}

/// <summary>
/// AdornerManager 통계 정보
/// </summary>
public class AdornerManagerStatistics
{
    /// <summary>
    /// 등록된 지도 컨트롤 수
    /// </summary>
    public int RegisteredMapControls { get; set; }

    /// <summary>
    /// 총 활성 Adorner 수
    /// </summary>
    public int TotalActiveAdorners { get; set; }

    /// <summary>
    /// 편집 중인 Adorner 수
    /// </summary>
    public int TotalEditingAdorners { get; set; }

    /// <summary>
    /// 다중 선택 활성화 여부
    /// </summary>
    public bool MultiSelectEnabled { get; set; }

    /// <summary>
    /// 자동 정리 활성화 여부
    /// </summary>
    public bool AutoCleanupEnabled { get; set; }

    /// <summary>
    /// 총 편집 시간 (초)
    /// </summary>
    public double TotalEditTimeSeconds { get; set; }

    /// <summary>
    /// 평균 편집 시간 (초)
    /// </summary>
    public double AverageEditTimeSeconds { get; set; }

    /// <summary>
    /// 통계 문자열 표현
    /// </summary>
    public override string ToString()
    {
        return $"MapControls: {RegisteredMapControls}, " +
               $"Adorners: {TotalActiveAdorners} (편집중: {TotalEditingAdorners}), " +
               $"다중선택: {MultiSelectEnabled}, 자동정리: {AutoCleanupEnabled}, " +
               $"평균편집시간: {AverageEditTimeSeconds:F1}초";
    }
}
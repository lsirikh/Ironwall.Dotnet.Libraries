using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 마커 Adorner들을 관리하는 레이어 클래스                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 마커 Adorner들을 관리하는 레이어
/// - Adorner 생성, 제거, 활성화 관리
/// - 메모리 누수 방지
/// - 편집 상태 추적
/// </summary>
public class MarkerAdornerLayer : IDisposable
{
    #region Fields

    private readonly ILogService _log;
    private readonly GMapControl _mapControl;
    private readonly Dictionary<GMapCustomMarker, MarkerEditAdorner> _activeAdorners;
    private readonly object _lock = new object();
    private bool _disposed = false;

    #endregion

    #region Events

    /// <summary>
    /// Adorner 생성 이벤트
    /// </summary>
    public event EventHandler<AdornerLifecycleEventArgs> AdornerCreated;

    /// <summary>
    /// Adorner 제거 이벤트
    /// </summary>
    public event EventHandler<AdornerLifecycleEventArgs> AdornerRemoved;

    /// <summary>
    /// 편집 시작 이벤트
    /// </summary>
    public event EventHandler<MarkerEditStartedEventArgs> EditStarted;

    /// <summary>
    /// 편집 완료 이벤트
    /// </summary>
    public event EventHandler<MarkerEditCompletedEventArgs> EditCompleted;

    /// <summary>
    /// 편집 취소 이벤트
    /// </summary>
    public event EventHandler<MarkerEditCancelledEventArgs> EditCancelled;

    #endregion

    #region Constructor

    /// <summary>
    /// MarkerAdornerLayer 생성자
    /// </summary>
    /// <param name="mapControl">지도 컨트롤</param>
    /// <param name="log">로깅 서비스</param>
    public MarkerAdornerLayer(GMapControl mapControl, ILogService log = null)
    {
        _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
        _log = log;
        _activeAdorners = new Dictionary<GMapCustomMarker, MarkerEditAdorner>();

        _log?.Info("MarkerAdornerLayer 초기화 완료");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 마커에 대한 편집 Adorner 생성 및 활성화
    /// </summary>
    /// <param name="marker">대상 마커</param>
    /// <param name="markerControl">마커 UI 컨트롤</param>
    /// <returns>생성된 Adorner (이미 존재하면 기존 것 반환)</returns>
    public MarkerEditAdorner CreateAdorner(GMapCustomMarker marker, GMapMarkerBasicCustomControl markerControl)
    {
        if (marker == null) throw new ArgumentNullException(nameof(marker));
        if (markerControl == null) throw new ArgumentNullException(nameof(markerControl));

        lock (_lock)
        {
            try
            {
                // 이미 존재하는 Adorner가 있는지 확인
                if (_activeAdorners.TryGetValue(marker, out var existingAdorner))
                {
                    _log?.Info($"기존 Adorner 반환: {marker.Title}");
                    return existingAdorner;
                }

                // 새 Adorner 생성
                var adorner = new MarkerEditAdorner(markerControl, marker, _mapControl, _log);

                // 이벤트 구독
                SubscribeToAdornerEvents(adorner);

                // AdornerLayer에 추가
                var adornerLayer = AdornerLayer.GetAdornerLayer(markerControl);
                if (adornerLayer != null)
                {
                    adornerLayer.Add(adorner);
                    _activeAdorners[marker] = adorner;

                    _log?.Info($"Adorner 생성 및 추가: {marker.Title}");

                    // 이벤트 발생
                    AdornerCreated?.Invoke(this, new AdornerLifecycleEventArgs(marker, AdornerLifecycleEventType.Created));

                    return adorner;
                }
                else
                {
                    _log?.Warning($"AdornerLayer를 찾을 수 없음: {marker.Title}");
                    adorner.Dispose();
                    return null;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"Adorner 생성 실패: {marker.Title}, 오류: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 특정 마커의 Adorner 제거
    /// </summary>
    /// <param name="marker">대상 마커</param>
    /// <returns>제거 성공 여부</returns>
    public bool RemoveAdorner(GMapCustomMarker marker)
    {
        if (marker == null) return false;

        lock (_lock)
        {
            try
            {
                if (!_activeAdorners.TryGetValue(marker, out var adorner))
                {
                    return false; // 이미 없음
                }

                return RemoveAdornerInternal(marker, adorner);
            }
            catch (Exception ex)
            {
                _log?.Error($"Adorner 제거 실패: {marker.Title}, 오류: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 모든 Adorner 제거
    /// </summary>
    public void RemoveAllAdorners()
    {
        lock (_lock)
        {
            try
            {
                var adorners = _activeAdorners.ToList();
                foreach (var kvp in adorners)
                {
                    RemoveAdornerInternal(kvp.Key, kvp.Value);
                }

                _log?.Info($"모든 Adorner 제거 완료: {adorners.Count}개");
            }
            catch (Exception ex)
            {
                _log?.Error($"모든 Adorner 제거 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 특정 마커의 Adorner 반환
    /// </summary>
    /// <param name="marker">대상 마커</param>
    /// <returns>Adorner (없으면 null)</returns>
    public MarkerEditAdorner GetAdorner(GMapCustomMarker marker)
    {
        if (marker == null) return null;

        lock (_lock)
        {
            _activeAdorners.TryGetValue(marker, out var adorner);
            return adorner;
        }
    }

    /// <summary>
    /// 특정 마커에 Adorner가 있는지 확인
    /// </summary>
    /// <param name="marker">대상 마커</param>
    /// <returns>Adorner 존재 여부</returns>
    public bool HasAdorner(GMapCustomMarker marker)
    {
        if (marker == null) return false;

        lock (_lock)
        {
            return _activeAdorners.ContainsKey(marker);
        }
    }

    /// <summary>
    /// 현재 편집 중인 마커들 반환
    /// </summary>
    /// <returns>편집 중인 마커 목록</returns>
    public IList<GMapCustomMarker> GetEditingMarkers()
    {
        lock (_lock)
        {
            return _activeAdorners.Where(kvp => kvp.Value.EditState.IsEditing)
                                  .Select(kvp => kvp.Key)
                                  .ToList();
        }
    }

    /// <summary>
    /// 활성 Adorner 개수
    /// </summary>
    public int ActiveAdornerCount
    {
        get
        {
            lock (_lock)
            {
                return _activeAdorners.Count;
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
                return _activeAdorners.Count(kvp => kvp.Value.EditState.IsEditing);
            }
        }
    }

    /// <summary>
    /// 모든 편집 취소
    /// </summary>
    public void CancelAllEditing()
    {
        lock (_lock)
        {
            try
            {
                var editingAdorners = _activeAdorners.Where(kvp => kvp.Value.EditState.IsEditing).ToList();

                foreach (var kvp in editingAdorners)
                {
                    // Escape 키 이벤트 시뮬레이션을 통한 편집 취소
                    var keyEventArgs = new System.Windows.Input.KeyEventArgs(
                        System.Windows.Input.Keyboard.PrimaryDevice,
                        PresentationSource.FromVisual(kvp.Value),
                        0,
                        System.Windows.Input.Key.Escape);

                    kvp.Value.RaiseEvent(keyEventArgs);
                }

                _log?.Info($"모든 편집 취소 완료: {editingAdorners.Count}개");
            }
            catch (Exception ex)
            {
                _log?.Error($"모든 편집 취소 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 특정 마커 외 모든 Adorner 제거 (단일 선택 모드)
    /// </summary>
    /// <param name="keepMarker">유지할 마커</param>
    public void RemoveAllExcept(GMapCustomMarker keepMarker)
    {
        if (keepMarker == null)
        {
            RemoveAllAdorners();
            return;
        }

        lock (_lock)
        {
            try
            {
                var adorners = _activeAdorners.Where(kvp => kvp.Key != keepMarker).ToList();
                foreach (var kvp in adorners)
                {
                    RemoveAdornerInternal(kvp.Key, kvp.Value);
                }

                _log?.Info($"다른 Adorner 제거 완료: {adorners.Count}개, 유지: {keepMarker.Title}");
            }
            catch (Exception ex)
            {
                _log?.Error($"선택적 Adorner 제거 실패: {ex.Message}");
            }
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Adorner 제거 내부 로직
    /// </summary>
    private bool RemoveAdornerInternal(GMapCustomMarker marker, MarkerEditAdorner adorner)
    {
        try
        {
            // 이벤트 구독 해제
            UnsubscribeFromAdornerEvents(adorner);

            // AdornerLayer에서 제거
            var markerControl = adorner.AdornedElement as GMapMarkerBasicCustomControl;
            if (markerControl != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(markerControl);
                adornerLayer?.Remove(adorner);
            }

            // 딕셔너리에서 제거
            _activeAdorners.Remove(marker);

            // 리소스 정리
            if (adorner is IDisposable disposableAdorner)
            {
                disposableAdorner.Dispose();
            }

            _log?.Info($"Adorner 제거 완료: {marker.Title}");

            // 이벤트 발생
            AdornerRemoved?.Invoke(this, new AdornerLifecycleEventArgs(marker, AdornerLifecycleEventType.Removed));

            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 제거 중 오류: {marker.Title}, {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Adorner 이벤트 구독
    /// </summary>
    private void SubscribeToAdornerEvents(MarkerEditAdorner adorner)
    {
        adorner.EditStarted += OnAdornerEditStarted;
        adorner.EditCompleted += OnAdornerEditCompleted;
        adorner.EditCancelled += OnAdornerEditCancelled;
    }

    /// <summary>
    /// Adorner 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromAdornerEvents(MarkerEditAdorner adorner)
    {
        adorner.EditStarted -= OnAdornerEditStarted;
        adorner.EditCompleted -= OnAdornerEditCompleted;
        adorner.EditCancelled -= OnAdornerEditCancelled;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Adorner 편집 시작 이벤트 핸들러
    /// </summary>
    private void OnAdornerEditStarted(object sender, MarkerEditStartedEventArgs e)
    {
        _log?.Info($"마커 편집 시작: {e.Marker.Title}, 핸들: {e.Handle}");
        EditStarted?.Invoke(this, e);
    }

    /// <summary>
    /// Adorner 편집 완료 이벤트 핸들러
    /// </summary>
    private void OnAdornerEditCompleted(object sender, MarkerEditCompletedEventArgs e)
    {
        _log?.Info($"마커 편집 완료: {e.Marker.Title}, 변경사항: {e.GetChangesSummary()}");
        EditCompleted?.Invoke(this, e);
    }

    /// <summary>
    /// Adorner 편집 취소 이벤트 핸들러
    /// </summary>
    private void OnAdornerEditCancelled(object sender, MarkerEditCancelledEventArgs e)
    {
        _log?.Info($"마커 편집 취소: {e.Marker.Title}, 이유: {e.Reason}");
        EditCancelled?.Invoke(this, e);
    }

    #endregion

    #region Statistics and Diagnostics

    /// <summary>
    /// Adorner 통계 정보
    /// </summary>
    public AdornerLayerStatistics GetStatistics()
    {
        lock (_lock)
        {
            var editingCount = _activeAdorners.Count(kvp => kvp.Value.EditState.IsEditing);
            var totalEditTime = _activeAdorners.Values
                .Where(a => a.EditState.EditDurationSeconds > 0)
                .Sum(a => a.EditState.EditDurationSeconds);

            return new AdornerLayerStatistics
            {
                TotalAdorners = _activeAdorners.Count,
                EditingAdorners = editingCount,
                IdleAdorners = _activeAdorners.Count - editingCount,
                TotalEditTimeSeconds = totalEditTime,
                AverageEditTimeSeconds = editingCount > 0 ? totalEditTime / editingCount : 0
            };
        }
    }

    /// <summary>
    /// 메모리 정리 (가비지 컬렉션 힌트)
    /// </summary>
    public void TrimMemory()
    {
        lock (_lock)
        {
            try
            {
                // 비활성 Adorner들 정리
                var inactiveAdorners = _activeAdorners
                    .Where(kvp => !kvp.Value.EditState.IsEditing &&
                                  kvp.Value.EditState.EditDurationSeconds == 0)
                    .ToList();

                foreach (var kvp in inactiveAdorners)
                {
                    RemoveAdornerInternal(kvp.Key, kvp.Value);
                }

                if (inactiveAdorners.Count > 0)
                {
                    _log?.Info($"메모리 정리 완료: {inactiveAdorners.Count}개 비활성 Adorner 제거");
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"메모리 정리 실패: {ex.Message}");
            }
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
                // 모든 Adorner 제거
                RemoveAllAdorners();

                _log?.Info("MarkerAdornerLayer 리소스 해제 완료");
            }
            catch (Exception ex)
            {
                _log?.Error($"MarkerAdornerLayer 해제 중 오류: {ex.Message}");
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
    ~MarkerAdornerLayer()
    {
        Dispose(false);
    }

    #endregion
}

/// <summary>
/// Adorner 레이어 통계 정보
/// </summary>
public class AdornerLayerStatistics
{
    /// <summary>
    /// 총 Adorner 개수
    /// </summary>
    public int TotalAdorners { get; set; }

    /// <summary>
    /// 편집 중인 Adorner 개수
    /// </summary>
    public int EditingAdorners { get; set; }

    /// <summary>
    /// 대기 중인 Adorner 개수
    /// </summary>
    public int IdleAdorners { get; set; }

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
        return $"Adorners: {TotalAdorners} (편집중: {EditingAdorners}, 대기: {IdleAdorners}), " +
               $"평균 편집시간: {AverageEditTimeSeconds:F1}초";
    }
}
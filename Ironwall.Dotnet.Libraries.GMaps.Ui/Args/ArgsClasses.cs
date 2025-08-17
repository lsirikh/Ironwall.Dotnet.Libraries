using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025 9:24:04 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 마커 클릭 이벤트 인수
/// </summary>
public class MarkerClickEventArgs : EventArgs
{
    public GMapMarkerBasicCustomControl Marker { get; }
    public MouseButtonEventArgs MouseEventArgs { get; }

    public MarkerClickEventArgs(GMapMarkerBasicCustomControl marker, MouseButtonEventArgs mouseEventArgs)
    {
        Marker = marker;
        MouseEventArgs = mouseEventArgs;
    }
}

// 임계값 초과 이벤트 인수
public class ThresholdExceededEventArgs : EventArgs
{
    public double CurrentValue { get; }
    public double ThresholdValue { get; }
    public DateTime Timestamp { get; }

    public ThresholdExceededEventArgs(double currentValue, double thresholdValue)
    {
        CurrentValue = currentValue;
        ThresholdValue = thresholdValue;
        Timestamp = DateTime.Now;
    }
}



/// <summary>
/// 마커 편집 시작 이벤트 인수
/// </summary>
public class MarkerEditStartedEventArgs : EventArgs
{
    /// <summary>
    /// 편집 대상 마커
    /// </summary>
    public GMapCustomMarker Marker { get; }

    /// <summary>
    /// 시작된 편집 핸들
    /// </summary>
    public MarkerHandle Handle { get; }

    /// <summary>
    /// 편집 시작 시간
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// 편집 시작 시 마커 위치
    /// </summary>
    public PointLatLng InitialPosition { get; }

    /// <summary>
    /// 편집 시작 시 마커 크기
    /// </summary>
    public (double Width, double Height) InitialSize { get; }

    /// <summary>
    /// 편집 시작 시 마커 회전각
    /// </summary>
    public double InitialBearing { get; }

    public MarkerEditStartedEventArgs(GMapCustomMarker marker, MarkerHandle handle)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        Handle = handle;
        StartTime = DateTime.Now;
        InitialPosition = marker.Position;
        InitialSize = (marker.Width, marker.Height);
        InitialBearing = marker.Bearing;
    }
}

/// <summary>
/// 마커 편집 중 이벤트 인수 (실시간)
/// </summary>
public class MarkerEditingEventArgs : EventArgs
{
    /// <summary>
    /// 편집 중인 마커
    /// </summary>
    public GMapCustomMarker Marker { get; }

    /// <summary>
    /// 현재 편집 중인 핸들
    /// </summary>
    public MarkerHandle Handle { get; }

    /// <summary>
    /// X축 이동량
    /// </summary>
    public double DeltaX { get; }

    /// <summary>
    /// Y축 이동량
    /// </summary>
    public double DeltaY { get; }

    /// <summary>
    /// 현재 마커 위치
    /// </summary>
    public PointLatLng CurrentPosition { get; }

    /// <summary>
    /// 현재 마커 크기
    /// </summary>
    public (double Width, double Height) CurrentSize { get; }

    /// <summary>
    /// 현재 마커 회전각
    /// </summary>
    public double CurrentBearing { get; }

    /// <summary>
    /// 편집 이벤트 발생 시간
    /// </summary>
    public DateTime EventTime { get; }

    public MarkerEditingEventArgs(GMapCustomMarker marker, MarkerHandle handle, double deltaX, double deltaY)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        Handle = handle;
        DeltaX = deltaX;
        DeltaY = deltaY;
        CurrentPosition = marker.Position;
        CurrentSize = (marker.Width, marker.Height);
        CurrentBearing = marker.Bearing;
        EventTime = DateTime.Now;
    }
}

/// <summary>
/// 마커 편집 완료 이벤트 인수
/// </summary>
public class MarkerEditCompletedEventArgs : EventArgs
{
    /// <summary>
    /// 편집 완료된 마커
    /// </summary>
    public GMapCustomMarker Marker { get; }

    /// <summary>
    /// 편집 시작 시 위치
    /// </summary>
    public PointLatLng OriginalPosition { get; }

    /// <summary>
    /// 편집 시작 시 너비
    /// </summary>
    public double OriginalWidth { get; }

    /// <summary>
    /// 편집 시작 시 높이
    /// </summary>
    public double OriginalHeight { get; }

    /// <summary>
    /// 편집 시작 시 회전각
    /// </summary>
    public double OriginalBearing { get; }

    /// <summary>
    /// 편집 완료 후 위치
    /// </summary>
    public PointLatLng FinalPosition { get; }

    /// <summary>
    /// 편집 완료 후 크기
    /// </summary>
    public (double Width, double Height) FinalSize { get; }

    /// <summary>
    /// 편집 완료 후 회전각
    /// </summary>
    public double FinalBearing { get; }

    /// <summary>
    /// 편집 완료 시간
    /// </summary>
    public DateTime CompletionTime { get; }

    /// <summary>
    /// 위치가 변경되었는지 여부
    /// </summary>
    public bool PositionChanged => !OriginalPosition.Equals(FinalPosition);

    /// <summary>
    /// 크기가 변경되었는지 여부
    /// </summary>
    public bool SizeChanged => Math.Abs(OriginalWidth - FinalSize.Width) > 0.1 ||
                               Math.Abs(OriginalHeight - FinalSize.Height) > 0.1;

    /// <summary>
    /// 회전각이 변경되었는지 여부
    /// </summary>
    public bool BearingChanged => Math.Abs(OriginalBearing - FinalBearing) > 0.1;

    /// <summary>
    /// 어떤 변경이라도 있었는지 여부
    /// </summary>
    public bool HasChanges => PositionChanged || SizeChanged || BearingChanged;

    public MarkerEditCompletedEventArgs(GMapCustomMarker marker,
        PointLatLng originalPosition, double originalWidth, double originalHeight, double originalBearing)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        OriginalPosition = originalPosition;
        OriginalWidth = originalWidth;
        OriginalHeight = originalHeight;
        OriginalBearing = originalBearing;

        FinalPosition = marker.Position;
        FinalSize = (marker.Width, marker.Height);
        FinalBearing = marker.Bearing;
        CompletionTime = DateTime.Now;
    }

    /// <summary>
    /// 변경 사항 요약 문자열
    /// </summary>
    public string GetChangesSummary()
    {
        var changes = new List<string>();

        if (PositionChanged)
        {
            changes.Add($"위치: ({OriginalPosition.Lat:F6}, {OriginalPosition.Lng:F6}) → ({FinalPosition.Lat:F6}, {FinalPosition.Lng:F6})");
        }

        if (SizeChanged)
        {
            changes.Add($"크기: {OriginalWidth:F0}×{OriginalHeight:F0} → {FinalSize.Width:F0}×{FinalSize.Height:F0}");
        }

        if (BearingChanged)
        {
            changes.Add($"회전: {OriginalBearing:F0}° → {FinalBearing:F0}°");
        }

        return changes.Count > 0 ? string.Join(", ", changes) : "변경 없음";
    }
}

/// <summary>
/// 마커 편집 취소 이벤트 인수
/// </summary>
public class MarkerEditCancelledEventArgs : EventArgs
{
    /// <summary>
    /// 편집이 취소된 마커
    /// </summary>
    public GMapCustomMarker Marker { get; }

    /// <summary>
    /// 취소 시간
    /// </summary>
    public DateTime CancellationTime { get; }

    /// <summary>
    /// 취소 이유
    /// </summary>
    public string Reason { get; }

    public MarkerEditCancelledEventArgs(GMapCustomMarker marker, string reason = "사용자 취소")
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        Reason = reason ?? "알 수 없음";
        CancellationTime = DateTime.Now;
    }
}

/// <summary>
/// 마커 선택 변경 이벤트 인수 (기존과 호환성 유지)
/// </summary>
public class MarkerSelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// 선택 상태가 변경된 마커 컨트롤
    /// </summary>
    public GMapMarkerBasicCustomControl MarkerControl { get; }

    /// <summary>
    /// 새로운 선택 상태
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>
    /// 변경 시간
    /// </summary>
    public DateTime ChangeTime { get; }

    public MarkerSelectionChangedEventArgs(GMapMarkerBasicCustomControl markerControl, bool isSelected)
    {
        MarkerControl = markerControl ?? throw new ArgumentNullException(nameof(markerControl));
        IsSelected = isSelected;
        ChangeTime = DateTime.Now;
    }
}

/// <summary>
/// Adorner 생명주기 이벤트 인수
/// </summary>
public class AdornerLifecycleEventArgs : EventArgs
{
    /// <summary>
    /// 대상 마커
    /// </summary>
    public GMapCustomMarker Marker { get; }

    /// <summary>
    /// 이벤트 유형
    /// </summary>
    public AdornerLifecycleEventType EventType { get; }

    /// <summary>
    /// 이벤트 발생 시간
    /// </summary>
    public DateTime EventTime { get; }

    public AdornerLifecycleEventArgs(GMapCustomMarker marker, AdornerLifecycleEventType eventType)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        EventType = eventType;
        EventTime = DateTime.Now;
    }
}

/// <summary>
/// Adorner 생명주기 이벤트 유형
/// </summary>
public enum AdornerLifecycleEventType
{
    /// <summary>
    /// Adorner 생성됨
    /// </summary>
    Created,

    /// <summary>
    /// Adorner 활성화됨
    /// </summary>
    Activated,

    /// <summary>
    /// Adorner 비활성화됨
    /// </summary>
    Deactivated,

    /// <summary>
    /// Adorner 제거됨
    /// </summary>
    Removed
}
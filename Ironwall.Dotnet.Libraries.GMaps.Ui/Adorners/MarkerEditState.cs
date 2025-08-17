using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/****************************************************************************
   Purpose      : 마커 편집 상태 정보를 담는 클래스                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

/// <summary>
/// 마커 편집 모드 열거형
/// </summary>
public enum MarkerEditMode
{
    /// <summary>
    /// 편집 없음
    /// </summary>
    None = 0,

    /// <summary>
    /// 위치 이동
    /// </summary>
    Move = 1,

    /// <summary>
    /// 회전 조정
    /// </summary>
    Rotate = 2,

    /// <summary>
    /// 크기 조정
    /// </summary>
    Resize = 3
}

/// <summary>
/// 마커 편집 상태 정보
/// </summary>
public class MarkerEditState
{
    /// <summary>
    /// 편집 중 여부
    /// </summary>
    public bool IsEditing { get; set; }

    /// <summary>
    /// 현재 편집 모드
    /// </summary>
    public MarkerEditMode EditMode { get; set; } = MarkerEditMode.None;

    /// <summary>
    /// 편집 대상 마커
    /// </summary>
    public GMapCustomMarker TargetMarker { get; set; }

    /// <summary>
    /// 편집 시작 시간
    /// </summary>
    public DateTime EditStartTime { get; set; }

    /// <summary>
    /// 마지막 편집 시간
    /// </summary>
    public DateTime LastEditTime { get; set; }

    /// <summary>
    /// 정보 표시 여부
    /// </summary>
    public bool ShowInfo { get; set; } = true;

    /// <summary>
    /// 편집 제약 조건 활성화 여부
    /// </summary>
    public bool EnableConstraints { get; set; } = true;

    /// <summary>
    /// 최소 크기 제한
    /// </summary>
    public double MinSize { get; set; } = 10.0;

    /// <summary>
    /// 최대 크기 제한
    /// </summary>
    public double MaxSize { get; set; } = 200.0;

    /// <summary>
    /// 회전 스냅 각도 (도 단위, 0이면 스냅 비활성화)
    /// </summary>
    public double RotationSnapAngle { get; set; } = 15.0;

    /// <summary>
    /// 편집 상태 초기화
    /// </summary>
    public void Reset()
    {
        IsEditing = false;
        EditMode = MarkerEditMode.None;
        EditStartTime = default;
        LastEditTime = default;
    }

    /// <summary>
    /// 편집 시작
    /// </summary>
    public void StartEdit(MarkerEditMode mode)
    {
        IsEditing = true;
        EditMode = mode;
        EditStartTime = DateTime.Now;
        LastEditTime = DateTime.Now;
    }

    /// <summary>
    /// 편집 업데이트
    /// </summary>
    public void UpdateEdit()
    {
        LastEditTime = DateTime.Now;
    }

    /// <summary>
    /// 편집 완료
    /// </summary>
    public void CompleteEdit()
    {
        IsEditing = false;
        EditMode = MarkerEditMode.None;
        LastEditTime = DateTime.Now;
    }

    /// <summary>
    /// 편집 지속 시간 (초)
    /// </summary>
    public double EditDurationSeconds =>
        IsEditing ? (DateTime.Now - EditStartTime).TotalSeconds :
        LastEditTime != default ? (LastEditTime - EditStartTime).TotalSeconds : 0;

    /// <summary>
    /// 편집 상태 문자열
    /// </summary>
    public override string ToString()
    {
        return $"EditState: {EditMode}, Duration: {EditDurationSeconds:F1}s, Target: {TargetMarker?.Title ?? "None"}";
    }
}

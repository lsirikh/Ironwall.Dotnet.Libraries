using System;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/11/2025 2:05:54 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지도 회전 정보를 담는 클래스
/// </summary>
public class RotationInfo
{
    /// <summary>
    /// 현재 회전 각도 (도 단위)
    /// </summary>
    public double CurrentRotation { get; set; }

    /// <summary>
    /// 회전 여부 (0.1도 이상 회전시 true)
    /// </summary>
    public bool IsRotated { get; set; }

    /// <summary>
    /// 회전 중심점
    /// </summary>
    public PointLatLng RotationCenter { get; set; }

    /// <summary>
    /// 스냅 각도 (0이면 스냅 비활성화)
    /// </summary>
    public double SnapAngle { get; set; }

    public override string ToString()
    {
        return $"회전: {CurrentRotation:F1}° (중심: {RotationCenter.Lat:F6}, {RotationCenter.Lng:F6})";
    }
}
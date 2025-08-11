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
/// 회전 정보 클래스
/// </summary>
public class RotationInfo
{
    public double CurrentRotation { get; set; }
    public bool IsRotated { get; set; }
    public PointLatLng RotationCenter { get; set; }
    public double SnapAngle { get; set; }
}
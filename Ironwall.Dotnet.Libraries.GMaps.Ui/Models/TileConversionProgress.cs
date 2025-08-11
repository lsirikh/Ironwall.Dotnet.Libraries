using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      : 타일 변환 진행률 정보                                                         
   Created By   : GHLee                                                
   Created On   : 7/28/2025 7:44:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TileConversionProgress
{
    public int ProcessedTiles { get; set; }
    public int TotalTiles { get; set; }
    public int CurrentZoomLevel { get; set; }
    public double ProgressPercentage { get; set; }
    public string Status => $"줌레벨 {CurrentZoomLevel}: {ProcessedTiles}/{TotalTiles} ({ProgressPercentage:F1}%)";
}
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 6:09:29 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지도의 GMap 심볼용 기본 모델
/// </summary>
public class SymbolModel : BaseModel, ISymbolModel
{
    #region - Ctors -
    public SymbolModel()
    {
        // 기본값 설정
        Id = 0;
        Pid = 0;
        Title = "Unknown";
        OperationState = EnumOperationState.NONE;
        Latitude = 0.0;       // 순수 double 타입
        Longitude = 0.0;      // 순수 double 타입
        Altitude = 0;
        Pitch = 0;
        Roll = 0;
        Width = 30;
        Height = 30;
        Bearing = 0;
        Category = EnumMarkerCategory.BASIC_SHAPES;
        Visibility = true;
    }

    public SymbolModel(int id, string title, double latitude, double longitude)
    {
        Id = id;
        Title = title;
        Latitude = latitude;
        Longitude = longitude;

        // 기본값
        Pid = 0;
        OperationState = EnumOperationState.NONE;
        Altitude = 0;
        Pitch = 0;
        Roll = 0;
        Width = 30;
        Height = 30;
        Bearing = 0;
        Category = EnumMarkerCategory.BASIC_SHAPES;
        Visibility = true;
    }
    #endregion

    #region - 기본 식별 속성 -
    public int Pid { get; set; }
    public string Title { get; set; }
    #endregion

    #region - 타입 및 상태 속성 -
    public EnumOperationState OperationState { get; set; }
    #endregion

    #region - 위치 및 방향 속성 -
    /// <summary>
    /// 위도 (편의 속성)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// 경도 (편의 속성)
    /// </summary>
    public double Longitude { get; set; }

    public float Altitude { get; set; }
    public float Pitch { get; set; }
    public float Roll { get; set; }
    public double Bearing { get; set; }
    #endregion

    #region - 시각적 표현 속성 -
    public double Width { get; set; }
    public double Height { get; set; }
    public EnumMarkerCategory Category { get; set; }
    public bool Visibility { get; set; }
    #endregion



}
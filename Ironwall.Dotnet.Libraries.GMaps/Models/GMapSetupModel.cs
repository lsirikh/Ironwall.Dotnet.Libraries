using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/15/2025 2:31:56 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapSetupModel : IGMapSetupModel
{
    #region - Ctors -
    /// <summary>
    /// 기본 생성자 - Configuration Binding에서 사용
    /// </summary>
    public GMapSetupModel()
    {
        HomePosition = new HomePositionModel();
        MapType = "GoogleSatelliteMap";
        MapMode = "Online";
        MapName = "기본 지도";
        TileDirectory = "c:/";
    }

    /// <summary>
    /// 매개변수 생성자 - 타입 안전성 향상
    /// </summary>
    /// <param name="homePosition">홈 포지션 (null이면 기본값 사용)</param>
    /// <param name="mapType">지도 타입</param>
    /// <param name="mapMode">지도 모드</param>
    /// <param name="mapName">지도 이름</param>
    public GMapSetupModel(
        HomePositionModel? homePosition = null,
        string? mapType = null,
        string? mapMode = null,
        string? mapName = null,
        string? tileDirectory = null)
    {
        HomePosition = homePosition;
        MapType = mapType;
        MapMode = mapMode;
        MapName = mapName;
        TileDirectory = tileDirectory;
    }

    /// <summary>
    /// 복사 생성자
    /// </summary>
    /// <param name="source">복사할 소스 모델</param>
    /// <exception cref="ArgumentNullException">source가 null인 경우</exception>
    public GMapSetupModel(IGMapSetupModel source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        HomePosition = source.HomePosition;
        MapType = source.MapType;
        MapMode = source.MapMode;
        MapName = source.MapName;
        TileDirectory = source.TileDirectory;
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public HomePositionModel? HomePosition { get; set; }
    public string? MapType { get; set; }
    public string? MapMode { get; set; }
    public string? MapName { get; set; }
    public string? TileDirectory { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
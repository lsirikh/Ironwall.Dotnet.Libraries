using Ironwall.Dotnet.Libraries.Base.Models;
using System;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/23/2025 1:50:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class HomePositionModel : BaseModel
{
    #region - Ctors -
    public HomePositionModel()
    {

    }

    public HomePositionModel(int id, PointLatLng position, double zoom, bool isAvailable) : base(id)
    {
        Position = position;
        Zoom = zoom;
        IsAvailable = isAvailable;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public override string ToString()
    {
        return $"Latitude : {Position.Lat}, Longitude : {Position.Lng}";
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public PointLatLng Position { get; set; }
    public double Zoom { get; set; }
    public bool IsAvailable { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
using System;
using System.Diagnostics.Metrics;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/28/2025 3:19:05 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CoordinateModel : ICoordinateModel
{
    #region - Ctors -
    public CoordinateModel()
    {
        Latitude = 37.648425;
        Longitude = 126.904284;
        Altitude = 0.0;
    }

    public CoordinateModel(double latitude, double longitude, double altitude = 0.0)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    public CoordinateModel(ICoordinateModel model)
    {
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        Altitude = model.Altitude;
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
    /// <summary>
    /// Lines of latitude run east-west, parallel to the equator.
    /// They are measured in degrees, ranging from 0° at the equator to +90° at the North Pole and -90° at the South Pole.
    /// </summary>
    public double Latitude { get; set; }
    /// <summary>
    /// Lines of longitude, also called meridians, run north-south, converging at the poles. 
    /// They are measured in degrees, ranging from 0° at the Prime Meridian (which passes through Greenwich, England) to +180° (east) and -180° (west). 
    /// </summary>
    public double Longitude { get; set; }
    /// <summary>
    /// Altitude, or elevation, is the vertical distance above a reference point, typically sea level. 
    /// It is usually measured in meters or feet.
    /// </summary>
    public double Altitude { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
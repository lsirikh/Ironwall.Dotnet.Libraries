namespace Ironwall.Dotnet.Libraries.GMaps.Models;

public interface ICoordinateModel
{
    /// <summary>
    /// Lines of latitude run east-west, parallel to the equator.
    /// They are measured in degrees, ranging from 0° at the equator to +90° at the North Pole and -90° at the South Pole.
    /// </summary>
    double Latitude { get; set; }
    /// <summary>
    /// Lines of longitude, also called meridians, run north-south, converging at the poles. 
    /// They are measured in degrees, ranging from 0° at the Prime Meridian (which passes through Greenwich, England) to +180° (east) and -180° (west). 
    /// </summary>
    double Longitude { get; set; }
    /// <summary>
    /// Altitude, or elevation, is the vertical distance above a reference point, typically sea level. 
    /// It is usually measured in meters or feet.
    /// </summary>
    double Altitude { get; set; }
}
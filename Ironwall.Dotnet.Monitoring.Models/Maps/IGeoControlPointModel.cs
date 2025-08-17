using Ironwall.Dotnet.Libraries.Base.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/// <summary>
/// 지리참조 기준점 인터페이스 (순수 데이터)
/// </summary>
public interface IGeoControlPointModel : IBaseModel
{
    int CustomMapId { get; set; }
    double PixelX { get; set; }
    double PixelY { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    double? AccuracyMeters { get; set; }
    string? Description { get; set; }
}

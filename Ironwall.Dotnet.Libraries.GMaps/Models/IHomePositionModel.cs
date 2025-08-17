using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;

public interface IHomePositionModel
{
    CoordinateModel? Position { get; set; }
    double Zoom { get; set; }
    bool IsAvailable { get; set; }

    string ToString();
}
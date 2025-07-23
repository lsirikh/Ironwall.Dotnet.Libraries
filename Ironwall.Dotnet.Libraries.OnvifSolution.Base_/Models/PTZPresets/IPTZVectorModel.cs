using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets
{
    public interface IPTZVectorModel
    {
        Vector2DModel PanTilt { get; set; }
        Vector1DModel Zoom { get; set; }
    }
}
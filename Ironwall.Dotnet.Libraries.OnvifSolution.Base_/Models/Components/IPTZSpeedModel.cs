namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    public interface IPTZSpeedModel
    {
        Vector2DModel PanTilt { get; set; }
        Vector1DModel Zoom { get; set; }
    }
}
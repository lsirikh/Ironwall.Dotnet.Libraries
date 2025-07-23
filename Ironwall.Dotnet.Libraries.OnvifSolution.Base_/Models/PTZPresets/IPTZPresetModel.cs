namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets
{
    public interface IPTZPresetModel
    {
        string Name { get; set; }
        PTZVectorModel PTZPosition { get; set; }
        string Token { get; set; }
    }
}
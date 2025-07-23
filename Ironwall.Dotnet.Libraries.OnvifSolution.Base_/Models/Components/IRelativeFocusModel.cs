namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    public interface IRelativeFocusModel
    {
        float Distance { get; set; }
        float Speed { get; set; }
        bool SpeedSpecified { get; set; }
    }
}
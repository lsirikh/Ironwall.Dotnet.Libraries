using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Focuses
{
    public interface IFocusMoveModel
    {
        AbsoluteFocusModel AbsoluteFocus { get; set; }
        ContinuousFocusModel ContinuousFocus { get; set; }
        RelativeFocusModel RelativeFocus { get; set; }
    }
}
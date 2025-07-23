namespace Ironwall.Dotnet.Libraries.Events.Models;

public interface IEventSetupModel
{
    bool IsAutoEventDiscard { get; set; }
    bool IsSound { get; set; }
    int LengthMaxEventPrev { get; set; }
    int LengthMinEventPrev { get; set; }
    int TimeDiscardSec { get; set; }
    int TimeDurationSound { get; set; }
}
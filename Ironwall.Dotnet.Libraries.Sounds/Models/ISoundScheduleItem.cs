
namespace Ironwall.Dotnet.Libraries.Sounds.Models;

public interface ISoundScheduleItem
{
    string EventId { get; set; }
    CancellationToken ExternalToken { get; set; }
    int Priority { get; set; }
    DateTime ScheduledTime { get; set; }
    ISoundModel SoundModel { get; set; }
}
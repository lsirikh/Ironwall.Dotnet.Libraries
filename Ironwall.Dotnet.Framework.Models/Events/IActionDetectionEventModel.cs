namespace Ironwall.Dotnet.Framework.Models.Events;

public interface IActionDetectionEventModel : IActionEventModel
{
    int Result { get; set; }
}
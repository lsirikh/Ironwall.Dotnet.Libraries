namespace Ironwall.Dotnet.Framework.Models.Events;

public interface IActionMalfunctionEventModel : IActionEventModel
{
    int FirstEnd { get; set; }
    int FirstStart { get; set; }
    int Reason { get; set; }
    int SecondEnd { get; set; }
    int SecondStart { get; set; }
}
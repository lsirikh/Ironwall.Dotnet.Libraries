namespace Ironwall.Dotnet.Monitoring.Models.Events;

public interface IActionEventModel : IBaseEventModel
{
    string? Content { get; set; }
    string? User { get; set; }
    IExEventModel? OriginEvent { get; set; }
}
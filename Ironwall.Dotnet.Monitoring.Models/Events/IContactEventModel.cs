namespace Ironwall.Dotnet.Monitoring.Models.Events;

public interface IContactEventModel : IExEventModel
{
    int ContactNumber { get; set; }
    int ContactSignal { get; set; }
    int ReadWrite { get; set; }
}
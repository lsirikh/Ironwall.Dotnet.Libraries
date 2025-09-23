using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IPidsGroupSymbolModel : ILineSymbolModel
{
    EnumEventStatus EventStatus { get; set; }
    int LinkedDeviceGroup { get; set; }
    void SetUpdate();
    event EventHandler Update;
}
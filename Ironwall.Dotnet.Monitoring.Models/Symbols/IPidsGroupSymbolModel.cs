using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IPidsGroupSymbolModel : IPidsEventCapable, ILineSymbolModel
{
    int LinkedDeviceGroup { get; set; }
    event EventHandler Update;
}
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;

public interface IPidsEventCapable : ISymbolModel
{
    EnumEventStatus EventStatus { get; set; }
    EnumCompositeEventStatus CompositeStatus { get; set; }
    void SetUpdate();
}
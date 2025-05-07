namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface ISymbolInfoTableMapper : IUpdateMapperBase
    {
        int Map { get; set; }
        int ObjectShape { get; set; }
        int ShapeSymbol { get; set; }
        int Symbol { get; set; }
    }
}
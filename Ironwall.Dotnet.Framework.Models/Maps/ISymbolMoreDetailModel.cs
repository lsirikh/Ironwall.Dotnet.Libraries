namespace Ironwall.Dotnet.Framework.Models.Maps
{
    public interface ISymbolMoreDetailModel : IUpdateDetailBaseModel
    {
        int Map { get; set; }
        int ObjectShape { get; set; }
        int Points { get; set; }
        int ShapeSymbol { get; set; }
        int Symbol { get; set; }
    }
}
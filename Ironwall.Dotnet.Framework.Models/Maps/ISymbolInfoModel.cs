using System;

namespace Ironwall.Dotnet.Framework.Models.Maps
{
    public interface ISymbolInfoModel
    {
        int Map { get; set; }
        int ObjectShape { get; set; }
        int ShapeSymbol { get; set; }
        int Symbol { get; set; }
        DateTime UpdateTime { get; set; }
    }
}
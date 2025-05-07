using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points;
using Newtonsoft.Json;

using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface ISymbolDataLoadResponseModel : IResponseModel
    {
        List<MapModel>? Maps { get; }
        List<PointClass>? Points { get; }
        List<ObjectShapeModel>? Objects { get; }
        List<ShapeSymbolModel>? Shapes { get; }
        List<SymbolModel>? Symbols { get; }
    }
}
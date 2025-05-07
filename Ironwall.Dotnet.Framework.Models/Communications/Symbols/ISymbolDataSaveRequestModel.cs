using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface ISymbolDataSaveRequestModel : IUserSessionBaseRequestModel
    {
        //Body<MapModel> Maps { get; }
        List<ObjectShapeModel>? Objects { get; }
        List<PointClass>? Points { get; }
        List<ShapeSymbolModel>? Shapes { get; }
        List<SymbolModel>? Symbols { get; }
    }
}
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface ISymbolResponseModel : IResponseModel
    {
        List<SymbolModel>? Symbols { get; }
    }
}
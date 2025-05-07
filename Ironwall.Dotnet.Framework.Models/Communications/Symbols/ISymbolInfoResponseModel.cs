using Ironwall.Dotnet.Framework.Models.Maps;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface ISymbolInfoResponseModel : IResponseModel
    {
        SymbolDetailModel? Detail { get; }
    }
}
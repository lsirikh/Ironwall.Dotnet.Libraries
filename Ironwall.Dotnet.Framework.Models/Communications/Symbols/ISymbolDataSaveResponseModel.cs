using Ironwall.Dotnet.Framework.Models.Maps;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface ISymbolDataSaveResponseModel : IResponseModel
    {
        SymbolMoreDetailModel? Detail { get; }
    }
}
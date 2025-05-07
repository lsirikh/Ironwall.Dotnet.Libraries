using Ironwall.Dotnet.Framework.Models.Maps;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface IMapFileSaveResponseModel : IResponseModel
    {
        MapDetailModel? Detail { get; set; }
    }
}
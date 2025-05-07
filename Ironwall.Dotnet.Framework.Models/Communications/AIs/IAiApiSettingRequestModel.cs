using Ironwall.Dotnet.Framework.Models.Ais;

namespace Ironwall.Dotnet.Framework.Models.Communications.AIs
{
    public interface IAiApiSettingRequestModel : IBaseMessageModel
    {
        NetworkSettingModel SettingModel { get; set; }
    }
}
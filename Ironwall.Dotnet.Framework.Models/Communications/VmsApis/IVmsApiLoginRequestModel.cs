using Sensorway.Accounts.Base.Models;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis;

public interface IVmsApiLoginRequestModel : IBaseMessageModel
{
    LoginUserModel? Body { get; set; }
}
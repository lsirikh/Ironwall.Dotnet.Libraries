using Sensorway.Accounts.Base.Models;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis;

public interface IVmsApiLogoutRequestModel : IBaseMessageModel
{
    LoginSessionModel? Body { get; set; }
}
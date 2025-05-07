using Sensorway.Accounts.Base.Models;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis;

public interface IVmsApiKeepAliveResponseModel : IResponseModel
{
    LoginSessionModel? Body { get; set; }
}
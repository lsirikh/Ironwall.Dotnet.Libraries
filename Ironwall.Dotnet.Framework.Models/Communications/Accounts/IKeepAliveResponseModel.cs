using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface IKeepAliveResponseModel : IResponseModel
    {
        DateTime TimeExpired { get; set; }
    }
}
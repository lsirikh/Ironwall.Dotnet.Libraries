using System;

namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public interface IUserSessionBaseRequestModel : IBaseMessageModel
    {
        string Token { get; }
        string UserId { get; }
        string UserPass { get; }
        DateTime TimeCreated { get; }
        DateTime TimeExpired { get; }
    }
}
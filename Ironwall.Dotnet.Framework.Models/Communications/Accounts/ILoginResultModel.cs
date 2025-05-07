using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public interface ILoginResultModel
    {
        int ClientId { get; set; }
        AccountDetailModel Details { get; set; }
        int SessionTimeOut { get; set; }
        string Token { get; set; }
        string UserId { get; set; }
        int UserLevel { get; set; }
        DateTime TimeCreated { get; set; }
        DateTime TimeExpired { get; set; }

        //void Insert(string userId, string token, int clientId, int userLevel, int sessionTimeOut, AccountDetailModel details, string createdTime, string expiredTime);
    }
}
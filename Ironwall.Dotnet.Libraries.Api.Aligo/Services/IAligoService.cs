using Dotnet.Gym.Message.Contacts;
using Ironwall.Dotnet.Libraries.Base.Services;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Api.Aligo.Services;
public interface IAligoService: IService
{
    Task<HttpResponseMessage?> CancelReserveMsgAsync(int mid = 1);
    Task<HttpResponseMessage?> GetAvailableEmsServiceAsync();
    Task<HttpResponseMessage?> GetSentMessageListAsync(int page = 1, int pageSize = 10, DateTime startDate = default, int limitDay = 7);
    Task<HttpResponseMessage?> GetSentSpecificMessageListAsync(int mid = 1, int page = 1, int pageSize = 10);
    Task<HttpResponseMessage?> SendEmsMessageAsync(IEmsMessageModel emsMessage, bool isReserve = false, bool testMode = false);
    Task<HttpResponseMessage?> SendMassEmsMessageAsync(List<IEmsMessageModel> messages, bool isReserve = false, bool testMode = false);
}
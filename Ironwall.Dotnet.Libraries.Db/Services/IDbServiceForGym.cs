using Dotnet.Gym.Message.Accounts;
using Dotnet.Gym.Message.Contacts;

namespace Ironwall.Dotnet.Libraries.Db.Services;
public interface IDbServiceForGym
{
    Task BuildSchemeAsync(CancellationToken token = default);
    Task Connect(CancellationToken token = default);
    Task DeleteActiveUserAsync(int userId, CancellationToken token = default);
    Task DeleteAllUsersAsync(CancellationToken token = default);
    Task DeleteEmsMessageAsync(int emsId, CancellationToken token = default);
    Task DeleteLockerAsync(int userId, CancellationToken token = default);
    Task DeleteUserAsync(int userId, CancellationToken token = default);
    Task Disconnect(CancellationToken token = default);
    void Dispose();
    Task<List<EmsMessageModel>?> FetchEmsMessagesAsync(CancellationToken token = default);
    Task FetchInstanceAsync(CancellationToken token = default);
    Task<UserModel?> FetchUserByIdAsync(int userId, CancellationToken token = default);
    Task<List<UserModel>?> FetchUsersAsync(CancellationToken token = default);
    Task InsertActiveUserAsync(IActivePeriod model, CancellationToken token = default);
    Task<int> InsertEmsMessageAsync(IEmsMessageModel msg, CancellationToken token = default);
    Task InsertLockerAsync(ILockerModel locker, CancellationToken token = default);
    Task<int> InsertUserAsync(IUserModel user, CancellationToken token = default);
    Task StartService(CancellationToken token = default);
    Task StopService(CancellationToken token = default);
    Task UpdateActiveUserAsync(IActivePeriod model, CancellationToken token = default);
    Task UpdateLockerAsync(ILockerModel locker, CancellationToken token = default);
    Task UpdateUserAsync(IUserModel user, CancellationToken token = default);
}
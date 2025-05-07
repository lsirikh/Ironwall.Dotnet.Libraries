using Ironwall.Dotnet.Libraries.Base.Services;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Api.Services;
public interface IApiService : IService
{
    void Initialize();
    Task<HttpResponseMessage> DeleteRequestAsync(string endpoint);
    Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null);
    Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body);
    Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content);
    Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body);
    Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body);
    string Url { get; }
    string ApiKey { get; }
    string UserId { get; }
    string Phone { get; }
}
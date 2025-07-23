using Ironwall.Dotnet.Libraries.Redis.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Redis.Models;
public interface IRedisMessageModel
{
    MessageArgsModel? Model { get; set; }
    JsonSerializerSettings? Settings { get; set; }
}
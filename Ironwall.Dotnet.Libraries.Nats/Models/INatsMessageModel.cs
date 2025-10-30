using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Nats.Models;
public interface INatsMessageModel
{
    MessageArgsModel? Model { get; set; }
    JsonSerializerSettings? Settings { get; set; }
}
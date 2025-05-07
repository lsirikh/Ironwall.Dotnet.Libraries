using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public interface IBaseMessageModel
    {
        int Id { get; set; }
        EnumCmdType Command { get; set; }
        DateTime Datetime { get; set; }
    }
}
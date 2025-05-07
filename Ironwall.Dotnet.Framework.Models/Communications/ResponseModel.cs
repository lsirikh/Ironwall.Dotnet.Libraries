using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public class ResponseModel 
        : BaseMessageModel
        , IResponseModel
    {
        public ResponseModel()
        {

        }

        public ResponseModel(bool success, string msg)
        {
            Success = success;
            Message = msg;
        }

        public ResponseModel(EnumCmdType cmd, bool success, string msg)
        {
            Command = cmd;
            Success = success;
            Message = msg;
        }

        [JsonProperty("success", Order = 1)]
        public bool Success { get; set; }
        [JsonProperty("code", Order = 2)]
        public int Code { get; set; }
        [JsonProperty("message", Order = 3)]
        public string Message { get; set; } = string.Empty;
    }
}

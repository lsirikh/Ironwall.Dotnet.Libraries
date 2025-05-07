using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Utils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Design;

namespace Ironwall.Dotnet.Framework.Models.Communications;

public class BaseMessageModel : IBaseMessageModel
{
    public BaseMessageModel()
    {
        Id = IdGenTool.GenIdInt();
        Datetime = DateTime.Now;
    }

    public BaseMessageModel(EnumCmdType cmd) : this()
    {
        Command = cmd;
    }

    public BaseMessageModel(IBaseMessageModel model) : this()
    {
        Id = model.Id;
        Command = model.Command;
    }

    public BaseMessageModel(int id, EnumCmdType command = default, DateTime? dateTime = null) : this()
    {
        Id = id == 0 ? IdGenTool.GenIdInt():id;
        Command = command;
        Datetime = dateTime ?? DateTime.Now;
    }
    
    [JsonProperty("id", Order = 0)]
    public int Id { get; set; }

    [JsonProperty("command", Order = 1)]
    public EnumCmdType Command { get; set; }

    [JsonProperty("time", Order = 99)]
    public DateTime? Datetime { get; set; }

}




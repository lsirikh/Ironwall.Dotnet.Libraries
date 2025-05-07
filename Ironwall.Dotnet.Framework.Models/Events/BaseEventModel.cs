
using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Communications;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Events;

public abstract class BaseEventModel : BaseModel, IBaseEventModel
{
    public BaseEventModel()
    {
        DateTime = DateTime.Now;
    }

    public BaseEventModel(IEventMapperBase model) : base(model.Id)
    {
        MessageType = model.MessageType;
        DateTime = DateTime.Parse(model.Datetime);
    }

    public BaseEventModel(IBaseEventMessageModel model) : base(model.Id)
    {
        MessageType = EnumHelper.GetEventType(model.Command);
        DateTime = model.Datetime;
    }

    protected BaseEventModel(IBaseEventModel model) : base(model.Id)
    {
        MessageType = model.MessageType;
        DateTime = model.DateTime;
    }

    [JsonProperty("type_event", Order = 5)]
    public EnumEventType MessageType { get; set; } = default!;

    [JsonProperty("datetime", Order = 20)]
    public DateTime DateTime { get; set; } = System.DateTime.Now;
}

using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Communications.Helpers;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Events;

public class ActionEventModel
    : BaseEventModel, IActionEventModel
{

    public ActionEventModel()
    {
        MessageType = EnumEventType.Action;
    }

    public ActionEventModel(IActionEventMapper model, IMetaEventModel fromEvent) : base(model)
    {
        try
        {
            if(fromEvent == null) throw new ArgumentNullException(nameof(fromEvent));
            
            FromEvent = (MetaEventModel)fromEvent;
            Content = model.Content;
            User = model.User;
            MessageType = EnumEventType.Action;
        }
        catch (Exception)
        {
            throw;
        }
        
    }

    public ActionEventModel(IActionRequestMalfunctionModel model) : base(model)
    {
        FromEvent = model.Event;
        Content = model.Content;
        User = model.User;
        MessageType = EnumEventType.Action;
    }

    public ActionEventModel(IActionRequestDetectionModel model) : base(model)
    {
        FromEvent = model.Event;
        Content = model.Content;
        User = model.User;
        MessageType = EnumEventType.Action;
    }

    public ActionEventModel(IActionRequestModel model) : base(model)
    {
        FromEvent = model.Body.FromEvent;
        Content = model.Body.Content;
        User = model.Body.User;
        MessageType = EnumEventType.Action;
    }

    public ActionEventModel(IActionEventModel model) : base(model)
    {
        FromEvent = model.FromEvent;
        Content = model.Content;
        User = model.User;
        MessageType = EnumEventType.Action;
    }

    [JsonProperty("from_event", Order = 2)]
    [JsonConverter(typeof(EventModelConverter))] // JsonConverter 추가
    public MetaEventModel FromEvent { get; set; } = default!;
    [JsonProperty("content", Order = 3)]
    public string Content { get; set; } = string.Empty;
    [JsonProperty("user", Order = 4)]
    public string User { get; set; } = string.Empty;

}

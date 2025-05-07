using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Communications;
using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Communications.Helpers;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public class MetaEventModel : BaseEventModel, IMetaEventModel
    {

        public MetaEventModel()
        {

        }

        public MetaEventModel(IMetaEventMapper model, IBaseDeviceModel device) : base(model)
        {
            EventGroup = model.EventGroup;
            MessageType = model.MessageType;
            Device = device as BaseDeviceModel;
            Status = EnumHelper.SetStatusType(model.Status);
        }

        public MetaEventModel(IMetaEventModel model): base(model)
        {
            EventGroup = model.EventGroup;
            MessageType = model.MessageType;
            Device = model.Device;
            Status = model.Status;
        }

        [JsonProperty("group_event", Order = 3)]
        public string? EventGroup { get; set; }

        [JsonProperty("device", Order = 4)]
        [JsonConverter(typeof(DeviceModelConverter))] // JsonConverter 추가
        public BaseDeviceModel? Device { get; set; }
        
        [JsonProperty("status", Order = 19)]
        public EnumTrueFalse Status { get; set; }
    }
}

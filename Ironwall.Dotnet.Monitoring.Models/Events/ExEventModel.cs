using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 12:59:45 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ExEventModel : BaseEventModel, IExEventModel
{
    public ExEventModel()
    {
    }

    public ExEventModel(IExEventModel model, IBaseDeviceModel device) : base(model)
    {
        EventGroup = model.EventGroup;
        MessageType = model.MessageType;
        Device = device as BaseDeviceModel;
        Status = model.Status;
    }

    public ExEventModel(IExEventModel model) : base(model)
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
    public IBaseDeviceModel? Device { get; set; }

    [JsonProperty("status", Order = 19)]
    public EnumTrueFalse Status { get; set; }
}
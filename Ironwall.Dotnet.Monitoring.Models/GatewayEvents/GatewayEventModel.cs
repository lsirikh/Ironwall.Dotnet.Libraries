using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
/****************************************************************************
   Purpose      : Gateway 이벤트 스키마 모델 (3rd Party 통합용)                                                         
   Created By   : GHLee                                                
   Created On   : 10/29/2025 12:16:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GatewayEventModel : BaseModel, IGatewayEventModel
{
    public GatewayEventModel()
    {
    }

    public GatewayEventModel(IGatewayEventModel model) : base(model)
    {
        EventName = model.EventName;
        DeviceGroups = new List<int>(model.DeviceGroups);
        IsEnable = model.IsEnable;
        Description = model.Description;
    }

    [JsonProperty("event_name", Order = 2)]
    public string EventName { get; set; } = string.Empty;

    [JsonProperty("groups", Order = 3)]
    public List<int> DeviceGroups { get; set; } = new();

    [JsonIgnore]
    [Obsolete("Use DeviceGroups. Kept for migration period only.")]
    public int Group
    {
        get => DeviceGroups.FirstOrDefault();
        set { if (value > 0 && !DeviceGroups.Contains(value)) DeviceGroups.Add(value); }
    }

    [JsonProperty("is_enable", Order = 4)]
    public bool IsEnable { get; set; } = true;

    [JsonProperty("description", Order = 5)]
    public string? Description { get; set; }
}


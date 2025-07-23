using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 1:02:34 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

/// <summary>
/// List·IEnumerable<BaseDeviceModel> 변환 전용
/// (내부에서 DeviceModelConverter 재사용)
/// </summary>
public sealed class DeviceModelListConverter : JsonConverter<IList<IBaseDeviceModel>>
{
    private readonly ILogService? _log;
    private readonly DeviceModelConverter _itemConv;
    public override bool CanWrite => false;
    public DeviceModelListConverter() => _itemConv = new DeviceModelConverter();
    public DeviceModelListConverter(ILogService log)
    {
        _log = log;
        _itemConv = new DeviceModelConverter(log);
    }


    public override IList<IBaseDeviceModel>? ReadJson(JsonReader reader, Type objectType, IList<IBaseDeviceModel>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        try
        {
            var arr = JArray.Load(reader);
            var list = existingValue ?? new List<IBaseDeviceModel>(arr.Count);

            foreach (var token in arr)
            {
                // JToken → JsonReader 재생성 후 단일 Converter 호출
                using var subReader = token.CreateReader();
                list.Add(_itemConv.ReadJson(subReader,
                                            typeof(BaseDeviceModel),
                                            null,
                                            false,
                                            serializer)!);
            }
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            return null;
        }
        
    }

    public override void WriteJson(JsonWriter writer, IList<IBaseDeviceModel>? value, JsonSerializer serializer)
        => serializer.Serialize(writer, value);
}
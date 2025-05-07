using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class CameraDeviceModel : BaseDeviceModel, ICameraDeviceModel
{
    public CameraDeviceModel()
    {
        Profiles = new();
        Presets = new();
        DeviceType = EnumDeviceType.IpCamera;
    }

    public CameraDeviceModel(int id) : this()
    {
        Id = id;
    }

    public CameraDeviceModel(ICameraTableMapper model, List<CameraPresetModel> presets, List<CameraProfileModel> profiles)
        : base(model)
    {
        IpAddress = model.IpAddress;
        Port = model.Port;
        UserName = model.UserName;
        Password = model.Password;
        Presets = presets;
        Profiles = profiles;
        Category = model.Category;
        DeviceModel = model.DeviceModel;
        RtspUri = model.RtspUri;
        RtspPort = model.RtspPort;
        Mode = model.Mode;
    }

    public CameraDeviceModel(ICameraDeviceModel model, List<CameraPresetModel>? presets = default, List<CameraProfileModel>? profiles = default)
        : base(model)
    {
        IpAddress = model.IpAddress;
        Port = model.Port;
        UserName = model.UserName;
        Password = model.Password;
        Presets = presets != null ? presets : model.Presets;
        Profiles = profiles != null ? profiles : model.Profiles;
        Category = model.Category;
        DeviceModel = model.DeviceModel;
        RtspUri = model.RtspUri;
        RtspPort = model.RtspPort;
        Mode = model.Mode;
    }

    [JsonProperty("ip_address", Order = 6)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("ip_port", Order = 7)]
    public int Port { get; set; } = 80;
    
    [JsonProperty("category", Order = 8)]
    public EnumCameraType Category { get; set; }

    [JsonProperty("user_name", Order = 9)]
    public string UserName { get; set; } = string.Empty;

    [JsonProperty("user_pass", Order = 10)]
    public string Password { get; set; } = string.Empty;

    [JsonProperty("presets", Order = 11)]
    public List<CameraPresetModel> Presets { get; set; }

    [JsonProperty("profiles", Order = 12)]
    public List<CameraProfileModel> Profiles { get; set; }

    [JsonProperty("device_model", Order = 13)]
    public string DeviceModel { get; set; } = string.Empty;

    [JsonProperty("rtsp_uri", Order = 14)]
    public string RtspUri { get; set; } = string.Empty;

    [JsonProperty("rtsp_port", Order = 15)]
    public int RtspPort { get; set; } = 554;

    [JsonProperty("mode", Order = 16)]
    public int Mode { get; set; }

}

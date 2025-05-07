using Ironwall.Dotnet.Framework.Models.Devices;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    public interface ICameraOptionResponseModel : IResponseModel
    {
        List<CameraPresetModel> Presets { get; }
        List<CameraProfileModel> Profiles { get; }
    }
}
using Ironwall.Dotnet.Framework.Models.Devices;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    public interface ICameraDataSaveResponseModel : IResponseModel
    {
        List<CameraDeviceModel> Body { get; set; }
    }
}
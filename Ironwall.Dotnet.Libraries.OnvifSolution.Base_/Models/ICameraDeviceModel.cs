using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    public interface ICameraDeviceModel : IConnectionModel, IDeviceInfoModel
    {
        void Update(ICameraDeviceModel model);
        EnumCameraType Type { get; set; }
        CameraMediaModel CameraMedia { get; set; }
        EnumCameraStatus CameraStatus { get; set; }
        DateTime UpdateTime { get; set; }
    }
}
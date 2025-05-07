using System;

namespace Ironwall.Dotnet.Framework.Models.Devices
{
    public interface IMappingInfoModel
    {
        int Mapping { get; set; }
        DateTime UpdateTime { get; set; }
        void Clear();
    }
}
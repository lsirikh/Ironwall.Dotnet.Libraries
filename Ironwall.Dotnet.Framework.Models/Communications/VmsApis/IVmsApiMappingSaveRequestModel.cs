using Ironwall.Dotnet.Framework.Models.Vms;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    public interface IVmsApiMappingSaveRequestModel : IBaseMessageModel
    {
        List<VmsMappingModel>? Body { get; set; }
    }
}
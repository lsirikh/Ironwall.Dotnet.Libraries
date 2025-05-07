using Ironwall.Dotnet.Framework.Models.Vms;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis;

public interface IVmsApiMappingListResponseModel :IResponseModel
{
    List<VmsMappingModel>? Body { get; set; }
}
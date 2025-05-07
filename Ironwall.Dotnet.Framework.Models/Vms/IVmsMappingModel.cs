using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Vms
{
    public interface IVmsMappingModel: IBaseModel
    {
        int GroupNumber { get; set; }
        int EventId { get; set; }
        EnumTrueFalse Status { get; set; }
    }
}
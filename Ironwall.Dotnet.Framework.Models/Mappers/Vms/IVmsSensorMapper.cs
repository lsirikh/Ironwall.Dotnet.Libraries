namespace Ironwall.Dotnet.Framework.Models.Mappers.Vms
{
    public interface IVmsSensorMapper : IBaseModel
    {
        int Device { get; set; }
        int GroupNumber { get; set; }
        bool Status { get; set; }
    }
}
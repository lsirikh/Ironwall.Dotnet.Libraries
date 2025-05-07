namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface IMappingTableMapper : IBaseModel
    {
        string MappingGroup { get; set; }
        int Sensor { get; set; }
        int? FirstPreset { get; set; }
        int? SecondPreset { get; set; }
    }
}
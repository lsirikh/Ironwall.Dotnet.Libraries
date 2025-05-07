namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface IPresetTableMapper : IOptionMapperBase
    {
        int Delay { get; set; }
        bool IsHome { get; set; }
        double Pan { get; set; }
        string PresetName { get; set; }
        double Tilt { get; set; }
        double Zoom { get; set; }
    }
}
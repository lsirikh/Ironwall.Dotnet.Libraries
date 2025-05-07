namespace Ironwall.Dotnet.Framework.Models.Symbols
{
    public interface IEntityPropertyModel : IPropertyModel
    {
        int IdController { get; set; }
        int IdSensor { get; set; }
        string NameArea { get; set; }
        string NameDevice { get; set; }
        int TypeDevice { get; set; }
        int TypeShape { get; set; }
    }
}
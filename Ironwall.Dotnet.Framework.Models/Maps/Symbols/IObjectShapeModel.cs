namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols
{
    public interface IObjectShapeModel : IShapeSymbolModel
    {
        int IdController { get; set; }
        int IdSensor { get; set; }
        string NameArea { get; set; }
        string NameDevice { get; set; }
        int TypeDevice { get; set; }
    }
}
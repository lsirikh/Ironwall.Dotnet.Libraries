namespace Ironwall.Dotnet.Framework.Models.Symbols
{
    public interface IShapePropertyModel : IPropertyModel
    {
        string ShapeFill { get; set; }
        string ShapeStroke { get; set; }
        double ShapeStrokeThick { get; set; }
    }
}
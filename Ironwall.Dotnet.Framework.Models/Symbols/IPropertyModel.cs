namespace Ironwall.Dotnet.Framework.Models.Symbols
{
    public interface IPropertyModel
    {
        int Id { get; set; }
        double X { get; set; }
        double Y { get; set; }
        double Width { get; set; }
        double Height { get; set; }
        double Angle { get; set; }
        bool IsShowLable { get; set; }
        string Lable { get; set; }
        double FontSize { get; set; }
        bool Visibility { get; set; }
        bool Used { get; set; }
    }
}
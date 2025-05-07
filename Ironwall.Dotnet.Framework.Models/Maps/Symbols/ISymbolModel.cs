namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols
{
    public interface ISymbolModel
    {
        int Id { get; set; }
        double X { get; set; }
        double Y { get; set; }
        double Z { get; set; }
        double Width { get; set; }
        double Height { get; set; }
        double Angle { get; set; }
        bool IsShowLable { get; set; }
        string Lable { get; set; }
        double FontSize { get; set; }
        string FontColor { get; set; }
        int TypeShape { get; set; }
        bool IsVisible { get; set; }
        int Layer { get; set; }
        int Map { get; set; }
        bool IsUsed { get; set; }
    }
}
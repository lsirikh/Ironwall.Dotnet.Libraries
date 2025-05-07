namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points
{
    public interface IPointClass
    {
        int Id { get; set; }
        int PointGroup { get; set; }
        int Sequence { get; set; }
        double X { get; set; }
        double Y { get; set; }
    }
}
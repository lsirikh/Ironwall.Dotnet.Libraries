namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface IActionEventMapper : IEventMapperBase
    {
        int FromEventId { get; set; }
        string Content { get; set; }
        string User { get; set; }
    }
}
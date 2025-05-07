namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IActionDetailModel
    {
        int Id { get; set; }
        string Content { get; set; }
        string FromEventId { get; set; }
        string IdUser { get; set; }
    }
}
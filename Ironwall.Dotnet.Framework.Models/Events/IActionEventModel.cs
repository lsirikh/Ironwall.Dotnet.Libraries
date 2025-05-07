namespace Ironwall.Dotnet.Framework.Models.Events
{
    public interface IActionEventModel : IBaseEventModel
    {
        MetaEventModel FromEvent { get; set; }
        string Content { get; set; }
        string User { get; set; }
    }
}
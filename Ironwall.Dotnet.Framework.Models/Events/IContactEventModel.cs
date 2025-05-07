namespace Ironwall.Dotnet.Framework.Models.Events
{
    public interface IContactEventModel : IMetaEventModel
    {
        int ContactNumber { get; set; }
        int ContactSignal { get; set; }
        int ReadWrite { get; set; }
    }
}
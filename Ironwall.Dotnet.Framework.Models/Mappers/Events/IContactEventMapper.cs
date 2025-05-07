namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface IContactEventMapper : IMetaEventMapper
    {
        int ContactNumber { get; set; }
        int ContactSignal { get; set; }
        int ReadWrite { get; set; }
    }
}
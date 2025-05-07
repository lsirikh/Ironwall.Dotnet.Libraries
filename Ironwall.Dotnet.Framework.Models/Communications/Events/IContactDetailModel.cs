namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IContactDetailModel 
    {
        int ContactNumber { get; set; }
        int ContactSignal { get; set; }
        int ReadWrite { get; set; }
    }
}
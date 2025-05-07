namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface ISearchDetectionRequestModel : IUserSessionBaseRequestModel
    {
        string EndDateTime { get; set; }
        string StartDateTime { get; set; }
    }
}
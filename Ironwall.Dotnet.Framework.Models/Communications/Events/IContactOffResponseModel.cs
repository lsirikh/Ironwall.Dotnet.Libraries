namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IContactOffResponseModel : IResponseModel
    {
        ContactOffRequestModel? RequestModel { get; set; }
    }
}
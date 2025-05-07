namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis;

public interface IVmsApiGetEventListRequestModel:IBaseMessageModel
{
    string? Token { get; set; }
}
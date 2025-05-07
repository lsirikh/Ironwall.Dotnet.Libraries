namespace Ironwall.Dotnet.Framework.Models.Vms
{
    public interface IVmsApiModel : IBaseModel
    {
        string ApiAddress { get; set; }
        uint ApiPort { get; set; }
        string Username { get; set; }
        string Password { get; set; }
    }
}
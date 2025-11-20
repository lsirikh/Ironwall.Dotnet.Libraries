namespace Ironwall.Dotnet.Libraries.Api.Models;

public interface IApiSetupModel
{
    string Url { get; set; }
    string Username { get; set; }
    string Password { get; set; }
    string ApiKey { get; set; }
    string Phone { get; set; }
    int Timeout { get; set; }
}
namespace Ironwall.Dotnet.Libraries.Api.Models;

public interface IApiSetupModel
{
    string Url { get; set; }
    string Username { get; set; }
    string Password { get; set; }
    string ApiKey { get; set; }
    string Phone { get; set; }
    int Timeout { get; set; }

    /// <summary>GOP-00 (FR-4): Bearer/refresh 토큰 폴백 보관. 동적 주입은 BearerAuthHandler 가 우선(§3.2).</summary>
    string BearerToken { get; set; }
    string RefreshToken { get; set; }
}
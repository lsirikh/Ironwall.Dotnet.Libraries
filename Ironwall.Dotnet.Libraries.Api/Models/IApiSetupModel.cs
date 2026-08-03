namespace Ironwall.Dotnet.Libraries.Api.Models;

public interface IApiSetupModel
{
    string Url { get; set; }
    string Username { get; set; }
    string Password { get; set; }
    string ApiKey { get; set; }
    string Phone { get; set; }
    int Timeout { get; set; }

    /// <summary>세션 식별용 클라이언트 ID(서버 X-Client-Id). 주체별 고유(관제 UI=central-ui). 빈값/패턴위반이면 미전송(하위호환).
    /// <para>default 구현 — 기존 IApiSetupModel 구현체(메인 앱 SetupModel 등)를 깨지 않는다. 설정 주입이 필요하면 구현체가 override(ApiSetupModel 참조).</para></summary>
    string ClientId { get => "central-ui"; set { } }
}
using Sso.Client.Sdk;

namespace Ironwall.Dotnet.Libraries.Sso;

/// <summary>
/// <see cref="ISsoAgentGateway"/> 의 실제 구현 — <c>Sso.Client.Sdk</c> 의 <see cref="SsoAgentClient"/> 를 감싼다.
///
/// <para><b>client_id 는 앱 종류</b>(<c>gis-monitoring</c>)다 — SSO 등록 단위이자 토큰의 <c>client_id</c> 클레임.
/// GOP 헤더 <c>X-Client-Id</c>(설치 고유값)와는 <b>다른 축</b>이다(3자 계약 C-10).</para>
/// <para><c>issuer</c> 는 앱에 적지 않는다 — 에이전트가 안다(PRD V2).</para>
/// </summary>
public sealed class SsoAgentGateway : ISsoAgentGateway, IDisposable
{
    /// <summary>SSO 에 등록된 이 앱의 이름 — 토큰 <c>client_id</c> 클레임과 같아야 한다.</summary>
    public const string AppClientId = "gis-monitoring";

    private readonly SsoAgentClient _client;

    public SsoAgentGateway(string clientId = AppClientId) => _client = new SsoAgentClient(clientId);

    public async Task<SsoAgentResult> SignInAsync(CancellationToken ct = default)
    {
        // 티켓 인자 없이 = 갱신·401 복구가 쓰는 경로(C-16). SDK 3.10.6 은 실패를 예외가 아니라 값으로 준다.
        var r = await _client.SignInAsync(Array.Empty<string>(), ct).ConfigureAwait(false);
        return Map(r.Outcome, r.Value?.AccessToken, r.Detail);
    }

    public async Task<SsoAgentResult> SignInInteractiveAsync(CancellationToken ct = default)
    {
        var r = await _client.SignInInteractiveAsync(ct).ConfigureAwait(false);
        return Map(r.Outcome, r.Value?.AccessToken, r.Detail);
    }

    /// <summary>SDK 결과를 GIS 대응 기준으로 접는다 — 시험 대상.</summary>
    internal static SsoAgentResult Map(SsoOutcome outcome, string? token, string detail) => outcome switch
    {
        SsoOutcome.Ok when !string.IsNullOrEmpty(token) => SsoAgentResult.Ok(token),
        SsoOutcome.Ok => SsoAgentResult.Fail(SsoAgentStatus.Failed, "에이전트가 성공을 알렸지만 토큰이 비었다"),

        SsoOutcome.AgentNotRunning or SsoOutcome.VersionMismatch or SsoOutcome.SignInUnsupported
            => SsoAgentResult.Fail(SsoAgentStatus.Unavailable, detail),

        SsoOutcome.NoActiveSession => SsoAgentResult.Fail(SsoAgentStatus.NoActiveSession, detail),
        SsoOutcome.UnknownClient => SsoAgentResult.Fail(SsoAgentStatus.NotRegistered, detail),
        SsoOutcome.ConsentRequired => SsoAgentResult.Fail(SsoAgentStatus.ConsentRequired, detail),
        SsoOutcome.UpstreamUnavailable => SsoAgentResult.Fail(SsoAgentStatus.UpstreamUnavailable, detail),

        _ => SsoAgentResult.Fail(SsoAgentStatus.Failed, $"{outcome}: {detail}"),
    };

    public void Dispose() => _client.Dispose();
}

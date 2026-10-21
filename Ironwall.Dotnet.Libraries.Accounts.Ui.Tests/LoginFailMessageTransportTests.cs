using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/// <summary>
/// 원격지 설치 준비 — 로그인 화면이 <b>서버에 닿지 못한 사유</b>를 사람이 알 수 있게 보이는가(창 없이 문구만).
/// <para>게이트웨이는 연결 실패면 세부 사유 코드(<c>CERT_UNTRUSTED</c> 등)를 <see cref="AuthOutcome.ErrorCode"/> 로 넘긴다
/// (Accounts.Api <c>ApiTransportFailureTests</c> DEP-213). 종전 화면은 모르는 코드를 전부
/// "아이디 또는 비밀번호가 일치하지 않습니다." 로 보였다 — 인증서 문제를 비밀번호 문제로 오진하게 만든다.</para>
/// </summary>
[Trait("Scenario", "DEP-216")]
public class LoginFailMessageTransportTests
{
    [Theory]
    [InlineData("CERT_UNTRUSTED", "서버 인증서를 신뢰할 수 없습니다", "신뢰 저장소")]
    [InlineData("CONNECTION_REFUSED", "연결을 거부", "포트")]
    [InlineData("CONNECT_TIMEOUT", "시간이 초과", "방화벽")]
    [InlineData("NAME_RESOLUTION_FAILED", "이름을 찾을 수 없습니다", "DNS")]
    [InlineData("CONNECTION_FAILED", "서버에 연결할 수 없습니다", "서버 주소")]
    [InlineData("REQUEST_TIMEOUT", "요청 시간이 초과되었습니다", "응답")]
    public void should_show_transport_reason_instead_of_credential_error(string code, string reason, string nextStep)
    {
        // Arrange — 게이트웨이가 넘기는 모양(메시지는 비워도 화면 문구는 코드로 정한다)
        var outcome = AuthOutcome.Fail(code);

        // Act
        var text = LoginPanelViewModel.FailMessage(outcome);

        // Assert
        Assert.Contains(reason, text);
        Assert.Contains(nextStep, text);
        Assert.DoesNotContain("비밀번호", text);
    }

    [Theory]
    [InlineData("UNAUTHORIZED", "아이디 또는 비밀번호가 일치하지 않습니다.")]
    [InlineData("SERVICE_UNAVAILABLE", "서버에 연결할 수 없습니다.")]
    [InlineData("GATEWAY_TIMEOUT", "요청 시간이 초과되었습니다.")]
    public void should_keep_existing_messages_for_existing_codes(string code, string expected)
    {
        // 무회귀 — 서버가 준 상위 코드의 종전 문구는 그대로다.
        Assert.Equal(expected, LoginPanelViewModel.FailMessage(AuthOutcome.Fail(code)));
    }

    [Trait("Scenario", "GOP815-G07")]
    [Theory]
    [InlineData("비활성화된 계정입니다", "비활성화된 계정입니다")]   // G07 — 서버가 403 사유를 알려 주면 그대로
    [InlineData("   ", "계정이 잠겼거나 비활성 상태입니다. (로그인 시도 초과 등) 관리자에게 문의하세요.")]
    [InlineData(null, "계정이 잠겼거나 비활성 상태입니다. (로그인 시도 초과 등) 관리자에게 문의하세요.")]
    public void should_show_the_server_reason_for_forbidden_login_when_it_is_given(string? serverMessage, string expected)
        => Assert.Equal(expected, LoginPanelViewModel.FailMessage(AuthOutcome.Fail("FORBIDDEN", serverMessage)));
}

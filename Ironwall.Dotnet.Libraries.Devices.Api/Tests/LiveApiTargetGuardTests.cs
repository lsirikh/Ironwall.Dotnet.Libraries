using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// 라이브 시험의 <b>원격 쓰기 안전 문</b> — 네트워크 없이 판정만 본다.
/// <para>쓰기 시험(생성 · 수정 · 삭제)은 loopback · 사설망(RFC1918) 주소에서만 돈다. 원격 운영 서버
/// (예: 123.141.236.253)를 가리키면 네트워크에 닿기 전에 건너뛴다.</para>
/// </summary>
[Trait("Scenario", "DEP-208")]
public class LiveApiTargetGuardTests
{
    [Theory]
    [InlineData("https://localhost:8000/api")]
    [InlineData("https://LOCALHOST:8000/api")]
    [InlineData("https://127.0.0.1:8000/api")]
    [InlineData("https://[::1]:8000/api")]
    [InlineData("https://10.0.0.5:8000/api")]
    [InlineData("https://172.16.0.1:8000/api")]
    [InlineData("https://172.31.255.254:8000/api")]
    [InlineData("https://192.168.0.20:8136/api")]
    public void should_allow_writes_only_on_loopback_or_private_address(string url)
    {
        Assert.True(LiveApiTarget.IsWriteAllowedHost(new Uri(url)));
        Assert.Null(LiveApiTarget.WriteGuardReason(url));
    }

    [Theory]
    [InlineData("https://123.141.236.253:8136/api")]   // 원격 운영 GOP DB API
    [InlineData("https://123.141.236.253:18443")]      // 원격 SSO
    [InlineData("https://172.32.0.1:8000/api")]        // 172.16/12 바로 밖
    [InlineData("https://172.15.255.255:8000/api")]
    [InlineData("https://192.169.0.1:8000/api")]
    [InlineData("https://11.0.0.1:8000/api")]
    [InlineData("https://gop-server.example:8136/api")] // 이름은 해석하지 않고 원격으로 본다(보수적)
    public void should_block_writes_before_touching_remote_address(string url)
    {
        Assert.False(LiveApiTarget.IsWriteAllowedHost(new Uri(url)));

        var reason = LiveApiTarget.WriteGuardReason(url);
        Assert.NotNull(reason);
        Assert.Contains("원격", reason);
    }

    [Fact]
    public void should_default_to_https_local_server()
    {
        // 옛 시험은 http://localhost:8000/api 로 고정돼 HTTPS 서버에 닿지 못했다.
        Assert.Equal("https://localhost:8000/api", LiveApiTarget.DefaultUrl);
        Assert.StartsWith("https://", LiveApiTarget.DefaultUrl);
    }

    [Fact]
    public void should_not_carry_credentials_in_setup()
    {
        // 계정은 전용 주입 파일로만 — 설정 모델(NetworkCredential 자리)에는 싣지 않는다.
        var setup = LiveApiTarget.CreateSetup();
        Assert.True(string.IsNullOrEmpty(setup.Username));
        Assert.True(string.IsNullOrEmpty(setup.Password));
    }
}

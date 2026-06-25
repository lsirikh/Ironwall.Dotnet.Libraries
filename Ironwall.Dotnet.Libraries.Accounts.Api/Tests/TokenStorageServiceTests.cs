using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

public class TokenStorageServiceTests
{
    /// <summary>exp 클레임만 담은 최소 JWT(header.payload.sig) 생성. 서명은 더미.</summary>
    private static string MakeJwt(DateTimeOffset exp)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = B64($"{{\"sub\":\"u\",\"exp\":{exp.ToUnixTimeSeconds()}}}");
        return $"{header}.{payload}.sig";
    }

    [Fact]
    public void should_report_authenticated_when_access_token_set()
    {
        var s = new TokenStorageService();
        Assert.False(s.IsAuthenticated);

        s.SetTokens("abc", "ref");

        Assert.True(s.IsAuthenticated);
        Assert.Equal("abc", s.AccessToken);
        Assert.Equal("ref", s.RefreshToken);
    }

    [Fact]
    public void should_decode_exp_when_access_token_is_jwt()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(24);
        var s = new TokenStorageService();

        s.SetTokens(MakeJwt(exp));

        Assert.NotNull(s.AccessExpiresAtUtc);
        Assert.Equal(
            exp.ToUnixTimeSeconds(),
            new DateTimeOffset(s.AccessExpiresAtUtc!.Value, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    [Fact]
    public void should_flag_expiring_when_within_threshold()
    {
        var s = new TokenStorageService();
        s.SetTokens(MakeJwt(DateTimeOffset.UtcNow.AddMinutes(3)));

        Assert.True(s.IsAccessTokenExpiring(TimeSpan.FromMinutes(5)));
        Assert.False(s.IsAccessTokenExpiring(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void should_not_flag_expiring_when_exp_unknown()
    {
        var s = new TokenStorageService();
        s.SetTokens("not-a-jwt");

        Assert.Null(s.AccessExpiresAtUtc);
        Assert.False(s.IsAccessTokenExpiring(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void should_clear_all_when_clear_called()
    {
        var s = new TokenStorageService();
        s.SetTokens("a", "r");

        s.Clear();

        Assert.Null(s.AccessToken);
        Assert.Null(s.RefreshToken);
        Assert.Null(s.AccessExpiresAtUtc);
        Assert.False(s.IsAuthenticated);
    }
}

using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// FR-1 DTO 역직렬화 검증. permissions flat-string(B-3) 수용 + 날짜 string 보존(DateParseHandling.None) + 누락 NRE 없음.
/// (VER-08 실제 서버 샘플 확보 시 케이스 추가)
/// </summary>
public class AccountDtoTests
{
    private const string LoginJson = @"{
        ""success"": true,
        ""message"": ""ok"",
        ""data"": {
            ""access_token"": ""acc"",
            ""refresh_token"": ""ref"",
            ""token_type"": ""bearer"",
            ""user"": {
                ""id"": 1,
                ""login_id"": ""admin"",
                ""name"": ""관리자"",
                ""role"": ""ADMIN"",
                ""group_id"": 2,
                ""permissions"": { ""events"": ""rw"", ""devices"": ""rw"", ""reports"": ""rw"" },
                ""is_active"": true,
                ""is_locked"": false,
                ""last_login_at"": ""2026-06-24T04:34:00.000Z""
            }
        }
    }";

    [Fact]
    public void should_deserialize_login_envelope_with_nested_user()
    {
        var res = ApiMessageHelper.FromJsonResponse<LoginResponseDataDto>(LoginJson);

        Assert.NotNull(res);
        Assert.True(res!.Success);
        Assert.Equal("acc", res.Data!.AccessToken);
        Assert.Equal("ref", res.Data.RefreshToken);
        Assert.NotNull(res.Data.User);
        Assert.Equal("admin", res.Data.User!.LoginId);
        Assert.Equal("ADMIN", res.Data.User.Role);
        Assert.Equal(2, res.Data.User.GroupId);
    }

    [Fact]
    public void should_accept_flat_string_permissions_as_raw_jobject()
    {
        var res = ApiMessageHelper.FromJsonResponse<LoginResponseDataDto>(LoginJson);

        var perms = res!.Data!.User!.Permissions;
        Assert.NotNull(perms);
        Assert.Equal("rw", (string?)perms!["events"]);
        Assert.Equal("rw", (string?)perms["devices"]);
    }

    [Fact]
    public void should_keep_datetime_as_raw_string()
    {
        var res = ApiMessageHelper.FromJsonResponse<LoginResponseDataDto>(LoginJson);

        // DateParseHandling.None → DateTime 변환 없이 원문 문자열 보존
        Assert.Equal("2026-06-24T04:34:00.000Z", res!.Data!.User!.LastLoginAt);
    }

    [Fact]
    public void should_not_throw_when_permissions_missing()
    {
        const string noPerm = @"{""success"":true,""data"":{""access_token"":""a"",""user"":{""id"":3,""login_id"":""u"",""role"":""VIEWER""}}}";

        var res = ApiMessageHelper.FromJsonResponse<LoginResponseDataDto>(noPerm);

        Assert.NotNull(res);
        Assert.Null(res!.Data!.User!.Permissions);   // 누락 시 null, NRE 없음
        Assert.Equal("VIEWER", res.Data.User.Role);
    }
}

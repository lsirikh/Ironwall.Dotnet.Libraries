using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// FR-19 UserGroup/UserSession DTO 역직렬화 검증 — <b>실서버 응답 발췌</b> 사용(2026-06-25 캡처).
/// UserGroup 권한은 nested(login 응답의 flat-string과 다름, B-3) 임을 실측 확인.
/// </summary>
public class AdminDtoTests
{
    [Fact]
    public void should_deserialize_usergroup_with_nested_permissions()
    {
        // GET /api/user-groups 실제 응답 발췌(운영팀)
        const string json = @"{""success"":true,""data"":[{
            ""id"":1,""name"":""운영팀"",""description"":""시스템 운영 담당"",
            ""permissions"":{""modules"":{
                ""events"":{""edit"":true,""view"":true,""delete"":false,""control"":false},
                ""devices"":{""edit"":true,""view"":true,""delete"":false,""control"":false}
            },""device_groups"":null},
            ""is_active"":true,""user_count"":null,
            ""created_at"":""2026-06-19T17:08:42.085398+09:00""}]}";

        var res = ApiMessageHelper.FromJsonListResponse<UserGroupDto>(json);

        Assert.NotNull(res);
        Assert.True(res!.Success);
        var grp = res.Data![0];
        Assert.Equal("운영팀", grp.Name);
        Assert.True(grp.Permissions!.Modules["events"].Edit);   // nested 권한 파싱
        Assert.True(grp.Permissions.Modules["events"].View);
        Assert.False(grp.Permissions.Modules["events"].Control);
        Assert.Null(grp.Permissions.DeviceGroups);
        Assert.Null(grp.UserCount);                             // 목록에선 null(B-5)
    }

    [Fact]
    public void should_deserialize_usersession_list()
    {
        // GET /api/user-sessions 실제 응답 발췌
        const string json = @"{""success"":true,""data"":[{
            ""id"":135,""user_id"":1,""login_id"":""admin"",""role"":""ADMIN"",
            ""ip_address"":""172.18.0.1"",""user_agent"":""curl/8.18.0"",
            ""expires_at"":""2026-06-27T00:19:13.047670+09:00"",""is_active"":true,
            ""logout_reason"":null,""logged_out_at"":null}]}";

        var res = ApiMessageHelper.FromJsonListResponse<UserSessionDto>(json);

        Assert.NotNull(res);
        var s = res!.Data![0];
        Assert.Equal(135, s.Id);
        Assert.Equal("admin", s.LoginId);
        Assert.Equal("ADMIN", s.Role);
        Assert.True(s.IsActive);
        Assert.Null(s.LoggedOutAt);
    }
}

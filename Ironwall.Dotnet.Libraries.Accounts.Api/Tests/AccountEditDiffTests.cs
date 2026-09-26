using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 편집 다이얼로그 [확인] 의 "바뀐 칸만" 판정과 서버 모드 게이트 — 라이브 왕복(accounts-left AL1 · AL3 · AL4)의 헤드리스 짝.
/// </summary>
public class AccountEditDiffTests
{
    private static AccountModel Before() => new()
    {
        Id = 5,
        Username = "op5",
        Name = "김운영",
        EMail = "op5@example.com",
        Phone = "010-0000-0005",
        Department = "경비과",
        Position = null,
        EmployeeNumber = "21-1",
        Role = EnumUserRole.USER,
        Level = EnumLevelType.USER,
        Used = EnumUsedType.USED,
    };

    private static AccountModel Copy(AccountModel m)
    {
        var c = new AccountModel();
        c.Update(m);
        return c;
    }

    [Fact]
    public void should_return_no_field_when_nothing_changed()
    {
        var before = Before();

        Assert.Empty(AccountDtoMapper.ChangedFields(before, Copy(before)));
    }

    [Fact]
    public void should_not_report_role_when_only_the_department_changed()
    {
        var before = Before();
        var after = Copy(before);
        after.Department = "상황실";

        Assert.Equal(new[] { "department" }, AccountDtoMapper.ChangedFields(before, after));
    }

    [Fact]
    public void should_treat_blank_and_null_as_the_same_when_a_field_was_already_empty()
    {
        var before = Before();
        var after = Copy(before);
        after.Position = "";   // 화면 입력 칸은 비어 있으면 빈 글을 준다

        Assert.Empty(AccountDtoMapper.ChangedFields(before, after));
    }

    [Fact]
    public void should_report_is_active_when_the_status_changes_to_not_used()
    {
        var before = Before();
        var after = Copy(before);
        after.Used = EnumUsedType.NOT_USED;

        Assert.Equal(new[] { "is_active" }, AccountDtoMapper.ChangedFields(before, after));
    }

    [Fact]
    public void should_not_report_role_when_a_legacy_role_normalizes_to_the_same_server_role()
    {
        var before = Before();
        before.Role = EnumUserRole.OPERATOR;   // 레거시 5단계 — 서버 어휘로는 USER
        var after = Copy(before);
        after.Role = EnumUserRole.USER;

        Assert.Empty(AccountDtoMapper.ChangedFields(before, after));
    }

    [Fact]
    public void should_send_explicit_nulls_when_the_changed_fields_are_cleared()
    {
        var before = Before();
        var after = Copy(before);
        after.EMail = "";
        after.Phone = "";

        var fields = AccountDtoMapper.ChangedFields(before, after);
        var wire = JObject.Parse(JsonConvert.SerializeObject(AccountDtoMapper.ToUserUpdateDto(after, fields)));

        Assert.Equal(new[] { "email", "phone" }, wire.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(JTokenType.Null, wire["email"]!.Type);
        Assert.Equal(JTokenType.Null, wire["phone"]!.Type);
    }

    [Fact]
    public void should_not_infer_first_account_admin_when_the_directory_is_the_server()
    {
        IUserDirectoryGateway gw = new ApiAccountGateway(new AccountApiService(null!, null!), new TokenStorageService(), new PermissionService());

        Assert.False(gw.CanInferFirstAccountAdmin);
    }

    [Fact]
    public void should_not_allow_self_edit_of_the_employee_number_when_the_profile_is_on_the_server()
    {
        IProfileGateway gw = new ApiAccountGateway(new AccountApiService(null!, null!), new TokenStorageService(), new PermissionService());

        Assert.False(gw.CanSelfEditEmployeeNumber);
    }
}

using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 계정 콘솔 콤보 항목 이름 — UIA · 화면 읽기 프로그램이 읽는 이름은 항목 객체의 <c>ToString()</c> 이다
/// (<c>DisplayMemberPath</c> 는 그려지는 글자만 바꾼다).
/// </summary>
/// <remarks>
/// 대상: [부여] 레일의 [계정] 콤보(<c>GrantManagementPanelViewModel.Accounts</c>, <c>DisplayMemberPath="LoginId"</c>) ·
/// [권한 매트릭스] 멤버 추가 콤보(<c>PermissionMatrixPanelViewModel.AddableAccounts</c>) — <see cref="AuthUserDto"/>,
/// [부여] 레일의 [그룹] 콤보(<c>GrantManagementPanelViewModel.Groups</c>, <c>DisplayMemberPath="Name"</c>) — <see cref="UserGroupDto"/>.
/// 수정 전에는 항목이 전부 <c>Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.AuthUserDto</c> 로 읽혀 누구인지 고를 수 없었다
/// (같은 결함을 <c>ReportTemplateDto</c> 에서 bb4da2a4 가 고쳤다).
/// </remarks>
public class AccountComboAutomationNameTests
{
    [Fact]
    public void should_read_login_id_when_account_is_shown_as_combo_item()
    {
        var dto = new AuthUserDto { Id = 12, LoginId = "operator01", Name = "홍길동" };

        Assert.Equal("operator01", dto.ToString());
    }

    [Fact]
    public void should_read_person_name_when_account_login_id_is_empty()
    {
        var dto = new AuthUserDto { Id = 12, LoginId = "", Name = "홍길동" };

        Assert.Equal("홍길동", dto.ToString());
    }

    [Fact]
    public void should_read_account_number_when_account_has_no_login_id_or_name()
    {
        var dto = new AuthUserDto { Id = 12, LoginId = " ", Name = null };

        Assert.Equal("사용자 #12", dto.ToString());
    }

    [Fact]
    public void should_read_group_name_when_group_is_shown_as_combo_item()
    {
        var dto = new UserGroupDto { Id = 3, Name = "관제 운영" };

        Assert.Equal("관제 운영", dto.ToString());
    }

    [Fact]
    public void should_read_group_number_when_group_name_is_empty()
    {
        var dto = new UserGroupDto { Id = 3, Name = "" };

        Assert.Equal("그룹 #3", dto.ToString());
    }
}

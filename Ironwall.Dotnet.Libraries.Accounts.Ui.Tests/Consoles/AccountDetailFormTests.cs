using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 상세 칸 — 여섯 상태 · 손댄 칸만 적용 · 아이디 읽기 전용 · 여러 명 편집의 "— 여러 값 —"
/// (설계 정본 L918-L931 · L1163-L1167 · L1242).
/// </summary>
public class AccountDetailFormTests
{
    [Fact]
    public async Task should_lock_the_login_id_always_because_it_cannot_be_changed_after_creation()
    {
        var console = await OpenWithUsersAsync();
        SelectOne(console, 0);

        var username = console.Form.Fields.Single(f => f.Key == "username");
        Assert.True(username.IsLocked);
        Assert.Equal(AccountFieldCatalog.IdentityLockReason, username.LockReason);
        Assert.Equal(AccountFieldEditor.ReadOnly, username.EffectiveEditor);
    }

    [Fact]
    public async Task should_show_the_mixed_marker_when_the_selected_accounts_differ()
    {
        var console = await OpenWithUsersAsync();
        SelectAll(console);

        var department = console.Form.Fields.Single(f => f.Key == "department");
        Assert.True(department.IsMixed);
        Assert.Equal(ConsoleDetailStateMachine.MixedValuesText, department.DisplayText);
    }

    [Fact]
    public async Task should_lock_identity_fields_when_several_accounts_are_selected()
    {
        var console = await OpenWithUsersAsync();
        SelectAll(console);

        Assert.True(console.Form.Fields.Single(f => f.Key == "name").IsLocked);
        Assert.True(console.Form.Fields.Single(f => f.Key == "phone").IsLocked);
        // 부서 · 직급 · 구분 · 상태는 한꺼번에 바꿀 수 있다.
        Assert.False(console.Form.Fields.Single(f => f.Key == "department").IsLocked);
        Assert.False(console.Form.Fields.Single(f => f.Key == "role").IsLocked);
    }

    [Fact]
    public async Task should_write_only_the_touched_field_when_applying()
    {
        var (console, _, gateway, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        SelectOne(console, 0);

        console.Form.Fields.Single(f => f.Key == "department").Text = "경비1과";
        await console.ApplyAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider[0];
        Assert.Equal("경비1과", row.Department);
        Assert.Equal("김운영", row.Name);              // 손대지 않은 칸은 그대로
        Assert.Equal(1, gateway.UpdateCallCount);      // 고른 계정 1명 = 1회
    }

    [Fact]
    public async Task should_apply_one_field_to_every_selected_account_when_editing_together()
    {
        var (console, _, gateway, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        SelectAll(console);

        console.Form.Fields.Single(f => f.Key == "department").Text = "통합과";
        await console.ApplyAsync();

        Assert.All(console.AccountManagerPanelViewModel.ViewModelProvider, row => Assert.Equal("통합과", row.Department));
        Assert.Equal(3, gateway.UpdateCallCount);
    }

    [Fact]
    public async Task should_refuse_to_apply_when_a_required_field_is_emptied()
    {
        var (console, _, gateway, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        SelectOne(console, 0);

        console.Form.Fields.Single(f => f.Key == "name").Text = "   ";
        await console.ApplyAsync();

        Assert.Equal(0, gateway.UpdateCallCount);
        Assert.Contains("필수", console.DetailMessage);
        Assert.Equal("김운영", console.AccountManagerPanelViewModel.ViewModelProvider[0].Name);
    }

    [Fact]
    public async Task should_restore_the_original_text_when_reverting()
    {
        var console = await OpenWithUsersAsync();
        SelectOne(console, 0);

        var field = console.Form.Fields.Single(f => f.Key == "department");
        field.Text = "바뀐 부서";
        Assert.True(console.DetailIsDirty);

        console.Revert();

        Assert.Equal("경비과", field.Text);
        Assert.False(console.DetailIsDirty);
    }

    [Fact]
    public async Task should_report_read_only_when_the_account_may_not_edit_users()
    {
        var (console, _, _, permission, _) = ConsoleFixtures.Build(Users());
        permission.Role = EnumUserRole.MAINTAINER;
        permission.AuditAllowed = true;
        permission.Allow("users:view");
        await console.ActivateForTestAsync();

        // MAINTAINER 는 사용자 레일을 아예 못 본다 — 그것이 지금의 계약이다.
        Assert.DoesNotContain(console.RailEntries, e => e.Key == "users");
        Assert.False(console.CanEditUsers);
    }

    [Fact]
    public async Task should_keep_the_empty_state_when_nothing_is_selected()
    {
        var console = await OpenWithUsersAsync();

        Assert.Equal("선택한 항목 없음", console.DetailTitle);
        Assert.False(console.DetailShowButtons);
        Assert.False(console.IsDetailRequested);
    }

    [Fact]
    public async Task should_summarise_the_lock_state_of_the_single_selected_account()
    {
        var console = await OpenWithUsersAsync();
        var locked = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op3");
        console.OnUsersSelected(new List<object> { locked });

        Assert.Equal("잠김", console.LockStateText);
        Assert.Equal("로그인 5회 실패", console.LockReasonText);
        Assert.True(console.CanUnlockSelected);
    }

    /// <summary>
    /// D-10 — 칸을 하나 건드리면 적용 막대가 <b>즉시</b> 따라와야 한다.
    /// 값(<c>DetailIsDirty</c>)은 늘 맞았지만 <b>알림</b>이 없어 화면은 "변경 없음" 인 채
    /// [되돌리기] · [적용] 이 꺼져 있었다(깨끗한 화면과 픽셀 100% 동일 실측). 그래서 값이 아니라 알림을 단언한다.
    /// </summary>
    [Fact]
    public async Task should_raise_the_apply_bar_state_when_a_field_is_touched()
    {
        var console = await OpenWithUsersAsync();
        SelectOne(console, 0);

        var raised = new List<string>();
        console.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        console.Form.Fields.Single(f => f.Key == "department").Text = "경비1과(수정)";

        Assert.True(console.DetailIsDirty);
        Assert.True(console.DetailCanApply);
        Assert.True(console.DetailCanRevert);
        Assert.Contains(nameof(console.DetailIsDirty), raised);
        Assert.Contains(nameof(console.DetailCanApply), raised);
        Assert.Contains(nameof(console.DetailCanRevert), raised);
        Assert.Contains(nameof(console.DetailFooter), raised);
    }

    /// <summary>되돌리면 같은 길로 알림이 돌아와 막대가 다시 조용해진다.</summary>
    [Fact]
    public async Task should_raise_the_apply_bar_state_when_the_touched_field_is_reverted()
    {
        var console = await OpenWithUsersAsync();
        SelectOne(console, 0);
        console.Form.Fields.Single(f => f.Key == "department").Text = "경비1과(수정)";

        var raised = new List<string>();
        console.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        console.Revert();

        Assert.False(console.DetailIsDirty);
        Assert.False(console.DetailCanApply);
        Assert.Contains(nameof(console.DetailIsDirty), raised);
        Assert.Contains(nameof(console.DetailCanApply), raised);
    }

    private static IEnumerable<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel> Users() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영", department: "경비과", position: "주임"),
        ConsoleFixtures.User(2, "op2", "이감시", department: "상황실", position: "반장"),
        ConsoleFixtures.User(3, "op3", "박점검", department: "정비과", position: "대리", locked: true),
    };

    private static async Task<TestAccountConsole> OpenWithUsersAsync()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        return console;
    }

    private static void SelectOne(TestAccountConsole console, int index)
        => console.OnUsersSelected(new List<object> { console.AccountManagerPanelViewModel.ViewModelProvider[index] });

    private static void SelectAll(TestAccountConsole console)
        => console.OnUsersSelected(console.AccountManagerPanelViewModel.ViewModelProvider.Cast<object>().ToList());
}

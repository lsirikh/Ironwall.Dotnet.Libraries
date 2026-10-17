using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 권한 "모를 때 기본값" 정책 — PRD symbol-detail-and-door-control FR-27 (계획 TEST-03).
///
/// <para>이 테스트가 지키는 것은 <b>비대칭</b> 하나다: 명령류는 막고 조회·편집은 연다.
/// 한쪽으로 통일하려는 리팩터가 들어오면 여기서 걸린다 —
/// 전부 fail-closed 면 권한 엔진 없는 환경에서 앱이 잠기고, 전부 fail-open 이면 무권한 계정이 문을 연다.</para>
/// </summary>
public class PermissionGateTests
{
    [Theory]
    [InlineData("control", true)]
    [InlineData("CONTROL", true)]      // 대소문자로 정책이 갈리면 안 된다
    [InlineData("Control", true)]
    [InlineData("edit", false)]
    [InlineData("view", false)]
    [InlineData("delete", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void should_classify_only_control_as_command(string? verb, bool expected)
        => Assert.Equal(expected, PermissionGate.IsCommand(verb));

    [Fact]
    public void should_block_commands_when_permission_is_unknown()
    {
        // 되돌릴 수 없는 외부 행위 — 권한을 물어볼 수 없으면 막는다.
        Assert.False(PermissionGate.Resolve(null, "control"));
        Assert.False(PermissionGate.FallbackFor("control"));
    }

    [Theory]
    [InlineData("view")]
    [InlineData("edit")]
    public void should_allow_read_and_edit_when_permission_is_unknown(string verb)
    {
        // 권한 엔진이 없는 환경(단독 실행·테스트)에서 앱이 통째로 잠기면 못 쓴다.
        Assert.True(PermissionGate.Resolve(null, verb));
        Assert.True(PermissionGate.FallbackFor(verb));
    }

    [Theory]
    [InlineData(true, "control", true)]
    [InlineData(false, "control", false)]
    [InlineData(true, "view", true)]
    [InlineData(false, "view", false)]
    [InlineData(true, "edit", true)]
    [InlineData(false, "edit", false)]
    public void should_pass_through_when_permission_is_known(bool granted, string verb, bool expected)
        => Assert.Equal(expected, PermissionGate.Resolve(granted, verb));

    [Fact]
    public void should_never_open_a_command_that_was_explicitly_denied()
    {
        // 폴백 정책이 명시적 거부를 뒤집으면 안 된다 — 서버가 "안 됨"이라고 답한 것이 최우선.
        Assert.False(PermissionGate.Resolve(false, "control"));
        Assert.False(PermissionGate.Resolve(false, "view"));
    }

    [Fact]
    public void should_treat_unknown_verbs_as_non_command()
    {
        // 새 동사가 생기면 기본은 fail-open 이다. 명령류로 다루려면 여기 정책을 명시적으로 고쳐야 한다
        // (조용히 fail-closed 가 되어 멀쩡한 화면이 잠기는 쪽이 더 나쁘다).
        Assert.True(PermissionGate.Resolve(null, "export"));
        Assert.False(PermissionGate.IsCommand("export"));
    }
}

using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 진행 창 — 취소가 있는 진행(스토리보드 #h-dlg T5 표 L1450: 지금은 500×500 에 닫기 요소 0개).
/// </summary>
[Collection("CaliburnIoC")]
public class ProgressDialogViewModelTests
{
    [Fact]
    public void should_start_with_a_live_token_and_an_enabled_cancel()
    {
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");

        Assert.False(vm.Token.IsCancellationRequested);
        Assert.True(vm.CanCancel);
        Assert.False(vm.IsDone);
        Assert.Equal(string.Empty, vm.PrimaryText);
        Assert.Equal("취소", vm.SecondaryText);
    }

    [Fact]
    public void should_show_n_of_m_when_reporting()
    {
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");

        vm.Report(7, 16, "정문 함체 7");

        Assert.Equal("7 / 16", vm.CountText);
        Assert.Equal(43.75d, vm.Percent, 2);
        Assert.False(vm.IsIndeterminate);
        Assert.Contains("정문 함체 7", vm.Message);
    }

    [Fact]
    public void should_run_indeterminate_when_the_total_is_unknown()
    {
        using var vm = new ProgressDialogViewModel("불러오는 중");

        vm.Report(3, 0);

        Assert.True(vm.IsIndeterminate);
        Assert.Equal(0d, vm.Percent);
        Assert.Equal("3", vm.CountText);
    }

    [Fact]
    public void should_cancel_the_token_and_disable_the_button_when_cancel_is_pressed()
    {
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");
        vm.Report(7, 16);

        vm.RequestCancel();

        Assert.True(vm.Token.IsCancellationRequested);
        Assert.True(vm.IsCancelRequested);
        Assert.False(vm.CanCancel);      // 두 번 눌러도 할 일이 없다
    }

    [Fact]
    public void should_say_how_far_it_got_when_finishing_after_a_cancel()
    {
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");
        vm.Report(7, 16);
        vm.RequestCancel();

        vm.Finish();

        Assert.Equal("중단 — 7건 완료", vm.Message);
        Assert.True(vm.IsDone);
        Assert.Equal("닫기", vm.PrimaryText);
        Assert.Equal(string.Empty, vm.SecondaryText);      // 끝난 뒤에는 취소할 것이 없다
    }

    [Fact]
    public void should_say_it_is_done_when_finishing_without_a_cancel()
    {
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");
        vm.Report(16, 16);

        vm.Finish();

        Assert.Equal("끝 — 16건 완료", vm.Message);
        Assert.True(vm.IsClosable);
    }

    [Fact]
    public void should_release_the_window_when_it_fails()
    {
        // 예외가 나도 창은 풀린다 — 잠긴 전체 화면에 갇히지 않는다.
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");

        vm.Fail("서버가 응답하지 않아 멈췄습니다 — 잠시 뒤 다시 시도하세요.");

        Assert.True(vm.IsDone);
        Assert.True(vm.IsClosable);
        Assert.DoesNotContain("Exception", vm.Message);      // 날 예외 글을 사람에게 보이지 않는다
    }

    [Fact]
    public void should_ignore_a_cancel_that_arrives_after_the_work_finished()
    {
        using var vm = new ProgressDialogViewModel("장비를 등록하는 중");
        vm.Report(16, 16);
        vm.Finish();

        vm.RequestCancel();

        Assert.False(vm.IsCancelRequested);
        Assert.Equal("끝 — 16건 완료", vm.Message);
    }
}

using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 블록 — 꺼진 상자 안 칸의 입력 오류가 저장을 막지 않는다 (호스트 보고 R6 · SET-020 · SET-025)
   Created By   : Claude (t-20261011-111636-d46d)
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// "자동 닫기 [사용]" · "타이머로 닫기" 를 끄면 그 옆 숫자 칸은 꺼진다(뷰 IsEnabled). 꺼진 칸의 틀린 글은 저장될 값이 아니므로
/// 저장 판정(<see cref="CameraPopupSettingsViewModel.HasError"/>)에서 빠지고, 친 글은 그대로 남았다가 상자를 다시 켜면 다시 검사된다.
/// 상자가 없는 칸(다음 창 간격 · 응답 기다림)은 늘 저장 대상이라 계속 막는다.
/// </summary>
public class CameraPopupDisabledBoxErrorTests
{
    private sealed class FakePort : ICameraPopupSettingsPort
    {
        public CameraPopupSettings Stored = new CameraPopupSettings().Normalize();
        public int SaveCount;

        public CameraPopupSettings LoadCameraPopup() => Stored;

        public void SaveCameraPopup(CameraPopupSettings settings)
        {
            SaveCount++;
            Stored = settings;
        }

        public string CameraPopupClientId => "gis-test";
    }

    private sealed class OneMonitor : IDisplayMonitorProvider
    {
        public IReadOnlyList<DisplayMonitorInfo> GetMonitors() => new List<DisplayMonitorInfo>
        {
            new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true),
        };
    }

    private static (CameraPopupSettingsViewModel Vm, FakePort Port) Make()
    {
        var port = new FakePort();
        return (new CameraPopupSettingsViewModel(port, new OneMonitor()), port);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    [Trait("Scenario", "SET-020")]
    public void should_not_block_save_when_the_bad_auto_close_time_sits_in_a_switched_off_box(string bad)
    {
        // Arrange — 켜진 상자에서 틀린 값을 치면 막는다(현행 그대로)
        var (vm, port) = Make();
        vm.DoubleClickAutoCloseSecondsText = bad;
        Assert.True(vm.HasError);

        // Act — 상자를 끈다
        vm.DoubleClickAutoClose = false;

        // Assert — 꺼진 칸은 저장 판정에서 빠지고, 친 글은 남는다
        Assert.False(vm.HasError);
        Assert.False(vm.HasDoubleClickAutoCloseSecondsError);
        Assert.Equal(bad, vm.DoubleClickAutoCloseSecondsText);
        var result = vm.Apply();
        Assert.True(result.Applied, result.Reason);
        Assert.Equal(1, port.SaveCount);
        Assert.False(port.Stored.DoubleClickAutoClose);
        Assert.Equal(15, port.Stored.DoubleClickAutoCloseSeconds);                 // 마지막으로 맞았던 값 그대로
        Assert.Equal(bad, vm.DoubleClickAutoCloseSecondsText);                     // 저장 뒤에도 친 글은 보존
    }

    [Fact]
    [Trait("Scenario", "SET-020")]
    public void should_check_the_auto_close_time_again_when_the_box_is_switched_back_on()
    {
        var (vm, _) = Make();
        vm.DoubleClickAutoCloseSecondsText = "0";
        vm.DoubleClickAutoClose = false;
        Assert.False(vm.HasError);

        vm.DoubleClickAutoClose = true;

        Assert.True(vm.HasError);
        Assert.True(vm.HasDoubleClickAutoCloseSecondsError);
        Assert.Equal("1~3600 초 사이 정수를 넣으세요", vm.DoubleClickAutoCloseSecondsError);
        Assert.True(vm.IsGroupTouched(CameraPopupSettingsViewModel.GroupDoubleClick));
        Assert.False(vm.Apply().Applied);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("abc")]
    [InlineData("3601")]
    [Trait("Scenario", "SET-025")]
    public void should_not_block_save_when_the_bad_close_timer_sits_in_a_switched_off_box(string bad)
    {
        var (vm, port) = Make();
        vm.CloseTimerSecondsText = bad;
        Assert.True(vm.HasError);

        vm.CloseByTimer = false;

        Assert.False(vm.HasError);
        Assert.False(vm.HasCloseTimerSecondsError);
        Assert.Equal(bad, vm.CloseTimerSecondsText);
        var result = vm.Apply();
        Assert.True(result.Applied, result.Reason);
        Assert.False(port.Stored.CloseByTimer);
        Assert.Equal(60, port.Stored.CloseTimerSeconds);

        vm.CloseByTimer = true;                                                    // 다시 켜면 다시 검사

        Assert.True(vm.HasError);
        Assert.Equal("5~3600 초 사이 정수를 넣으세요", vm.CloseTimerSecondsError);
    }

    [Fact]
    [Trait("Scenario", "SET-025")]
    public void should_keep_blocking_save_for_fields_that_have_no_switch()
    {
        // 다음 창 간격 · 응답 기다림은 상자 없이 늘 저장되는 칸 — 의도가 달라 꺼진 상자 규칙을 쓰지 않는다
        var (vm, _) = Make();
        vm.DoubleClickAutoClose = false;
        vm.CloseByTimer = false;

        vm.CascadeStepText = "abc";
        Assert.True(vm.HasError);
        vm.CascadeStepText = "24";
        Assert.False(vm.HasError);

        vm.BrokerTimeoutText = "0";
        Assert.True(vm.HasError);
    }

    [Fact]
    [Trait("Scenario", "SET-025")]
    public void should_drop_the_parked_text_when_the_draft_is_reverted()
    {
        var (vm, _) = Make();
        vm.CloseTimerSecondsText = "4";
        vm.CloseByTimer = false;

        vm.Revert();

        Assert.True(vm.CloseByTimer);
        Assert.Equal("60", vm.CloseTimerSecondsText);
        Assert.False(vm.HasError);
    }
}

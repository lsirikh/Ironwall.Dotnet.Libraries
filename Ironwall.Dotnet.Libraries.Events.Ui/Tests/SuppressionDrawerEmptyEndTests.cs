using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 억제 서랍 — 단발에서 끝 칸을 비웠을 때 운영자가 듣는 까닭(2026-09-28 헤디드 SC-SUP-007).
/// </summary>
/// <remarks>
/// 예전엔 빈 끝 칸을 '읽을 수 없는 글자'로만 쳐서 초안에 옛 끝 시각이 남았다. 시작을 이틀 뒤로 옮긴 뒤 끝을 지우면
/// 검증이 그 옛 끝(지금 + 1시간)으로 "종료가 시작보다 뒤여야 합니다." 를 말했다 — 끝을 지운 사람에게 엉뚱한 까닭이다.
/// </remarks>
public class SuppressionDrawerEmptyEndTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 9, 20, 10, 0, 0));
    private readonly SuppressionDrawerViewModel _drawer;

    public SuppressionDrawerEmptyEndTests()
    {
        PlatformProvider.Current = new DefaultPlatformProvider();
        _drawer = new SuppressionDrawerViewModel(_clock, null, null,
            (_, _) => Task.FromResult(new SuppressionSaveOutcome(true, "저장했습니다.", new EventSuppressionScheduleDto { Id = 1 })));
        _drawer.OpenNew();
        _drawer.Name = "정문 보수";
        _drawer.AddSelected(new[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3") });
    }

    [Fact]
    public void should_say_a_one_shot_needs_an_end_when_the_end_field_is_cleared()
    {
        _drawer.WindowStartText = "2026-09-22 03:00";

        _drawer.WindowEndText = "";

        Assert.Contains("단발 억제에는 종료 시각이 있어야 합니다", _drawer.ErrorText);
        Assert.DoesNotContain("종료가 시작보다 뒤여야", _drawer.ErrorText);
        Assert.False(_drawer.CanSave);
    }

    [Fact]
    public void should_keep_the_end_field_open_and_empty_when_a_one_shot_end_is_cleared()
    {
        _drawer.WindowStartText = "2026-09-22 03:00";

        _drawer.WindowEndText = "  ";

        Assert.False(_drawer.IsUnlimited);                 // '기간 제한 없음' 이 아니다 — 끝 칸이 접히면 다시 적을 수 없다
        Assert.Equal(string.Empty, _drawer.WindowEndText);
        Assert.False(_drawer.HasUnreadableTime);
    }

    [Fact]
    public void should_save_again_when_an_end_is_typed_after_clearing_it()
    {
        _drawer.WindowStartText = "2026-09-22 03:00";
        _drawer.WindowEndText = "";

        _drawer.WindowEndText = "2026-09-22 04:00";

        Assert.True(string.IsNullOrEmpty(_drawer.ErrorText), _drawer.ErrorText);
        Assert.True(_drawer.CanSave);
    }

    [Fact]
    public void should_restore_an_end_when_a_cleared_one_shot_switches_to_weekly()
    {
        _drawer.WindowStartText = "2026-09-22 03:00";
        _drawer.WindowEndText = "";

        _drawer.IsWeekly = true;

        Assert.False(_drawer.IsUnlimited);
        Assert.NotEqual(string.Empty, _drawer.WindowEndText);
        Assert.DoesNotContain("단발 억제에는", _drawer.ErrorText);
    }

    [Fact]
    public void should_refuse_to_save_a_weekly_schedule_when_no_weekday_is_chosen()
    {
        // 요일 0개 = 단발이 아니다 — 주간 반복이면 저장을 막는다(PRD FR-08 · SuppressionRules.ValidateWeeklyForm).
        _drawer.IsWeekly = true;
        foreach (var off in new System.Action[]
                 {
                     () => _drawer.IsMonChecked = false, () => _drawer.IsTueChecked = false, () => _drawer.IsWedChecked = false,
                     () => _drawer.IsThuChecked = false, () => _drawer.IsFriChecked = false, () => _drawer.IsSatChecked = false,
                     () => _drawer.IsSunChecked = false,
                 })
            off();

        Assert.False(_drawer.CanSave);
        Assert.Contains("요일", _drawer.ErrorText);
    }
}

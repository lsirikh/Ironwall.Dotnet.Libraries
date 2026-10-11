using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sounds.Tests;

/// <summary>
/// 재생 시험 4건(<see cref="SoundService_PlaybackTests"/>)과 <b>같은 판정</b>을 가짜 재생기로 — 소리 없이 · 파일 없이 · 창 없이.
/// <para>시험마다 자기 서비스 · 자기 설정이라 실행 순서와 무관하다. 클래스 안 시험은 xunit 이 순서대로 돌리므로
/// 일부러 서로 반대 설정(자동 멈춤 켬 ↔ 끔)을 연달아 두어 순서 의존이 없음을 같이 본다.</para>
/// </summary>
public class SoundServiceFakePlayerTests
{
    [Fact]
    [Trait("Scenario", "DEP-225")]
    public async Task should_auto_stop_detection_after_duration_with_fake_player()
    {
        var farm = new FakeWavePlayerFarm();
        await using var iso = await IsolatedSoundService.CreateFakeAsync(farm, s =>
        {
            s.IsDetectionAutoSoundStop = true;
            s.DetectionSoundDuration = 1;
        });

        await SoundPlaybackScenarios.AutoStopsAfterDurationAsync(iso, EnumEventType.Intrusion, FakeWavePlayerFarm.ClipLength * 2);

        Assert.Equal(0, farm.Active);                 // 출력 장치도 멈췄다
        Assert.True(farm.Created >= 1);               // 가짜 재생기가 실제로 쓰였다(실제 NAudio 아님)
    }

    [Fact]
    [Trait("Scenario", "DEP-226")]
    public async Task should_keep_malfunction_playing_until_cancelled_with_fake_player()
    {
        var farm = new FakeWavePlayerFarm();
        await using var iso = await IsolatedSoundService.CreateFakeAsync(farm, s =>
        {
            s.IsMalfunctionAutoSoundStop = false;
            s.MalfunctionSoundDuration = 1;
        });

        await SoundPlaybackScenarios.KeepsPlayingUntilCancelledAsync(iso);

        Assert.Equal(0, farm.Active);
    }

    [Fact]
    [Trait("Scenario", "DEP-227")]
    public async Task should_never_play_two_sounds_at_once_with_fake_player()
    {
        var farm = new FakeWavePlayerFarm();
        await using var iso = await IsolatedSoundService.CreateFakeAsync(farm, s =>
        {
            s.IsDetectionAutoSoundStop = false;
        });

        await SoundPlaybackScenarios.NeverPlaysTwoAtOnceAsync(iso, TimeSpan.FromSeconds(1), () => farm.Active);
    }

    [Fact]
    [Trait("Scenario", "DEP-228")]
    public async Task should_auto_stop_action_report_after_duration_with_fake_player()
    {
        var farm = new FakeWavePlayerFarm();
        await using var iso = await IsolatedSoundService.CreateFakeAsync(farm, s =>
        {
            s.IsActionReportAutoSoundStop = true;
            s.ActionReportSoundDuration = 1;
        });

        await SoundPlaybackScenarios.AutoStopsAfterDurationAsync(iso, EnumEventType.Action, FakeWavePlayerFarm.ClipLength * 2);

        Assert.Equal(0, farm.Active);
    }

    [Fact]
    [Trait("Scenario", "DEP-229")]
    public async Task should_not_share_setup_between_isolated_services()
    {
        // 한 시험이 설정을 바꿔도 다른 시험의 서비스는 영향받지 않는다(예전 공유 SetupModel 결함의 반대 확인).
        var farm = new FakeWavePlayerFarm();
        await using var autoStop = await IsolatedSoundService.CreateFakeAsync(farm, s => s.IsDetectionAutoSoundStop = true);
        await using var manual = await IsolatedSoundService.CreateFakeAsync(farm, s => s.IsDetectionAutoSoundStop = false);

        Assert.NotSame(autoStop.Setup, manual.Setup);
        Assert.NotSame(autoStop.Provider, manual.Provider);
        Assert.True(autoStop.Setup.IsDetectionAutoSoundStop);
        Assert.False(manual.Setup.IsDetectionAutoSoundStop);

        manual.Setup.IsDetectionAutoSoundStop = true;   // 한쪽을 바꿔도
        autoStop.Setup.IsDetectionAutoSoundStop = false;
        Assert.True(manual.Setup.IsDetectionAutoSoundStop);   // 각자 자기 값을 가진다
        Assert.False(autoStop.Setup.IsDetectionAutoSoundStop);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>지도 영상 팝업 — PTZ 속도 우선순위(사용자 설정 → 카메라 기본 → 0.6) · 영상 오른쪽 아래 이동 중 표시.</summary>
public class CameraPopupPtzSpeedMotionTests
{
    private static readonly VideoProviderInfo Provider = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    private static CameraStreamPopupViewModel NewVm() => new(7, "정문", new PointLatLng(37, 127), new OverlayTestHost(), Provider);

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-150")]
    public void should_use_camera_default_speed_when_user_has_not_set_one()
    {
        var vm = NewVm();
        Assert.Equal(0.6, vm.PanTiltSpeed);   // 둘 다 없으면 기존 0.6

        vm.ApplyCameraDefaultSpeeds(0.34, 1.0);

        Assert.Equal(0.3, vm.PanTiltSpeed);   // 슬라이더 단위(0.1)로
        Assert.Equal(1.0, vm.ZoomSpeed);
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-151")]
    public void should_keep_user_speed_over_camera_default()
    {
        var vm = NewVm();
        vm.PanTiltSpeed = 0.9;   // 사용자가 PTZ 탭에서 정했다

        vm.ApplyCameraDefaultSpeeds(0.2, null);

        Assert.Equal(0.9, vm.PanTiltSpeed);
        Assert.Equal(0.6, vm.ZoomSpeed);   // 카메라가 줌 기본 속도를 안 알려줬다
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-152")]
    public async Task should_show_moving_then_done_then_hide_when_host_reports_camera_stopped()
    {
        var vm = NewVm();
        int probes = 0;
        vm.PtzMotionProbe = _ => Task.FromResult<bool?>(Interlocked.Increment(ref probes) < 3 ? true : false);

        vm.BeginPtzMotion(TimeSpan.FromSeconds(5));
        Assert.True(vm.IsPtzMotionMoving);
        Assert.Equal("이동 중", vm.PtzMotionText);

        Assert.True(await WaitUntilAsync(() => vm.IsPtzMotionDone, 4000), "'이동 완료' 가 오지 않았다");
        Assert.Equal("이동 완료", vm.PtzMotionText);
        Assert.True(await WaitUntilAsync(() => !vm.IsPtzMotionVisible, 4000), "사라지지 않았다");
        int after = Volatile.Read(ref probes);
        await Task.Delay(600);
        Assert.Equal(after, Volatile.Read(ref probes));   // 끝나면 묻지 않는다
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int ms)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }
}

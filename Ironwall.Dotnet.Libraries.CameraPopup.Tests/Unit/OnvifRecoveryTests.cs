using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// 팝업 ONVIF 연결 회복(2026-10-22 "팝업 ONVIF 철저 확인") — 계정 거부 뒤 다시 붙기 · 무응답 카메라의 버려진 호출이 줄을 막지 않기 ·
/// 접속 정보가 바뀌면 새로 붙기 · PTZ 구성이 붙은 프로파일로 PTZ 명령 보내기. 가짜 SOAP 카메라(<see cref="FakeOnvifCamera"/>) 상대.
/// 같은 시나리오의 가짜 카메라 팜 판: tools/dummy-cameras/tests/PopupOnvifScenarioTests.
/// </summary>
public class OnvifRecoveryTests
{
    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static VideoProviderInfo Info(FakeOnvifCamera camera) => new()
    {
        Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "u", Password = "p",
    };

    [Fact]
    public async Task should_not_look_connected_and_connect_later_when_camera_refuses_profiles_then_accepts()
    {
        // Arrange — 시계 · GetCapabilities 는 계정 없이 답하고 GetProfiles 에서 계정이 걸린다(틀린 비밀번호 · 잠긴 계정)
        await using var camera = new FakeOnvifCamera { FailProfiles = true };
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;

        // Act
        var refused = await ptz.PrepareAsync("rec-1", Info(camera), Within(10_000));
        camera.FailProfiles = false;
        var accepted = await ptz.PrepareAsync("rec-1", Info(camera), Within(10_000));

        // Assert
        Assert.False(refused.Connected, "프로파일을 못 읽은 연결이 '연결됨'으로 캐시됐다");
        Assert.True(accepted.Connected);
        Assert.True(accepted.PtzCapable, "카메라가 다시 받아 주는데도 옛 캐시 때문에 PTZ 가 안 된다");
    }

    [Fact]
    public async Task should_free_the_camera_queue_soon_after_camera_returns_when_a_call_was_abandoned_on_silence()
    {
        // Arrange — 준비된 뒤 카메라가 응답을 멈춘다(호출이 WCF 송신 제한 8초까지 매달린다)
        await using var camera = new FakeOnvifCamera();
        var log = new TestLog();
        var ptz = CameraProviderRegistry.CreateDefault(log).GetPtz(VideoProviderKind.Onvif)!;
        Assert.True((await ptz.PrepareAsync("rec-2", Info(camera), Within(10_000))).PtzCapable);
        await Task.Delay(300);   // 준비 직후 배경 위치 읽기가 끝나게
        camera.Delay = TimeSpan.FromSeconds(30);

        // Act — 끊긴 동안 한 번(포기), 돌아온 뒤 호스트처럼 "준비 안 됐으면 준비 → 명령"
        var sw = Stopwatch.StartNew();
        var down = await ptz.GetPresetsAsync("rec-2", Within(1_000));
        var downMs = sw.ElapsedMilliseconds;
        camera.Delay = TimeSpan.Zero;
        sw.Restart();
        if (!ptz.IsPrepared("rec-2")) await ptz.PrepareAsync("rec-2", Info(camera), Within(5_000));
        var up = await ptz.GetPresetsAsync("rec-2", Within(5_000));
        var upMs = sw.ElapsedMilliseconds;

        // Assert
        Assert.Null(down);
        Assert.True(downMs < 1_700, $"끊긴 동안 {downMs} ms 뒤에야 돌아왔다");
        Assert.True(up is not null, "돌아온 뒤 프리셋 목록 실패" + Environment.NewLine + string.Join(Environment.NewLine, log.Lines));
        Assert.True(upMs < 3_000, $"카메라가 돌아온 뒤 첫 명령이 {upMs} ms 걸렸다(버려진 호출이 줄을 쥐고 있었다)");
    }

    [Fact]
    public async Task should_use_the_new_camera_when_address_changes_for_the_same_camera_id()
    {
        // Arrange — 같은 장비 id 의 주소가 운용 중 바뀌었다(장비 설정 수정)
        await using var oldCamera = new FakeOnvifCamera { StreamUri = "rtsp://10.0.0.1/old" };
        await using var newCamera = new FakeOnvifCamera { StreamUri = "rtsp://10.0.0.2/new" };
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var video = registry.GetVideo(VideoProviderKind.Onvif)!;

        // Act
        var before = await video.ResolveStreamAsync("rec-3", Info(oldCamera), Within(10_000));
        var after = await video.ResolveStreamAsync("rec-3", Info(newCamera), Within(10_000));

        // Assert
        Assert.EndsWith("10.0.0.1/old", before.Uri);
        Assert.EndsWith("10.0.0.2/new", after.Uri);
        Assert.Contains(newCamera.Requests, r => r.Body.Contains("GetProfiles"));
    }

    [Fact]
    public async Task should_send_ptz_commands_to_the_profile_carrying_ptz_when_first_profile_has_none()
    {
        // Arrange — 첫 프로파일(P0)에 PTZ 구성 없음, P1 에 있음
        await using var camera = new FakeOnvifCamera { PtzProfileToken = "P1" };
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        Assert.True((await ptz.PrepareAsync("rec-4", Info(camera), Within(10_000))).PtzCapable);

        // Act
        Assert.True(await ptz.ContinuousMoveAsync("rec-4", 0.5, 0, 0, Within(5_000)));
        await ptz.StopAsync("rec-4", Within(5_000));

        // Assert
        var commands = camera.PtzCommands;
        Assert.Contains(commands, c => c.Op == "ContinuousMove");
        Assert.All(commands, c => Assert.Contains(">P1<", c.Body));
        ptz.Release("rec-4");
    }
}

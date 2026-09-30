using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// 영상 위 드래그 PTZ · 누름 이동 — 실제 제공자 경로(<see cref="CameraProviderRegistry"/> → PtzController → OnvifSolution)를
/// 루프백 가짜 ONVIF 카메라에 붙여 "카메라에 무엇이 몇 번 도착했는가"를 본다.
/// </summary>
public class PtzDragProviderTests
{
    private readonly ITestOutputHelper _output;

    public PtzDragProviderTests(ITestOutputHelper output) => _output = output;

    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static async Task<(ICameraPtzProvider Ptz, VideoProviderInfo Info)> PrepareAsync(FakeOnvifCamera camera, string id, bool waitForStatus)
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());
        var info = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "fake-user", Password = "pw" };
        var ptz = registry.GetPtz(VideoProviderKind.Onvif)!;
        var ready = await ptz.PrepareAsync(id, info, Within(15_000));
        Assert.True(ready.Connected && ready.PtzCapable, ready.ToString());
        // 준비가 끝나면 위치 · 줌을 배경에서 한 번 읽는다(드래그 경로 밖). 읽기가 끝난 뒤의 드래그를 재려면 기다린다.
        if (waitForStatus)
            Assert.True(await StateRecorder.WaitUntilAsync(() => camera.Count("GetStatus") >= 1, TimeSpan.FromSeconds(5)), "준비 뒤 위치 읽기가 없었다");
        await Task.Delay(150);
        return (ptz, info);
    }

    [Fact]
    public async Task should_send_exactly_one_relative_move_and_no_other_call_when_video_is_dragged()
    {
        // Arrange — 일반 상대 · 절대 · 연속 공간이 있고 위치를 알려주는 카메라(TRUEN 실측과 같은 구성), 광각(zoom 0)
        await using var camera = new FakeOnvifCamera { HasAbsolute = true, Status = (0.1, 0.2, 0) };
        var (ptz, _) = await PrepareAsync(camera, "drag-1", waitForStatus: true);
        int before = camera.Requests.Count;

        // Act — 화면 너비의 1/4 오른쪽 · 높이의 1/10 위로
        var sw = Stopwatch.StartNew();
        var outcome = await ptz.DragMoveAsync("drag-1", 0.25, -0.1, 16d / 9d, Within(5000));
        sw.Stop();
        await Task.Delay(300);   // 뒤따르는 호출이 없는지 볼 시간

        // Assert — ONVIF 호출은 RelativeMove 하나뿐(GetStatus · GetNode · GetProfiles 없음), 일반 상대 공간, 방향 · 크기
        Assert.True(outcome.Sent, outcome.ToString());
        Assert.Equal(PtzDragMoveKind.RelativeGeneric, outcome.Kind);
        var added = camera.Requests.Skip(before).ToList();
        var only = Assert.Single(added);
        Assert.Contains("RelativeMove", only.Body);
        var pt = FakeOnvifCamera.PanTiltOf(only.Body);
        Assert.NotNull(pt);
        Assert.Equal(FakeOnvifCamera.RelGenericUri, pt!.Value.Space);
        Assert.Equal(0.25 * 60d / 360d * 2d, pt.Value.X, 4);          // 화각 60° 의 1/4 = 15° → 폭 2 가 360°
        double vfov = PtzDragMath.VerticalFovDeg(60, 16d / 9d);
        Assert.Equal(0.1 * vfov / 180d * 2d, pt.Value.Y, 4);          // 위로 끌면 +
        Assert.DoesNotContain("<Zoom", only.Body.Replace("tt:", string.Empty));   // 줌은 건드리지 않는다
        _output.WriteLine($"drag → RelativeMove in {sw.ElapsedMilliseconds} ms · x={pt.Value.X:F4} y={pt.Value.Y:F4}");
    }

    [Fact]
    public async Task should_move_less_for_same_drag_when_camera_is_zoomed_in()
    {
        await using var wide = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 0) };
        await using var tele = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 1) };
        var (ptzWide, _) = await PrepareAsync(wide, "wide", waitForStatus: true);
        var (ptzTele, _) = await PrepareAsync(tele, "tele", waitForStatus: true);

        await ptzWide.DragMoveAsync("wide", 0.5, 0, 16d / 9d, Within(5000));
        await ptzTele.DragMoveAsync("tele", 0.5, 0, 16d / 9d, Within(5000));

        double w = FakeOnvifCamera.PanTiltOf(wide.PtzCommands.Single(c => c.Op == "RelativeMove").Body)!.Value.X;
        double t = FakeOnvifCamera.PanTiltOf(tele.PtzCommands.Single(c => c.Op == "RelativeMove").Body)!.Value.X;
        Assert.True(t > 0 && t < w / 20, $"wide={w} tele={t}");   // 30배 망원에서는 같은 드래그가 훨씬 적게 움직인다
    }

    [Fact]
    public async Task should_use_fov_space_when_camera_lists_it_before_generic_space()
    {
        // Arrange — 화각 상대 공간이 노드 목록의 첫 항목(첫 항목을 일반 공간으로 잘못 쓰면 단위가 뒤바뀐다)
        await using var camera = new FakeOnvifCamera { HasRelativeFov = true };
        var (ptz, _) = await PrepareAsync(camera, "fov-1", waitForStatus: false);

        // Act
        var outcome = await ptz.DragMoveAsync("fov-1", -0.25, 0.5, 16d / 9d, Within(5000));

        // Assert — ±1 = 화면 가장자리: 비율 × 2, 아래로 끌면 −
        Assert.Equal(PtzDragMoveKind.RelativeFov, outcome.Kind);
        var pt = FakeOnvifCamera.PanTiltOf(camera.PtzCommands.Single().Body)!.Value;
        Assert.Equal(FakeOnvifCamera.RelFovUri, pt.Space);
        Assert.Equal(-0.5, pt.X, 4);
        Assert.Equal(-1.0, pt.Y, 4);
    }

    [Fact]
    public async Task should_use_absolute_move_from_cached_position_when_camera_has_no_relative_space()
    {
        await using var camera = new FakeOnvifCamera { HasRelativeGeneric = false, HasAbsolute = true, Status = (0.4, -0.3, 0) };
        var (ptz, _) = await PrepareAsync(camera, "abs-1", waitForStatus: true);
        int statusBefore = camera.Count("GetStatus");

        var first = await ptz.DragMoveAsync("abs-1", 0.5, 0, 16d / 9d, Within(5000));
        var second = await ptz.DragMoveAsync("abs-1", 0.5, 0, 16d / 9d, Within(5000));

        // Assert — 기억한 위치 + 이동량, 두 번째는 추적한 위치에서 이어 간다. 드래그 때문에 위치를 다시 읽지 않는다.
        Assert.Equal(PtzDragMoveKind.Absolute, first.Kind);
        Assert.Equal(PtzDragMoveKind.Absolute, second.Kind);
        var moves = camera.PtzCommands.Where(c => c.Op == "AbsoluteMove").Select(c => FakeOnvifCamera.PanTiltOf(c.Body)!.Value).ToList();
        Assert.Equal(2, moves.Count);
        double step = 30d / 360d * 2d;
        Assert.Equal(0.4 + step, moves[0].X, 4);
        Assert.Equal(0.4 + (2 * step), moves[1].X, 4);
        Assert.Equal(-0.3, moves[1].Y, 4);
        Assert.Equal(FakeOnvifCamera.AbsGenericUri, moves[0].Space);
        Assert.Equal(statusBefore, camera.Count("GetStatus"));
    }

    [Fact]
    public async Task should_pulse_continuous_move_then_stop_when_position_cannot_be_read()
    {
        // Arrange — GetStatus 를 모르는 카메라(위치 · 줌을 알 수 없다) → 옛 방식(길이 비례 시간 뒤 정지)으로 대체
        await using var camera = new FakeOnvifCamera();
        var (ptz, _) = await PrepareAsync(camera, "pulse-1", waitForStatus: false);

        var outcome = await ptz.DragMoveAsync("pulse-1", 0.2, 0, 16d / 9d, Within(5000));

        Assert.True(outcome.Sent);
        Assert.Equal(PtzDragMoveKind.ContinuousPulse, outcome.Kind);
        Assert.Equal(new[] { "ContinuousMove", "Stop" }, camera.PtzCommands.Select(c => c.Op));
        Assert.Contains("PT2S", camera.PtzCommands[0].Body);   // 정지가 못 나가도 카메라가 2초 뒤 스스로 멈춘다
    }

    [Fact]
    public async Task should_keep_only_the_last_drag_when_ten_drags_arrive_while_camera_is_busy()
    {
        // Arrange — 응답이 300 ms 걸리는 카메라: 첫 드래그가 나가 있는 동안 나머지 9개가 몰린다
        await using var camera = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 0) };
        var (ptz, _) = await PrepareAsync(camera, "burst-1", waitForStatus: true);
        camera.Delay = TimeSpan.FromMilliseconds(300);

        // Act
        var tasks = new List<Task<Providers.Onvif.PtzDragOutcome>>();
        for (int i = 1; i <= 10; i++)
        {
            tasks.Add(ptz.DragMoveAsync("burst-1", i * 0.05, 0, 16d / 9d, Within(10_000)));
            await Task.Delay(10);
        }
        var outcomes = await Task.WhenAll(tasks);
        await Task.Delay(500);

        // Assert — 쌓이지 않는다: 이미 나간 첫 번째 + 마지막 하나만. 중간 8개는 버려졌다(superseded).
        var moves = camera.PtzCommands.Where(c => c.Op == "RelativeMove").Select(c => FakeOnvifCamera.PanTiltOf(c.Body)!.Value.X).ToList();
        Assert.Equal(2, moves.Count);
        Assert.Equal(0.05 * 60d / 360d * 2d, moves[0], 4);
        Assert.Equal(0.50 * 60d / 360d * 2d, moves[1], 4);
        Assert.Equal(2, outcomes.Count(o => o.Sent));
        Assert.Equal(8, outcomes.Count(o => o.Reason == "superseded"));
        Assert.True(outcomes[^1].Sent, "마지막 드래그가 나가지 않았다");
    }

    [Fact]
    public async Task should_drop_waiting_drag_when_stop_arrives_first()
    {
        // Arrange — 느린 카메라: 드래그 1 이 나가는 중, 드래그 2 가 대기 중일 때 정지
        await using var camera = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 0) };
        var (ptz, _) = await PrepareAsync(camera, "stop-1", waitForStatus: true);
        camera.Delay = TimeSpan.FromMilliseconds(300);

        var first = ptz.DragMoveAsync("stop-1", 0.1, 0, 16d / 9d, Within(10_000));
        await Task.Delay(60);
        var second = ptz.DragMoveAsync("stop-1", 0.4, 0, 16d / 9d, Within(10_000));
        await Task.Delay(30);
        await ptz.StopAsync("stop-1", Within(10_000));
        var outcomes = await Task.WhenAll(first, second);
        await Task.Delay(400);

        // Assert — 정지 우선: 대기 중이던 드래그 2 는 카메라에 가지 않는다. 도착 순서 = 이동 1 → 정지.
        Assert.True(outcomes[0].Sent);
        Assert.False(outcomes[1].Sent);
        Assert.Equal(new[] { "RelativeMove", "Stop" }, camera.PtzCommands.Select(c => c.Op));
    }

    [Fact]
    public async Task should_resend_move_while_held_and_send_nothing_after_stop_when_button_is_released()
    {
        // Arrange
        await using var camera = new FakeOnvifCamera();
        var (ptz, _) = await PrepareAsync(camera, "hold-1", waitForStatus: false);

        // Act — 2.4초 누름(이동 1 + 유지 재전송 2) → 뗌(정지) → 1.5초 더 본다
        Assert.True(await ptz.ContinuousMoveAsync("hold-1", 0.5, 0, 0, Within(5000)));
        await Task.Delay(2400);
        int movesWhileHeld = camera.Count("ContinuousMove");
        await ptz.StopAsync("hold-1", Within(5000));
        await Task.Delay(1500);

        // Assert — 누르는 동안 1초마다 재전송(PT2S 안전 제한에 걸려 멈추지 않게), 뗀 뒤에는 재전송 없음, 마지막 명령은 정지
        Assert.InRange(movesWhileHeld, 3, 4);
        Assert.Equal(movesWhileHeld, camera.Count("ContinuousMove"));
        Assert.Equal("Stop", camera.PtzCommands[^1].Op);
        Assert.Equal(1, camera.PtzCommands.Count(c => c.Op == "Stop"));
        Assert.All(camera.PtzCommands.Where(c => c.Op == "ContinuousMove"), c => Assert.Contains("PT2S", c.Body));
    }

    [Fact]
    public async Task should_not_reach_camera_when_drag_vector_is_zero()
    {
        await using var camera = new FakeOnvifCamera { HasAbsolute = true, Status = (0, 0, 0) };
        var (ptz, _) = await PrepareAsync(camera, "zero-1", waitForStatus: true);

        var outcome = await ptz.DragMoveAsync("zero-1", 0, 0, 16d / 9d, Within(5000));

        Assert.False(outcome.Sent);
        Assert.Empty(camera.PtzCommands);
    }

    [Fact]
    public async Task should_report_not_ready_when_camera_was_never_prepared()
    {
        var registry = CameraProviderRegistry.CreateDefault(new TestLog());

        var outcome = await registry.GetPtz(VideoProviderKind.Onvif)!.DragMoveAsync("nobody", 0.2, 0, 1.5, Within(2000));

        Assert.False(outcome.Sent);
        Assert.Equal("not-ready", outcome.Reason);
    }
}

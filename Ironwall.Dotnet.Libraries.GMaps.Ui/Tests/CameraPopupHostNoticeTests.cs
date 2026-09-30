using System.Reflection;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 지도 하단 호스트 안내(camera-popup-modes FR-25 · T-09 K3): 오버레이가 하나도 없어도 일시 중지는 계속 보이고
/// [다시 시작]이 감시자를 되살린다. 사용할 수 없음은 토스트 한 번. 정상으로 돌아오면 지운다.
/// </summary>
public class CameraPopupHostNoticeTests
{
    private static (MapViewModel Vm, FakeHost Host) Create(CameraPopupHostState state = CameraPopupHostState.Running)
    {
        // 무거운 생성자를 건너뛴다 — 호스트는 해석 캐시에 끼운다.
        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        var host = new FakeHost { State = state };
        Set(vm, "_cameraPopupHost", host);
        Set(vm, "_cameraPopupHostResolved", true);
        Set(vm, "_cameraPopupToastMessage", string.Empty);
        Set(vm, "_cameraPopupHostNoticeMessage", string.Empty);
        return (vm, host);
    }

    private static void Set(object target, string field, object? value)
        => typeof(MapViewModel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void Start(MapViewModel vm)
        => typeof(MapViewModel).GetMethod("StartCameraPopupHostNotice", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null);

    private static void Stop(MapViewModel vm)
        => typeof(MapViewModel).GetMethod("StopCameraPopupHostNotice", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null);

    [Fact]
    public void should_show_persistent_notice_with_restart_when_host_is_suspended_and_no_overlay_is_open()
    {
        // Arrange
        var (vm, host) = Create();
        Start(vm);
        Assert.False(vm.IsCameraPopupHostNoticeVisible);

        // Act — 60초에 3번 넘게 죽어 일시 중지
        host.Raise(CameraPopupHostState.Restarting, CameraPopupHostState.Suspended);

        // Assert — 계속 떠 있는 안내(토스트가 아니다)
        Assert.True(vm.IsCameraPopupHostNoticeVisible);
        Assert.Equal("영상 기능 일시 중지", vm.CameraPopupHostNoticeMessage);
        Assert.False(vm.IsCameraPopupToastVisible);
    }

    [Fact]
    public void should_restart_host_and_clear_notice_when_restart_is_pressed_and_host_runs_again()
    {
        // Arrange
        var (vm, host) = Create();
        Start(vm);
        host.Raise(CameraPopupHostState.Restarting, CameraPopupHostState.Suspended);

        // Act — [다시 시작]
        vm.RestartCameraPopupHost();

        // Assert — 감시자에 재시작을 한 번 요청, 안내는 상태가 바뀔 때까지 남는다
        Assert.Equal(1, host.Restarts);
        Assert.True(vm.IsCameraPopupHostNoticeVisible);

        host.Raise(CameraPopupHostState.Suspended, CameraPopupHostState.Starting);
        Assert.False(vm.IsCameraPopupHostNoticeVisible);
        host.Raise(CameraPopupHostState.Starting, CameraPopupHostState.Running);
        Assert.False(vm.IsCameraPopupHostNoticeVisible);
    }

    [Fact]
    public void should_show_notice_on_activation_when_host_was_already_suspended()
    {
        // Arrange — 지도가 뜨기 전에 이미 일시 중지
        var (vm, _) = Create(CameraPopupHostState.Suspended);

        // Act
        Start(vm);

        // Assert
        Assert.True(vm.IsCameraPopupHostNoticeVisible);
        Assert.Equal(CameraPopupHostNotices.Suspended, vm.CameraPopupHostNoticeMessage);
    }

    [Fact]
    public void should_toast_once_without_persistent_notice_when_host_is_unavailable()
    {
        // Arrange
        var (vm, host) = Create();
        Start(vm);

        // Act — 실행 파일 없음
        host.Raise(CameraPopupHostState.NotStarted, CameraPopupHostState.Unavailable);

        // Assert — 토스트 한 번, 계속 떠 있는 안내는 없다
        Assert.True(vm.IsCameraPopupToastVisible);
        Assert.Equal(CameraPopupHostNotices.Unavailable, vm.CameraPopupToastMessage);
        Assert.False(vm.IsCameraPopupHostNoticeVisible);

        // 같은 상태가 또 알려져도 다시 띄우지 않는다
        Set(vm, "_isCameraPopupToastVisible", false);
        host.Raise(CameraPopupHostState.Unavailable, CameraPopupHostState.Unavailable);
        Assert.False(vm.IsCameraPopupToastVisible);
    }

    [Theory]
    [InlineData(CameraPopupHostState.Running)]
    [InlineData(CameraPopupHostState.Starting)]
    [InlineData(CameraPopupHostState.Restarting)]
    [InlineData(CameraPopupHostState.NotStarted)]
    public void should_show_nothing_when_host_is_not_stopped(CameraPopupHostState state)
    {
        var (vm, host) = Create();
        Start(vm);

        host.Raise(CameraPopupHostState.Suspended, state);

        Assert.False(vm.IsCameraPopupHostNoticeVisible);
        Assert.False(vm.IsCameraPopupToastVisible);
    }

    [Fact]
    public void should_stop_following_host_state_when_map_is_deactivated()
    {
        // Arrange
        var (vm, host) = Create();
        Start(vm);
        Assert.Equal(1, host.Subscribers);

        // Act
        Stop(vm);
        host.Raise(CameraPopupHostState.Restarting, CameraPopupHostState.Suspended);

        // Assert — 구독이 풀렸다(안내가 바뀌지 않는다), 다시 켜면 현재 상태를 반영한다
        Assert.Equal(0, host.Subscribers);
        Assert.False(vm.IsCameraPopupHostNoticeVisible);
        host.State = CameraPopupHostState.Suspended;
        Start(vm);
        Assert.True(vm.IsCameraPopupHostNoticeVisible);
    }

    [Fact]
    public void should_not_throw_when_restart_is_pressed_and_host_restart_fails()
    {
        var (vm, host) = Create(CameraPopupHostState.Suspended);
        Start(vm);
        host.ThrowOnRestart = true;

        var ex = Record.Exception(vm.RestartCameraPopupHost);   // 팝업 쪽 실패가 GIS 로 새지 않는다(FR-27)

        Assert.Null(ex);
    }

    /// <summary>상태만 흉내 내는 가짜 호스트(감시자 없이).</summary>
    private sealed class FakeHost : ICameraPopupHost
    {
        private EventHandler<CameraPopupHostStateChangedEventArgs>? _stateChanged;

        public CameraPopupHostState State { get; set; }
        public string? StateReason => null;
        public int Restarts { get; private set; }
        public bool ThrowOnRestart { get; set; }
        public int Subscribers => _stateChanged?.GetInvocationList().Length ?? 0;

        public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }

        public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived { add { } remove { } }

        public void Raise(CameraPopupHostState old, CameraPopupHostState now)
        {
            State = now;
            _stateChanged?.Invoke(this, new CameraPopupHostStateChangedEventArgs(old, now, "test", null, null));
        }

        public void Start() { }

        public void Restart()
        {
            if (ThrowOnRestart) throw new InvalidOperationException("restart failed");
            Restarts++;
        }

        public IFrameSource? OpenOverlay(OverlayStreamRequest request) => null;
        public void CloseOverlay(string streamId) { }
        public void OpenEventWindow(OpenEventWindow request) { }
        public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
        public void BringEventWindowToFront(string eventKey) { }
        public void SetTheme(string theme) { }
        public void SendPtz(PtzCommand command) { }
        public void Dispose() { }
    }
}

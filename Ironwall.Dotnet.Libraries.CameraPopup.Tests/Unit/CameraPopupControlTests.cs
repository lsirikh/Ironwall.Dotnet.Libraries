using System.Collections.Concurrent;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// GIS 쪽 조작 창구(<see cref="CameraPopupControl"/>, T-02) — 절대 기다리지 않고 던지지 않는다(FR-28):
/// 호스트 없음 → 즉시 실패 · 응답 → 짝 맞춰 완료 · 늦음 → 시간 초과 · 재시작 → 그 순간 실패 · 합치기 키(PTZ ↔ 포커스 분리).
/// </summary>
public class CameraPopupControlTests
{
    private sealed class FakeHost : ICameraPopupHost
    {
        public CameraPopupHostState State { get; set; } = CameraPopupHostState.Running;
        public string? StateReason => null;
        public bool AcceptSends { get; set; } = true;
        public ConcurrentQueue<(IIpcMessage Message, string? Key)> Sent { get; } = new();

        public event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged;
        public event EventHandler<CameraPopupStatusEventArgs>? StatusReceived;

        public void RaiseStatus(IIpcMessage message) => StatusReceived?.Invoke(this, new CameraPopupStatusEventArgs(message));

        public void ChangeState(CameraPopupHostState state)
        {
            var old = State;
            State = state;
            StateChanged?.Invoke(this, new CameraPopupHostStateChangedEventArgs(old, state, "test", null, null));
        }

        public bool Send(IIpcMessage message, string? coalesceKey = null)
        {
            if (!AcceptSends) return false;
            Sent.Enqueue((message, coalesceKey));
            return true;
        }

        public void Start() { }
        public void Restart() { }
        public IFrameSource? OpenOverlay(OverlayStreamRequest request) => null;
        public void CloseOverlay(string streamId) { }
        public void OpenEventWindow(OpenEventWindow request) { }
        public void CloseEventWindow(string eventKey, EventWindowCloseReason reason, bool returnHome) { }
        public void BringEventWindowToFront(string eventKey) { }
        public void SetTheme(string theme) { }
        public void SendPtz(PtzCommand command) => Send(command, "ptz:" + command.CameraId);
        public void Dispose() { }
    }

    private static readonly VideoProviderInfo Provider = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Username = "u", Password = "secret" };

    [Fact]
    public async Task should_fail_immediately_with_host_unavailable_when_host_suspended()
    {
        var host = new FakeHost { State = CameraPopupHostState.Suspended };
        using var control = new CameraPopupControl(host);

        var response = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GetPresets, CameraId = "c1", Provider = Provider });

        Assert.False(response.Success);
        Assert.Equal(CameraErrorCodes.HostUnavailable, response.ErrorCode);
        Assert.Empty(host.Sent);
    }

    [Fact]
    public async Task should_complete_request_when_host_responds_with_same_id()
    {
        // Arrange
        var host = new FakeHost();
        using var control = new CameraPopupControl(host);

        // Act
        var pending = control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GetPresets, CameraId = "c1", Provider = Provider });
        var sent = Assert.IsType<CameraRequest>(Assert.Single(host.Sent).Message);
        Assert.False(string.IsNullOrEmpty(sent.RequestId));
        host.RaiseStatus(new CameraResponse { RequestId = "someone-else", Success = true });
        host.RaiseStatus(new CameraResponse { RequestId = sent.RequestId, Kind = sent.Kind, CameraId = "c1", Success = true, Presets = new() { new() { Token = "1" } } });
        var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(response.Success);
        Assert.Equal("1", Assert.Single(response.Presets!).Token);
        Assert.Equal(0, control.PendingCount);
    }

    [Fact]
    public async Task should_time_out_when_host_never_responds()
    {
        var host = new FakeHost();
        using var control = new CameraPopupControl(host);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 호스트 제한 100 ms + 여유(ResponseGrace) 뒤 시간 초과
        var response = await control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.GotoHome, CameraId = "c1", Provider = Provider, TimeoutMs = 100 })
                                    .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(response.Success);
        Assert.Equal(CameraErrorCodes.Timeout, response.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8));
        Assert.Equal(0, control.PendingCount);
    }

    [Fact]
    public async Task should_fail_pending_with_host_restarted_when_host_restarts()
    {
        var host = new FakeHost();
        using var control = new CameraPopupControl(host);
        var pending = control.RequestAsync(new CameraRequest { Kind = CameraRequestKind.PreparePtz, CameraId = "c1", Provider = Provider });

        host.ChangeState(CameraPopupHostState.Restarting);
        var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(response.Success);
        Assert.Equal(CameraErrorCodes.HostRestarted, response.ErrorCode);
    }

    [Fact]
    public void should_use_separate_coalesce_keys_for_ptz_and_focus_when_sending_moments()
    {
        // 팝업 닫기의 PTZ 정지 · 포커스 정지가 합치기 대기열에서 서로를 지우면 안 된다.
        var host = new FakeHost();
        using var control = new CameraPopupControl(host);

        Assert.True(control.Move("c1", Provider, 0.4, 0, 0));
        Assert.True(control.Stop("c1", Provider));
        Assert.True(control.Focus("c1", Provider, 0));

        var sent = host.Sent.ToArray();
        Assert.Equal("ptz:c1", sent[0].Key);
        Assert.Equal("ptz:c1", sent[1].Key);
        Assert.Equal("focus:c1", sent[2].Key);
        Assert.Equal(PtzOperation.Stop, Assert.IsType<PtzCommand>(sent[1].Message).Operation);
        Assert.Equal(0, Assert.IsType<PtzFocusCommand>(sent[2].Message).Direction);
        Assert.Same(Provider, Assert.IsType<PtzCommand>(sent[0].Message).Provider);   // 재시작 직후 호스트가 준비할 수 있게 접속 정보 동봉
    }

    [Fact]
    public void should_not_send_moments_when_host_not_running()
    {
        var host = new FakeHost { State = CameraPopupHostState.Restarting };
        using var control = new CameraPopupControl(host);

        Assert.False(control.Move("c1", Provider, 0.4, 0, 0));
        Assert.False(control.Focus("c1", Provider, 1));
        Assert.Empty(host.Sent);
    }
}

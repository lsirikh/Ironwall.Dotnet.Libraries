using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

/****************************************************************************
   Purpose      : ONVIF PTZ · 영상 옵션 제공자(PRD camera-popup-modes FR-17/18 · FR-22 · FR-26)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// <see cref="IPtzController"/>(T-01 정지 우선 게이트 · 이동 유지 재전송 · 준비 병렬화)를 제공자 모양으로 감싼다.
/// 모든 호출은 <paramref name="ct"/>(호출 쪽 제한 시간) 안에 돌아온다 — WCF 호출 자체는 끊지 못하므로 결과만 버린다(FR-26).
/// </summary>
public sealed class OnvifPtzProvider : ICameraPtzProvider
{
    private readonly IPtzController _controller;

    public OnvifPtzProvider(IPtzController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    /// <summary>제공자 정보 → ONVIF 연결 모델(순수). 포트가 없으면 80.</summary>
    public static ConnectionModel ToConnection(VideoProviderInfo info) => new()
    {
        IpAddress = info.Host,
        PortOnvif = info.Port > 0 ? info.Port : 80,
        Username = info.Username,
        Password = info.Password,
    };

    public async Task<PtzReadiness> PrepareAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cameraId) || info is null || string.IsNullOrWhiteSpace(info.Host))
            return new PtzReadiness(false, false, false);
        var capable = await Bounded(_controller.EnsureReadyAsync(cameraId, ToConnection(info), ct), ct, false).ConfigureAwait(false);
        return new PtzReadiness(_controller.IsReady(cameraId), capable, _controller.IsImagingCapable(cameraId));
    }

    public bool IsPrepared(string cameraId) => _controller.IsReady(cameraId);

    public Task<bool> ContinuousMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct)
        => Bounded(_controller.ContinuousMoveAsync(cameraId, pan, tilt, zoom, ct), ct, false);

    public Task StopAsync(string cameraId, CancellationToken ct)
        => Bounded(StopCore(cameraId, ct), ct, true);

    private async Task<bool> StopCore(string cameraId, CancellationToken ct)
    {
        await _controller.StopAsync(cameraId, ct).ConfigureAwait(false);
        return true;
    }

    public Task<IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct)
        => Bounded(_controller.GetPresetsAsync(cameraId, ct), ct, null);

    public Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct)
        => Bounded(_controller.GotoPresetAsync(cameraId, presetToken, ct), ct, false);

    public Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct)
        => Bounded(_controller.SetPresetAsync(cameraId, presetName, ct), ct, false);

    public Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct)
        => Bounded(_controller.RemovePresetAsync(cameraId, presetToken, ct), ct, false);

    public Task<bool> SetHomeAsync(string cameraId, CancellationToken ct)
        => Bounded(_controller.SetHomePresetAsync(cameraId, ct), ct, false);

    public Task<bool> GotoHomeAsync(string cameraId, CancellationToken ct)
        => Bounded(_controller.GotoHomePresetAsync(cameraId, ct), ct, false);

    public bool IsImagingCapable(string cameraId) => _controller.IsImagingCapable(cameraId);

    public Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct)
        => Bounded(_controller.GetImagingAsync(cameraId, ct), ct, null);

    public Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct)
        => Bounded(_controller.SetIrCutFilterAsync(cameraId, mode, ct), ct, false);

    public Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct)
        => Bounded(_controller.SetAutoFocusAsync(cameraId, auto, ct), ct, false);

    public Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct)
        => Bounded(_controller.StartFocusAsync(cameraId, direction, ct), ct, false);

    public Task StopFocusAsync(string cameraId, CancellationToken ct)
        => Bounded(StopFocusCore(cameraId, ct), ct, true);

    private async Task<bool> StopFocusCore(string cameraId, CancellationToken ct)
    {
        await _controller.StopFocusAsync(cameraId, ct).ConfigureAwait(false);
        return true;
    }

    public void Release(string cameraId) => _controller.Release(cameraId);

    /// <summary>
    /// 호출 쪽 제한 시간 안에 돌아온다(FR-26). 시간이 넘으면 <paramref name="fallback"/> — 뒤에서 끝나는 원래 작업의
    /// 예외는 관찰해 버린다(미관찰 예외가 호스트를 흔들지 않게).
    /// </summary>
    internal static async Task<T> Bounded<T>(Task<T> task, CancellationToken ct, T fallback)
    {
        try
        {
            return await task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            return fallback;
        }
        catch (Exception)
        {
            // 제공자 경계(FR-27) — 컨트롤러는 스스로 로그를 남기고 false 를 돌려주지만, 예상 밖 예외도 여기서 멈춘다.
            return fallback;
        }
    }
}

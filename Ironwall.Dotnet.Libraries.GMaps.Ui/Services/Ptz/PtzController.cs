using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Ironwall.Dotnet.Libraries.OnvifSolution.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Services;
using OnvifImaging = Ironwall.Dotnet.Libraries.OnvifSolution.Imaging;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Ptz;

/****************************************************************************
   Purpose      : 팝업 PTZ 제어 단일 소유 앱서비스 구현 (CameraPopup_PTZ_Control)
   Created By   : Claude Code
   Created On   : 2026-06-24
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="IPtzController"/> 구현. cameraId별 ONVIF 자원(PtzClient·profileToken·GetNode space)을
/// 캐시·직렬화한다. PRD FR-PTZCTL-01~03 / FR-WRAP-01 / NFR-THREAD-01.
///
/// 스레드 안전(I-05): 카메라 맵=ConcurrentDictionary, 카메라별 SemaphoreSlim(1,1)로 PtzClient 직렬 호출
/// (동일 인스턴스 병렬 호출 금지). 다른 cameraId는 병렬. 모든 ONVIF 호출은 ConfigureAwait(false).
/// 변환/클램프는 카메라 GetNode space(XRange/YRange) 진실원 — 고정 ±1.0 금지(NFR-SAFE-01).
/// </summary>
public sealed class PtzController : IPtzController
{
    private readonly IOnvifService _onvif;
    private readonly ILogService? _log;
    private readonly ConcurrentDictionary<int, CamCtx> _ctx = new();

    public PtzController(IOnvifService onvif, ILogService? log = null)
    {
        _onvif = onvif ?? throw new ArgumentNullException(nameof(onvif));
        _log = log;
    }

    /*──────────────── 내부 상태 ────────────────*/

    private sealed class CamCtx
    {
        public ICameraOnvifModel? Model;
        public string ProfileToken = string.Empty;
        public SpaceInfo? Spaces;
        public string? VsToken;            // 영상 옵션(Imaging)용 VideoSourceToken
        public bool ImagingPossible;       // IsImagingPossible + ImagingClient + VsToken
        public readonly SemaphoreSlim Gate = new(1, 1);
        public volatile bool Busy;
    }

    /// <summary>GetNode로 읽은 카메라 좌표 space(변환/클램프 진실원). URI는 직렬화에 동봉.</summary>
    private sealed record SpaceInfo(
        bool HasRel, string? RelPtUri, double RelXMin, double RelXMax, double RelYMin, double RelYMax,
        bool HasAbs, string? AbsPtUri, double AbsXMin, double AbsXMax, double AbsYMin, double AbsYMax,
        string? AbsZoomUri, double AbsZMin, double AbsZMax,
        bool HasRelZoom, string? RelZoomUri, double RelZMin, double RelZMax);

    /*──────────────── 공개 API ────────────────*/

    public async Task<bool> EnsureReadyAsync(int cameraId, IConnectionModel conn, CancellationToken ct = default)
    {
        if (conn == null) return false;
        var ctx = _ctx.GetOrAdd(cameraId, _ => new CamCtx());
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (ctx.Model?.PtzClient == null)
                {
                    var model = await _onvif.InitializeFullAsync(conn, ct).ConfigureAwait(false);
                    if (model?.PtzClient == null)
                    {
                        _log?.Warning($"[PTZ] PtzClient 없음 cam={cameraId} — 비PTZ 카메라이거나 ONVIF 연결/인증 실패(포트/계정 확인).");
                        return false;
                    }
                    ctx.Model = model;
                    ctx.ProfileToken = model.CameraMedia?.Token ?? string.Empty;
                    // 영상 옵션(Imaging)용 VideoSourceToken — 첫 프로파일의 VideoSourceConfiguration.
                    ctx.VsToken = model.Profiles?.FirstOrDefault()?.VideoSourceConfiguration?.SourceToken;
                    ctx.ImagingPossible = model.IsImagingPossible && model.ImagingClient != null && !string.IsNullOrEmpty(ctx.VsToken);
                }
                ctx.Spaces ??= await LoadSpacesAsync(ctx).ConfigureAwait(false);
                var cap = IsCapable(ctx);
                if (!cap)
                    _log?.Warning($"[PTZ] capable=false cam={cameraId} (IsPtzPossible={ctx.Model?.IsPtzPossible}, GetNode space={(ctx.Spaces == null ? "로드실패/없음" : "로드됨")}).");
                return cap;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] EnsureReady 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public bool IsPtzCapable(int cameraId) => _ctx.TryGetValue(cameraId, out var c) && IsCapable(c);

    public bool IsBusy(int cameraId) => _ctx.TryGetValue(cameraId, out var c) && c.Busy;

    public async Task<bool> RelativeMoveByPixelAsync(int cameraId, double dx, double dy,
        double imageW, double imageH, double sensitivity = 1.0, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { HasRel: true } sp || ctx.Model?.PtzClient == null)
            return false;

        var (pan, tilt) = PtzCoordinateMath.PixelDeltaToRelative(
            dx, dy, imageW, imageH, sp.RelXMin, sp.RelXMax, sp.RelYMin, sp.RelYMax, sensitivity);

        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var vec = PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildPanTilt(pan, tilt, sp.RelPtUri));
                await ctx.Model.PtzClient.RelativeMoveAsync(ctx.ProfileToken, vec, null).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] RelativeMove 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> ContinuousMoveAsync(int cameraId, double panVel, double tiltVel, double zoomVel, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Model?.PtzClient == null) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // ContinuousMove(속도). Relative 미지원 카메라도 대부분 지원. 카메라는 Stop/타임아웃까지 이동.
                var speed = new PtzSpeedDto
                {
                    PanTilt = new Vector2DDto { X = (float)panVel, Y = (float)tiltVel },
                    Zoom = new Vector1DDto { X = (float)zoomVel },
                };
                await _onvif.MovePTZ(ctx.Model.PtzClient, speed, ctx.ProfileToken, "PT10S").ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] ContinuousMove 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> RelativeZoomAsync(int cameraId, double zoomDelta, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { HasRelZoom: true } sp || ctx.Model?.PtzClient == null)
            return false;

        var z = PtzCoordinateMath.ClampToRange(zoomDelta, sp.RelZMin, sp.RelZMax);
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var vec = PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildZoom(z, sp.RelZoomUri));
                await ctx.Model.PtzClient.RelativeMoveAsync(ctx.ProfileToken, vec, null).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] RelativeZoom 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> AbsoluteMoveAsync(int cameraId, double pan, double tilt, double zoom, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { HasAbs: true } sp || ctx.Model?.PtzClient == null)
            return false;

        var cpan = PtzCoordinateMath.ClampToRange(pan, sp.AbsXMin, sp.AbsXMax);
        var ctilt = PtzCoordinateMath.ClampToRange(tilt, sp.AbsYMin, sp.AbsYMax);
        double? czoom = sp.AbsZoomUri != null ? PtzCoordinateMath.ClampToRange(zoom, sp.AbsZMin, sp.AbsZMax) : null;

        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            ctx.Busy = true;
            try
            {
                var vec = PtzVectorMapper.ToPtzVector(
                    PtzVectorMapper.BuildAbsolute(cpan, ctilt, czoom, sp.AbsPtUri, sp.AbsZoomUri));
                await ctx.Model.PtzClient.AbsoluteMoveAsync(ctx.ProfileToken, vec, null).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Busy = false; ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] AbsoluteMove 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<PtzPosition?> GetStatusAsync(int cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Model?.PtzClient == null) return null;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var st = await ctx.Model.PtzClient.GetStatusAsync(ctx.ProfileToken).ConfigureAwait(false);
                var p = st?.Position;
                if (p?.PanTilt == null) return null;
                return new PtzPosition(p.PanTilt.x, p.PanTilt.y, p.Zoom?.x ?? 0d, p.PanTilt.space, p.Zoom?.space);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] GetStatus 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
    }

    public async Task<(double Bearing, double Zoom100)?> GetFovAsync(int cameraId, CancellationToken ct = default)
    {
        var pos = await GetStatusAsync(cameraId, ct).ConfigureAwait(false);
        if (pos == null || !_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { } sp) return null;

        // Pan: ONVIF abs space([xMin,xMax], 보통 [-1,1], 중앙=home)를 home 기준 ±180° 오프셋 → 0~360°.
        // (UpdateFOV가 DetectionBearing = BaseBearing + ConvertPanToBearing(pan)로 쓰므로 home=0 오프셋이어야 함.)
        var xr = sp.AbsXMax - sp.AbsXMin;
        var panNorm = Math.Abs(xr) > 1e-9 ? (2.0 * (pos.Pan - sp.AbsXMin) / xr - 1.0) : pos.Pan;   // [-1,1]
        var bearing = panNorm * 180.0;
        if (bearing < 0) bearing += 360.0;

        // Zoom: ONVIF abs zoom([zMin,zMax], 보통 [0,1]) → FOV 기대 스케일 0~100(100=NVR 최대줌).
        var zr = sp.AbsZMax - sp.AbsZMin;
        var zoom100 = Math.Abs(zr) > 1e-9 ? Math.Clamp((pos.Zoom - sp.AbsZMin) / zr, 0.0, 1.0) * 100.0 : 0.0;

        return (bearing, zoom100);
    }

    public async Task StopAsync(int cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Model?.PtzClient == null) return;
        try
        {
            // C1: PtzClient(WCF 채널) 동일 인스턴스 병렬 호출 금지(I-05) — Move와 동일 Gate로 직렬화.
            // (1회성 Relative/Absolute 이동이라 Stop이 직전 이동 뒤로 큐잉돼도 지연 짧음. 진행 중 이동
            //  즉시 중단이 필요하면 별도 채널 필요 — 후속.)
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try { await _onvif.StopPTZ(ctx.Model.PtzClient, ctx.ProfileToken, true, true).ConfigureAwait(false); }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[PTZ] Stop 실패 cam={cameraId}: {Mask(ex.Message)}"); }
    }

    /*──────────────── 영상 옵션(Imaging) ────────────────*/

    public bool IsImagingCapable(int cameraId) => _ctx.TryGetValue(cameraId, out var c) && c.ImagingPossible;

    public async Task<CameraImagingState?> GetImagingAsync(int cameraId, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return null;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var s = await ctx.Model!.ImagingClient!.GetImagingSettingsAsync(ctx.VsToken).ConfigureAwait(false);
                if (s == null) return null;
                var irc = s.IrCutFilterSpecified ? s.IrCutFilter.ToString() : "AUTO";
                var af = s.Focus?.AutoFocusMode == OnvifImaging.AutoFocusMode.AUTO;
                return new CameraImagingState(irc, af);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] GetImaging 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
    }

    public async Task<bool> SetIrCutFilterAsync(int cameraId, string mode, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return false;
        if (!Enum.TryParse<OnvifImaging.IrCutFilterMode>(mode, true, out var irc)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // read-modify-write — 타 필드(밝기/대비/Focus 등) 보존, 변경 필드만 Specified.
                var s = await ctx.Model!.ImagingClient!.GetImagingSettingsAsync(ctx.VsToken).ConfigureAwait(false);
                if (s == null) return false;
                s.IrCutFilter = irc;
                s.IrCutFilterSpecified = true;
                await ctx.Model.ImagingClient.SetImagingSettingsAsync(ctx.VsToken, s, false).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] SetIrCutFilter 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> SetAutoFocusAsync(int cameraId, bool auto, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var s = await ctx.Model!.ImagingClient!.GetImagingSettingsAsync(ctx.VsToken).ConfigureAwait(false);
                if (s == null) return false;
                s.Focus ??= new OnvifImaging.FocusConfiguration20();
                s.Focus.AutoFocusMode = auto ? OnvifImaging.AutoFocusMode.AUTO : OnvifImaging.AutoFocusMode.MANUAL;
                await ctx.Model.ImagingClient.SetImagingSettingsAsync(ctx.VsToken, s, false).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] SetAutoFocus 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    private bool TryImaging(int cameraId, out CamCtx ctx)
    {
        ctx = null!;
        if (!_ctx.TryGetValue(cameraId, out var c) || !c.ImagingPossible
            || c.Model?.ImagingClient == null || string.IsNullOrEmpty(c.VsToken)) return false;
        ctx = c;
        return true;
    }

    public void Release(int cameraId)
    {
        // H2: 딕셔너리에서만 제거. SemaphoreSlim은 Dispose하지 않는다 — 진행 중 Move/GetStatus 태스크가
        // 동일 ctx.Gate를 await/Release 중일 수 있어, 여기서 Dispose하면 ObjectDisposedException 경합.
        // SemaphoreSlim은 WaitHandle 미사용 시 정리할 핸들이 없어 미Dispose 비용 무시 가능. 멱등.
        _ctx.TryRemove(cameraId, out _);
    }

    /*──────────────── 내부 헬퍼 ────────────────*/

    private static bool IsCapable(CamCtx c)
        => (c.Model?.IsPtzPossible ?? false) && c.Spaces is { } s && (s.HasRel || s.HasAbs);

    /// <summary>GetConfigurations → NodeToken → GetNode로 카메라 space(Rel/Abs/Zoom)를 읽어 캐시.</summary>
    private async Task<SpaceInfo?> LoadSpacesAsync(CamCtx ctx)
    {
        var ptz = ctx.Model!.PtzClient!;
        try
        {
            var cfgs = await ptz.GetConfigurationsAsync().ConfigureAwait(false);
            var nodeToken = cfgs?.PTZConfiguration?.FirstOrDefault()?.NodeToken;
            if (string.IsNullOrEmpty(nodeToken)) return null;

            var node = await ptz.GetNodeAsync(nodeToken).ConfigureAwait(false);
            var sp = node?.SupportedPTZSpaces;
            if (sp == null) return null;

            var rel = sp.RelativePanTiltTranslationSpace?.FirstOrDefault();
            var abs = sp.AbsolutePanTiltPositionSpace?.FirstOrDefault();
            var absZ = sp.AbsoluteZoomPositionSpace?.FirstOrDefault();
            var relZ = sp.RelativeZoomTranslationSpace?.FirstOrDefault();

            return new SpaceInfo(
                HasRel: rel != null,
                RelPtUri: rel?.URI,
                RelXMin: rel?.XRange?.Min ?? -1d, RelXMax: rel?.XRange?.Max ?? 1d,
                RelYMin: rel?.YRange?.Min ?? -1d, RelYMax: rel?.YRange?.Max ?? 1d,
                HasAbs: abs != null,
                AbsPtUri: abs?.URI,
                AbsXMin: abs?.XRange?.Min ?? -1d, AbsXMax: abs?.XRange?.Max ?? 1d,
                AbsYMin: abs?.YRange?.Min ?? -1d, AbsYMax: abs?.YRange?.Max ?? 1d,
                AbsZoomUri: absZ?.URI,
                AbsZMin: absZ?.XRange?.Min ?? 0d, AbsZMax: absZ?.XRange?.Max ?? 1d,
                HasRelZoom: relZ != null, RelZoomUri: relZ?.URI,
                RelZMin: relZ?.XRange?.Min ?? -1d, RelZMax: relZ?.XRange?.Max ?? 1d);
        }
        catch (Exception ex)
        {
            _log?.Error($"[PTZ] GetNode space 로드 실패: {Mask(ex.Message)}");
            return null;
        }
    }

    /// <summary>자격증명/엔드포인트 마스킹(NFR-SEC-01). rtsp://user:pass@host → rtsp://***@host.</summary>
    private static string Mask(string? msg)
        => Regex.Replace(msg ?? string.Empty, @"//[^/@\s]+@", "//***@");
}

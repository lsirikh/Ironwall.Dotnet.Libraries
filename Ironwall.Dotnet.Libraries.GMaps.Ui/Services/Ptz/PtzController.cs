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
using OnvifMedia = Ironwall.Dotnet.Libraries.OnvifSolution.Media;

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
        // 연속 포커스(ContinuousFocus) 속도 범위 캐시 — GetMoveOptions 1회 조회(FR-PH-10 클램프 진실원). null=미조회/미지원→폴백.
        public bool FocusOptLoaded;
        public double? FocusSpeedMin;
        public double? FocusSpeedMax;
        // Onvif조회 모드(RTSP 소스) — GetStreamUri 결과 캐시(워밍 수명, Release 시 CamCtx째 무효화). (CameraPopup_RtspSource_Priority FR-03/07)
        public string? ResolvedStreamUri;
        public string? ResolvedProfileToken;
        public bool ResolvedPreferSub;
    }

    /// <summary>GetNode로 읽은 카메라 좌표 space(변환/클램프 진실원). URI는 직렬화에 동봉.</summary>
    private sealed record SpaceInfo(
        bool HasRel, string? RelPtUri, double RelXMin, double RelXMax, double RelYMin, double RelYMax,
        bool HasAbs, string? AbsPtUri, double AbsXMin, double AbsXMax, double AbsYMin, double AbsYMax,
        string? AbsZoomUri, double AbsZMin, double AbsZMax,
        bool HasRelZoom, string? RelZoomUri, double RelZMin, double RelZMax,
        // ContinuousMove velocity space URI(공백이면 카메라가 velocity를 무시할 수 있음 → ContinuousMove 무동작 원인).
        string? ContPtUri, string? ContZoomUri,
        // ContinuousMove velocity space 범위(FR-PTR-02 속도 스케일 진실원). 미제공 시 [-1,1] 폴백(=항등).
        double ContPtXMin, double ContPtXMax, double ContPtYMin, double ContPtYMax,
        double ContZMin, double ContZMax);

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
                // 워밍 판단은 "모델이 캐시됐는가"(ctx.Model==null) 기준 — PtzClient 유무가 아니다.
                // (구버전은 ctx.Model?.PtzClient==null 기준이라, 비PTZ 카메라는 모델을 못 담아 매 오픈 재초기화됐다.)
                if (ctx.Model == null)
                {
                    // PTZ 빠른 초기화(InitializePtz): device-info/스트림파싱/ONVIF프리셋 생략 + 클라이언트 일괄 생성으로
                    // SOAP 왕복 ~12→3 단축(첫 오픈 지연 감소). PTZ 이동/줌·옵션(주야간/포커스)에 필요한 것만 확보.
                    var model = await _onvif.InitializePtzAsync(conn, ct).ConfigureAwait(false);
                    if (model == null)
                    {
                        // ONVIF 연결/인증 자체 실패 — 캐시하지 않음(다음 오픈 시 재시도 허용).
                        _log?.Warning($"[PTZ] ONVIF 초기화 실패 cam={cameraId} — 연결/인증 실패(포트/계정 확인). 워밍 안 함(재시도 허용).");
                        return false;
                    }
                    // 모델이 살아있으면 PTZ 미지원(고정 카메라)이라도 인스턴스를 캐시 유지(워밍).
                    // 고정 카메라도 ONVIF Imaging(주야간/포커스)을 제공하며, 재오픈마다 ~31초 재초기화를 막는다.
                    ctx.Model = model;
                    ctx.ProfileToken = model.CameraMedia?.Token ?? string.Empty;
                    // 영상 옵션(Imaging)용 VideoSourceToken — 첫 프로파일의 VideoSourceConfiguration.
                    ctx.VsToken = model.Profiles?.FirstOrDefault()?.VideoSourceConfiguration?.SourceToken;
                    ctx.ImagingPossible = model.IsImagingPossible && model.ImagingClient != null && !string.IsNullOrEmpty(ctx.VsToken);
                    if (model.PtzClient == null)
                        _log?.Info($"[PTZ] cam={cameraId} 비PTZ(고정) — PtzClient 없음. 인스턴스 워밍 유지(Imaging={ctx.ImagingPossible}). 재오픈 시 즉시.");
                }
                // PTZ space(GetNode)는 PtzClient가 있을 때만 로드(비PTZ는 생략 — LoadSpaces의 PtzClient 역참조 NRE 방지).
                if (ctx.Model.PtzClient != null)
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
            var __swGate = System.Diagnostics.Stopwatch.StartNew();   // [진단] 게이트(직렬화) 대기 시간
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            __swGate.Stop();
            try
            {
                // ContinuousMove(속도). Relative 미지원 카메라도 대부분 지원. 카메라는 Stop/타임아웃까지 이동.
                // velocity space를 GetNode의 ContinuousPanTilt/ZoomVelocitySpace로 명시 — 공백이면 일부 카메라가
                // velocity를 무시해 무동작(이 카메라 증상). space가 null이면 카메라 기본 space 사용.
                var sp = ctx.Spaces;
                // FR-PTR-02: 정규화 속도(방향×사용자 PanTiltSpeed/ZoomSpeed)를 카메라 연속속도 범위로 스케일 → 속도 슬라이더 실반영.
                // sp==null(GetNode 미로드)이면 원값. 범위 미제공 시 [-1,1] 폴백 → 스케일=항등(기존 동작 안전).
                var sPan  = sp != null ? PtzVelocityMath.ScaleToRange(panVel,  sp.ContPtXMin, sp.ContPtXMax) : panVel;
                var sTilt = sp != null ? PtzVelocityMath.ScaleToRange(tiltVel, sp.ContPtYMin, sp.ContPtYMax) : tiltVel;
                var sZoom = sp != null ? PtzVelocityMath.ScaleToRange(zoomVel, sp.ContZMin,  sp.ContZMax)  : zoomVel;
                var speed = new PtzSpeedDto
                {
                    PanTilt = new Vector2DDto { X = (float)sPan, Y = (float)sTilt, Space = sp?.ContPtUri },
                    Zoom = new Vector1DDto { X = (float)sZoom, Space = sp?.ContZoomUri },
                };
                _log?.Info($"[PTZ] ContinuousMove cam={cameraId} norm(pan={panVel:F2},tilt={tiltVel:F2},zoom={zoomVel:F2})→scaled(pan={sPan:F2},tilt={sTilt:F2},zoom={sZoom:F2}) ptRange=[{(sp?.ContPtXMin ?? -1):F2},{(sp?.ContPtXMax ?? 1):F2}] zRange=[{(sp?.ContZMin ?? -1):F2},{(sp?.ContZMax ?? 1):F2}] gateWait={__swGate.ElapsedMilliseconds}ms");
                var __swMove = System.Diagnostics.Stopwatch.StartNew();   // [진단] WCF ContinuousMove 호출 왕복
                await _onvif.MovePTZ(ctx.Model.PtzClient, speed, ctx.ProfileToken, "PT10S").ConfigureAwait(false);
                __swMove.Stop();
                _log?.Info($"[PTZ] ContinuousMove WCF={__swMove.ElapsedMilliseconds}ms cam={cameraId}  (카메라 raw SOAP=~10~150ms 측정됨 — 이 값이 크면 WCF 바인딩 병목 확정)");
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

    // 수동 포커스 기본 속도 — GetMoveOptions 범위로 클램프(FR-PH-10), 옵션 미제공 카메라는 이 값 폴백.
    private const double FocusMoveSpeed = 0.7;   // ContinuousFocus 속도 크기 [0,1]

    /// <summary>포커스 연속 이동 시작(누름→Stop까지). 속도는 카메라 ContinuousFocus 범위로 클램프. delay/stop 없음(StopFocusAsync가 정지). (FR-PH-02/10)</summary>
    public async Task<bool> StartFocusAsync(int cameraId, int direction, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureFocusOptionsAsync(ctx).ConfigureAwait(false);   // GetMoveOptions 1회 캐시
                var mag = PtzFocusMath.ClampMagnitude(FocusMoveSpeed, ctx.FocusSpeedMin, ctx.FocusSpeedMax);
                // ContinuousFocus(속도) — RelativeFocus 미지원 카메라 대응(SetAutoFocus처럼 ImagingClient 직접). 정지는 StopFocusAsync.
                var move = new OnvifImaging.FocusMove
                {
                    Continuous = new OnvifImaging.ContinuousFocus { Speed = (float)((direction >= 0 ? 1 : -1) * mag) }
                };
                await ctx.Model!.ImagingClient!.MoveAsync(ctx.VsToken, move).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] StartFocus 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    /// <summary>포커스 연속 이동 정지(뗌/캡처분실/닫기) — ImagingClient.Stop. PTZ StopAsync와 별개 모터 경로. (FR-PH-02/03/05)</summary>
    public async Task StopFocusAsync(int cameraId, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try { await ctx.Model!.ImagingClient!.StopAsync(ctx.VsToken).ConfigureAwait(false); }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[PTZ] StopFocus 실패 cam={cameraId}: {Mask(ex.Message)}"); }
    }

    /// <summary>ContinuousFocus 속도 범위(GetMoveOptions)를 1회 조회해 캐시(FR-PH-10). 미지원/실패면 폴백 표시(±FocusMoveSpeed). Gate 보유 중 호출.</summary>
    private async Task EnsureFocusOptionsAsync(CamCtx ctx)
    {
        if (ctx.FocusOptLoaded) return;
        ctx.FocusOptLoaded = true;   // 1회만 시도(실패해도 폴백)
        try
        {
            var opt = await ctx.Model!.ImagingClient!.GetMoveOptionsAsync(ctx.VsToken).ConfigureAwait(false);
            var sp = opt?.Continuous?.Speed;
            if (sp != null && sp.Max > sp.Min) { ctx.FocusSpeedMin = sp.Min; ctx.FocusSpeedMax = sp.Max; }
            else _log?.Info("[PTZ] 포커스 ContinuousFocus 속도 옵션 없음 — 기본 폴백.");
        }
        catch (Exception ex) { _log?.Warning($"[PTZ] GetMoveOptions 실패(포커스 폴백): {Mask(ex.Message)}"); }
    }

    /*──────────────── RTSP 스트림 URL 조회(Onvif조회 모드 — CameraPopup_RtspSource_Priority FR-03/07/08) ────────────────*/

    public async Task<string?> ResolveStreamUriAsync(int cameraId, IConnectionModel conn, bool preferSub = true, CancellationToken ct = default)
    {
        if (conn == null) return null;
        // 모델 확보 — InitializePtz 워밍 재사용(이중 초기화 0). PTZ capable 여부와 무관하게
        // MediaClient/Profiles만 있으면 조회 가능(비PTZ 고정 카메라 포함).
        // 오픈 시 EnsurePtzReadyAsync(PTZ 준비)와 동시 호출될 수 있음(H-2) — 같은 Gate로 직렬화되어
        // InitializePtz는 1회만 수행되고 나중 진입자는 캐시를 본다. 첫 오픈 최악 지연=init+LoadSpaces+GetStreamUri
        // 직렬(호출측 12s 타임아웃이 안전망).
        await EnsureReadyAsync(cameraId, conn, ct).ConfigureAwait(false);
        if (!_ctx.TryGetValue(cameraId, out var ctx)) return null;

        try
        {
            // ⚠ ctx.Gate 이중 획득 금지(H-1): 위 EnsureReadyAsync가 내부에서 Gate를 획득·해제(비재진입
            //   SemaphoreSlim)한 뒤 여기서 "순차"로 다시 획득한다 — EnsureReady 로직을 이 Gate 블록 안으로
            //   인라인하거나 전체를 단일 Gate로 감싸면 자기 데드락(취소 전까지 무한 대기)이 된다.
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (ctx.ResolvedStreamUri != null && ctx.ResolvedPreferSub == preferSub)
                    return ctx.ResolvedStreamUri;   // 캐시 적중(워밍 수명 — Release 시 CamCtx째 무효화, FR-07)

                var media = ctx.Model?.MediaClient;
                var profiles = ctx.Model?.Profiles;
                if (media == null || profiles == null || profiles.Count == 0)
                {
                    _log?.Info($"[PTZ] StreamUri 조회 불가 cam={cameraId} — MediaClient/Profiles 없음(ONVIF 초기화 실패 또는 미디어 미지원).");
                    return null;
                }

                // WCF Profile → 순수 선택기 입력 투영(해상도·오디오 유무). 규칙: 해상도 최소→비오디오→원 순서(OQ-01).
                var infos = profiles
                    .Where(p => p != null && !string.IsNullOrEmpty(p.token))
                    .Select(p => new OnvifProfileSelector.ProfileInfo(
                        p.token,
                        p.VideoEncoderConfiguration?.Resolution?.Width ?? 0,
                        p.VideoEncoderConfiguration?.Resolution?.Height ?? 0,
                        p.AudioEncoderConfiguration != null))
                    .ToList();
                var token = OnvifProfileSelector.Select(infos, preferSub);
                if (string.IsNullOrEmpty(token))
                {
                    _log?.Info($"[PTZ] StreamUri 조회 불가 cam={cameraId} — 선택 가능한 프로파일 토큰 없음(profiles={profiles.Count}).");
                    return null;
                }

                var setup = new OnvifMedia.StreamSetup
                {
                    Stream = OnvifMedia.StreamType.RTPUnicast,
                    Transport = new OnvifMedia.Transport { Protocol = OnvifMedia.TransportProtocol.RTSP },
                };
                var uri = (await media.GetStreamUriAsync(setup, token).ConfigureAwait(false))?.Uri;
                if (string.IsNullOrWhiteSpace(uri))
                {
                    _log?.Warning($"[PTZ] GetStreamUri 빈 응답 cam={cameraId} profile={token}.");
                    return null;
                }

                ctx.ResolvedStreamUri = uri;
                ctx.ResolvedProfileToken = token;
                ctx.ResolvedPreferSub = preferSub;
                _log?.Info($"[PTZ] StreamUri 조회 cam={cameraId} profile={token} preferSub={preferSub} uri={Mask(uri)}");
                return uri;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] StreamUri 조회 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
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
            var contPt = sp.ContinuousPanTiltVelocitySpace?.FirstOrDefault();   // ContinuousMove 팬틸트 velocity space
            var contZ = sp.ContinuousZoomVelocitySpace?.FirstOrDefault();       // ContinuousMove 줌 velocity space

            // FR-PTR-02 진단(code-review MED): 연속속도 범위가 단방향/비대칭(min≥0)이면 역방향(음수)이 0으로 죽어
            // "한 방향만 안 움직임" 오진을 유발 → 1회 경고로 필드 진단 가능하게.
            if ((contPt?.XRange?.Min ?? -1d) >= 0d || (contPt?.YRange?.Min ?? -1d) >= 0d || (contZ?.XRange?.Min ?? -1d) >= 0d)
                _log?.Warning($"[PTZ] 연속속도 범위 단방향/비대칭 — ptX.min={(contPt?.XRange?.Min ?? -1d):F2} ptY.min={(contPt?.YRange?.Min ?? -1d):F2} z.min={(contZ?.XRange?.Min ?? -1d):F2}. 역방향(음수) 이동이 0으로 제한될 수 있음(카메라 space 특성).");

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
                RelZMin: relZ?.XRange?.Min ?? -1d, RelZMax: relZ?.XRange?.Max ?? 1d,
                ContPtUri: contPt?.URI, ContZoomUri: contZ?.URI,
                // 연속속도 범위(FR-PTR-02). 미제공 → [-1,1] 폴백(스케일 항등 = 기존 동작 안전).
                ContPtXMin: contPt?.XRange?.Min ?? -1d, ContPtXMax: contPt?.XRange?.Max ?? 1d,
                ContPtYMin: contPt?.YRange?.Min ?? -1d, ContPtYMax: contPt?.YRange?.Max ?? 1d,
                ContZMin: contZ?.XRange?.Min ?? -1d, ContZMax: contZ?.XRange?.Max ?? 1d);
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

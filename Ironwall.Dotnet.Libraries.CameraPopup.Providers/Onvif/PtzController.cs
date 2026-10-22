using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Ironwall.Dotnet.Libraries.OnvifSolution.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Services;
using OnvifImaging = Ironwall.Dotnet.Libraries.OnvifSolution.Imaging;
using OnvifMedia = Ironwall.Dotnet.Libraries.OnvifSolution.Media;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

/****************************************************************************
   Purpose      : 팝업 PTZ 제어 단일 소유 앱서비스 구현 (CameraPopup_PTZ_Control)
                  2026-09-30 camera-popup-modes T-02: GMaps.Ui → CameraPopup.Providers 로 이관(호스트 프로세스 전용,
                  카메라 id 는 문자열). GIS 프로세스는 이 클래스를 부르지 않는다(§0).
   Created By   : Claude Code
   Created On   : 2026-06-24
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="IPtzController"/> 구현. cameraId별 ONVIF 자원(PtzClient·profileToken·GetNode space)을
/// 캐시·직렬화한다. PRD FR-PTZCTL-01~03 / FR-WRAP-01 / NFR-THREAD-01.
///
/// 스레드 안전(I-05): 카메라 맵=ConcurrentDictionary, 카메라별 <see cref="PtzCommandGate"/>로 PtzClient 직렬 호출
/// (동일 인스턴스 병렬 호출 금지). 다른 cameraId는 병렬. 모든 ONVIF 호출은 ConfigureAwait(false).
/// 정지 우선(camera-popup-modes FR-22): 정지는 대기 중 이동을 취소하고 대기열 맨 앞에 선다. 연속 이동은 안전 제한
/// <see cref="PtzMotionPolicy.MoveTimeout"/>(2초)로 보내고, 누르는 동안 <see cref="PtzMotionPolicy.KeepAliveInterval"/>마다 재전송한다.
/// 변환/클램프는 카메라 GetNode space(XRange/YRange) 진실원 — 고정 ±1.0 금지(NFR-SAFE-01).
/// </summary>
public sealed class PtzController : IPtzController
{
    private readonly IOnvifService _onvif;
    private readonly ILogService? _log;
    private readonly ConcurrentDictionary<string, CamCtx> _ctx = new(StringComparer.Ordinal);

    public PtzController(IOnvifService onvif, ILogService? log = null)
    {
        _onvif = onvif ?? throw new ArgumentNullException(nameof(onvif));
        _log = log;
    }

    /*──────────────── 내부 상태 ────────────────*/

    private sealed class CamCtx
    {
        // 응답 없는 카메라의 연결을 끊을 때(AbandonStalled) 게이트 밖에서도 null 로 바뀐다 — 읽는 쪽은 한 번 읽어 쓴다.
        public volatile ICameraOnvifModel? Model;
        /// <summary>이 캐시를 만든 접속 정보(주소 · 포트 · 계정). 바뀌면 캐시째 버리고 새로 붙는다(<see cref="CtxFor"/>).</summary>
        public string ConnKey = string.Empty;
        /// <summary>마지막으로 붙은 접속 정보 · 그때 PTZ 가 있었나 — 응답 없어 끊은 연결을 다음 명령이 다시 붙일 때 쓴다(<see cref="LiveModelAsync"/>).</summary>
        public IConnectionModel? Conn;
        public bool HadPtz;
        public string ProfileToken = string.Empty;
        public SpaceInfo? Spaces;
        public string? VsToken;            // 영상 옵션(Imaging)용 VideoSourceToken
        public bool ImagingPossible;       // IsImagingPossible + ImagingClient + VsToken
        public readonly PtzCommandGate Gate = new();   // 정지 우선 직렬 게이트(FR-22) — WaitAsync/Release 모양은 SemaphoreSlim 과 같다
        public volatile bool Busy;
        // 누르는 동안 연속 이동 유지 재전송(FR-22 PT2S). 새 이동 · 정지 · 위치 명령 · Release 가 취소한다.
        public CancellationTokenSource? KeepAlive;
        // 연속 포커스(ContinuousFocus) 속도 범위 캐시 — GetMoveOptions 1회 조회(FR-PH-10 클램프 진실원). null=미조회/미지원→폴백.
        public bool FocusOptLoaded;
        public double? FocusSpeedMin;
        public double? FocusSpeedMax;
        // Onvif조회 모드(RTSP 소스) — GetStreamUri 결과 캐시(워밍 수명, Release 시 CamCtx째 무효화). (CameraPopup_RtspSource_Priority FR-03/07)
        public string? ResolvedStreamUri;
        public string? ResolvedProfileToken;
        public bool ResolvedPreferSub;
        /// <summary>프로파일 토큰 → 재생 주소(타일 = 서브, 크게 보기 = 메인이 같은 카메라에서 함께 열린다). Gate 안에서만 만진다.</summary>
        public readonly Dictionary<string, string> ResolvedUris = new(StringComparer.Ordinal);

        // ── 영상 위 드래그(DragMove) ──
        /// <summary>가장 새 드래그의 취소원 — 새 드래그 · 다른 PTZ 명령이 이전 드래그(게이트 대기 · 시간 제한 이동의 대기)를 버린다.</summary>
        public CancellationTokenSource? Drag;
        /// <summary>배경 위치 다시 읽기(GetStatus)의 취소원.</summary>
        public CancellationTokenSource? StatusRefresh;
        /// <summary>마지막으로 읽었거나 명령으로 추적한 위치. 드래그마다 GetStatus 를 부르지 않기 위한 캐시.</summary>
        public volatile PtzPosition? LastPosition;
        /// <summary>팬 · 틸트가 캐시와 다를 수 있다(연속 이동 · 프리셋 뒤 아직 다시 못 읽음).</summary>
        public volatile bool PanTiltStale = true;
        /// <summary>줌이 캐시와 다를 수 있다(연속 줌 · 프리셋 뒤 아직 다시 못 읽음).</summary>
        public volatile bool ZoomStale = true;
        /// <summary>위치를 마지막으로 카메라에서 읽은 때(Environment.TickCount64). 0 = 읽은 적 없음.</summary>
        public long LastPositionTick;
        /// <summary>위치를 알려주지 않는 카메라(GetStatus 실패 · 빈 응답) — 다시 묻지 않는다.</summary>
        public volatile bool StatusUnsupported;

        /// <summary>카메라 기본 속도(프로파일 PTZConfiguration.DefaultPTZSpeed ÷ 속도 공간 → 0.1..1). null = 카메라가 안 알려줬다.</summary>
        public double? DefaultPanTiltSpeed;
        public double? DefaultZoomSpeed;

        /// <summary>IR 조명 값("On" · "Off" · "Auto") → 카메라가 알린 보조 명령 글자 그대로(GetNode AuxiliaryCommands). null = 아직 못 읽음.</summary>
        public volatile System.Collections.Generic.IReadOnlyDictionary<string, string>? IrLampCommands;
        /// <summary>마지막으로 보낸 IR 조명 값(카메라가 상태를 알려주지 않는다).</summary>
        public volatile string? LastIrLamp;
    }

    /// <summary>GetNode로 읽은 카메라 좌표 space(변환/클램프 진실원). URI는 직렬화에 동봉.</summary>
    private sealed record SpaceInfo(
        // 화각 상대 공간(TranslationSpaceFov) — 있으면 드래그 PTZ 가 이것을 먼저 쓴다(±1 = 화면 가장자리).
        bool HasRelFov, string? RelFovUri, double RelFovXMin, double RelFovXMax, double RelFovYMin, double RelFovYMax,
        bool HasCont,
        bool HasRel, string? RelPtUri, double RelXMin, double RelXMax, double RelYMin, double RelYMax,
        bool HasAbs, string? AbsPtUri, double AbsXMin, double AbsXMax, double AbsYMin, double AbsYMax,
        string? AbsZoomUri, double AbsZMin, double AbsZMax,
        bool HasRelZoom, string? RelZoomUri, double RelZMin, double RelZMax,
        // ContinuousMove velocity space URI(공백이면 카메라가 velocity를 무시할 수 있음 → ContinuousMove 무동작 원인).
        string? ContPtUri, string? ContZoomUri,
        // ContinuousMove velocity space 범위(FR-PTR-02 속도 스케일 진실원). 미제공 시 [-1,1] 폴백(=항등).
        double ContPtXMin, double ContPtXMax, double ContPtYMin, double ContPtYMax,
        double ContZMin, double ContZMax,
        // 속도 공간(PanTiltSpeedSpace · ZoomSpeedSpace) — RelativeMove · AbsoluteMove 의 Speed 와 기본 속도 정규화. 미제공 = [0,1].
        string? PtSpeedUri, double PtSpeedMin, double PtSpeedMax, double ZSpeedMin, double ZSpeedMax);

    /*──────────────── 공개 API ────────────────*/

    public async Task<bool> EnsureReadyAsync(string cameraId, IConnectionModel conn, CancellationToken ct = default)
    {
        if (conn == null) return false;
        var ctx = CtxFor(cameraId, ConnKeyOf(conn));
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // 워밍 판단은 "모델이 캐시됐는가"(ctx.Model==null) 기준 — PtzClient 유무가 아니다.
                // (구버전은 ctx.Model?.PtzClient==null 기준이라, 비PTZ 카메라는 모델을 못 담아 매 오픈 재초기화됐다.)
                if (ctx.Model == null && !await InitCoreAsync(cameraId, ctx, conn, ct).ConfigureAwait(false)) return false;
                // PTZ space(GetNode)는 PtzClient가 있을 때만 로드(비PTZ는 생략 — LoadSpaces의 PtzClient 역참조 NRE 방지).
                if (ctx.Model is { PtzClient: not null } && ctx.Spaces == null)
                {
                    ctx.Spaces = await LoadSpacesAsync(ctx).ConfigureAwait(false);
                    // 드래그 PTZ 가 쓸 위치 · 줌을 배경에서 한 번 읽어 둔다(준비를 늦추지 않는다 — 게이트가 풀린 뒤 돈다).
                    if (ctx.Spaces != null)
                    {
                        ReadDefaultSpeeds(cameraId, ctx, ctx.Spaces);
                        ScheduleStatusRefresh(cameraId, ctx, TimeSpan.Zero);
                    }
                }
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

    /// <summary>
    /// 연결 초기화(게이트를 쥔 채 부른다) — 성공하면 모델 · 프로파일 토큰 · 영상 옵션 토큰을 캐시하고 접속 정보를 기억한다
    /// (응답 없어 끊은 연결을 다음 명령이 다시 붙일 때 쓴다 — <see cref="LiveModelAsync"/>). 실패는 캐시하지 않는다(재시도 허용).
    /// </summary>
    private async Task<bool> InitCoreAsync(string cameraId, CamCtx ctx, IConnectionModel conn, CancellationToken ct)
    {
        // PTZ 빠른 초기화(InitializePtz): device-info/스트림파싱/ONVIF프리셋 생략 + 클라이언트 일괄 생성으로
        // SOAP 왕복 ~12→3 단축(첫 오픈 지연 감소). PTZ 이동/줌·옵션(주야간/포커스)에 필요한 것만 확보.
        // 초기화는 자기 클라이언트를 새로 만들어 쓰므로 부른 쪽이 포기하면 기다리지 않고 게이트를 푼다 — 무응답 카메라의
        // 초기화가 WCF 제한 시간(8초)까지 줄을 막아, 카메라가 돌아온 뒤 다시 연 팝업까지 늦던 것(2026-10-22 실측 6.5초).
        var init = _onvif.InitializePtzAsync(conn, ct);
        ICameraOnvifModel? model;
        try { model = await init.WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            _ = init.ContinueWith(t => { if (t.IsCompletedSuccessfully) AbortClients(t.Result); else _ = t.Exception; },
                TaskScheduler.Default);
            throw;
        }
        if (model == null)
        {
            // ONVIF 연결/인증 자체 실패 — 캐시하지 않음(다음 오픈 시 재시도 허용).
            _log?.Warning($"[PTZ] ONVIF 초기화 실패 cam={cameraId} — 연결/인증 실패(포트/계정 확인). 워밍 안 함(재시도 허용).");
            return false;
        }
        // 감사(scenario-H): 인증/포트 실패에도 빌더가 "클라이언트 전무" 모델을 non-null로 반환할 수 있어
        // 그대로 캐시하면 실패가 세션 내내 고착(재시도 분기 데드코드화 — PTZ/Imaging/StreamUri 전부 불능).
        // 3개 클라이언트가 전부 null이면 연결 실패로 간주해 캐시하지 않는다(재시도 허용).
        if (model.PtzClient == null && model.MediaClient == null && model.ImagingClient == null)
        {
            _log?.Warning($"[PTZ] ONVIF 초기화 부분실패 cam={cameraId} — 클라이언트 전무(인증/포트 실패 추정). 워밍 안 함(재시도 허용).");
            return false;
        }
        // 클라이언트는 시계(GetSystemDateAndTime) · GetCapabilities 만으로 만들어진다 — 둘 다 계정 없이 답하는(PRE_AUTH)
        // 호출이라 비밀번호가 틀려도 클라이언트가 생긴다. 계정이 처음 검사되는 GetProfiles 가 실패하면(프로파일 없음)
        // 연결 실패로 본다: 캐시하면 "연결됨"으로 보이고, 모든 PTZ 명령이 빈 프로파일 토큰으로 나가며(fault:NoProfile),
        // 비밀번호를 고치거나 카메라 잠금이 풀려도 호스트를 다시 띄울 때까지 다시 붙지 않았다(2026-10-22 가짜 카메라 실측).
        if (model.Profiles is not { Count: > 0 })
        {
            _log?.Warning($"[PTZ] ONVIF 초기화 실패 cam={cameraId} — 프로파일을 못 읽음(계정 거부 추정). 워밍 안 함(재시도 허용).");
            AbortClients(model);
            return false;
        }
        // 모델이 살아있으면 PTZ 미지원(고정 카메라)이라도 인스턴스를 캐시 유지(워밍).
        // 고정 카메라도 ONVIF Imaging(주야간/포커스)을 제공하며, 재오픈마다 ~31초 재초기화를 막는다.
        ctx.Model = model;
        // PTZ 명령은 PTZ 구성이 붙은 프로파일로 보낸다(ONVIF: 없는 프로파일엔 ter:NoPTZProfile). 첫 프로파일에 PTZ 구성이
        // 없는 카메라(메타데이터 · 3번째 스트림 프로파일이 앞에 오는 경우)에서 모든 이동이 실패하던 것. 기본 속도 읽기(ReadDefaultSpeeds)와 같은 규칙.
        ctx.ProfileToken = model.Profiles.FirstOrDefault(p => p?.PTZConfiguration != null && !string.IsNullOrEmpty(p.token))?.token
                           ?? model.CameraMedia?.Token ?? string.Empty;
        // 영상 옵션(Imaging)용 VideoSourceToken — 첫 프로파일의 VideoSourceConfiguration.
        ctx.VsToken = model.Profiles?.FirstOrDefault()?.VideoSourceConfiguration?.SourceToken;
        ctx.ImagingPossible = model.IsImagingPossible && model.ImagingClient != null && !string.IsNullOrEmpty(ctx.VsToken);
        if (model.PtzClient == null)
            _log?.Info($"[PTZ] cam={cameraId} 비PTZ(고정) — PtzClient 없음. 인스턴스 워밍 유지(Imaging={ctx.ImagingPossible}). 재오픈 시 즉시. IR 조명 직접 제어 없음(보조 명령은 PTZ 서비스에만 있다).");
        ctx.Conn = conn;
        ctx.HadPtz = model.PtzClient != null;
        return true;
    }

    public bool IsPtzCapable(string cameraId) => _ctx.TryGetValue(cameraId, out var c) && IsCapable(c);

    public bool IsReady(string cameraId) => _ctx.TryGetValue(cameraId, out var c) && c.Model != null;

    public bool IsBusy(string cameraId) => _ctx.TryGetValue(cameraId, out var c) && c.Busy;

    /// <summary>PTZ 명령을 받을 카메라인가 — 연결이 살아 있으면 PtzClient, 응답 없어 끊었으면 그 전에 PTZ 였는지(다음 명령이 다시 붙는다).</summary>
    private static bool HasPtz(CamCtx c) => c.Model is { } m ? m.PtzClient != null : c.HadPtz && c.Conn != null;

    /// <summary>
    /// 게이트 안에서 쓸 모델. 응답 없어 끊은 연결(<see cref="AbandonStalled"/>)이면 기억한 접속 정보로 그 자리에서 다시 붙는다 —
    /// 호스트가 "준비됨"을 보고 바로 보낸 다음 명령이 끊긴 연결을 만나 실패하지 않게(끊기는 부른 쪽이 결과를 받은 직후에도 일어난다).
    /// </summary>
    private async Task<ICameraOnvifModel?> LiveModelAsync(string cameraId, CamCtx ctx, CancellationToken ct)
    {
        if (ctx.Model is { } live) return live;
        if (ctx.Conn is not { } conn) return null;
        _log?.Info($"[PTZ] cam={cameraId} 끊었던 연결을 다시 붙는다.");
        return await InitCoreAsync(cameraId, ctx, conn, ct).ConfigureAwait(false) ? ctx.Model : null;
    }

    public async Task<bool> RelativeMoveByPixelAsync(string cameraId, double dx, double dy,
        double imageW, double imageH, double sensitivity = 1.0, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { HasRel: true } sp || !HasPtz(ctx))
            return false;

        CancelKeepAlive(ctx);   // 위치 명령이 연속 이동 유지 재전송에 덮이지 않게(FR-22)
        CancelDrag(ctx);
        ctx.PanTiltStale = true;
        var (pan, tilt) = PtzCoordinateMath.PixelDeltaToRelative(
            dx, dy, imageW, imageH, sp.RelXMin, sp.RelXMax, sp.RelYMin, sp.RelYMax, sensitivity);

        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var vec = PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildPanTilt(pan, tilt, sp.RelPtUri));
                await CallAsync(cameraId, ctx, client.RelativeMoveAsync(ctx.ProfileToken, vec, null), ct).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] RelativeMove 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> ContinuousMoveAsync(string cameraId, double panVel, double tiltVel, double zoomVel, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        CancelKeepAlive(ctx);   // 직전 이동의 유지 재전송 중단 — 새 속도로 대체
        CancelDrag(ctx);        // 대기 중 드래그 · 시간 제한 이동의 정지가 이 이동을 끊지 않게
        CancelStatusRefresh(ctx);
        if (panVel != 0 || tiltVel != 0) ctx.PanTiltStale = true;
        if (zoomVel != 0) ctx.ZoomStale = true;
        try
        {
            var __swGate = System.Diagnostics.Stopwatch.StartNew();   // [진단] 게이트(직렬화) 대기 시간
            // FR-22: 이동 대기 중 정지가 오면 이 대기는 취소된다(정지가 먼저 나감). epoch = 이 이동의 세대(정지 시 취소).
            var epoch = await ctx.Gate.WaitMoveAsync(ct).ConfigureAwait(false);
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
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var __swMove = System.Diagnostics.Stopwatch.StartNew();   // [진단] WCF ContinuousMove 호출 왕복
                // FR-22: 안전 제한 PT10S → PT2S(GIS 가 죽어 정지가 안 가도 2초 뒤 카메라가 스스로 멈춤). 누르는 동안은 유지 재전송.
                var ok = await CallAsync(cameraId, ctx, _onvif.MovePTZ(client, speed, ctx.ProfileToken, PtzMotionPolicy.MoveTimeout), ct).ConfigureAwait(false);
                __swMove.Stop();
                _log?.Info($"[PTZ] ContinuousMove WCF={__swMove.ElapsedMilliseconds}ms cam={cameraId} ok={ok}");
                if (ok) StartKeepAlive(cameraId, ctx, speed, epoch);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] ContinuousMove 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> RelativeZoomAsync(string cameraId, double zoomDelta, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { HasRelZoom: true } sp || !HasPtz(ctx))
            return false;

        CancelKeepAlive(ctx);   // 위치 명령이 연속 이동 유지 재전송에 덮이지 않게(FR-22)
        CancelDrag(ctx);
        ctx.ZoomStale = true;
        var z = PtzCoordinateMath.ClampToRange(zoomDelta, sp.RelZMin, sp.RelZMax);
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var vec = PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildZoom(z, sp.RelZoomUri));
                await CallAsync(cameraId, ctx, client.RelativeMoveAsync(ctx.ProfileToken, vec, null), ct).ConfigureAwait(false);
                ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshRetry);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] RelativeZoom 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> AbsoluteMoveAsync(string cameraId, double pan, double tilt, double zoom, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || ctx.Spaces is not { HasAbs: true } sp || !HasPtz(ctx))
            return false;

        CancelKeepAlive(ctx);   // 위치 명령이 연속 이동 유지 재전송에 덮이지 않게(FR-22)
        CancelDrag(ctx);
        ctx.PanTiltStale = true;
        ctx.ZoomStale = true;
        var cpan = PtzCoordinateMath.ClampToRange(pan, sp.AbsXMin, sp.AbsXMax);
        var ctilt = PtzCoordinateMath.ClampToRange(tilt, sp.AbsYMin, sp.AbsYMax);
        double? czoom = sp.AbsZoomUri != null ? PtzCoordinateMath.ClampToRange(zoom, sp.AbsZMin, sp.AbsZMax) : null;

        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            ctx.Busy = true;
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var vec = PtzVectorMapper.ToPtzVector(
                    PtzVectorMapper.BuildAbsolute(cpan, ctilt, czoom, sp.AbsPtUri, sp.AbsZoomUri));
                await CallAsync(cameraId, ctx, client.AbsoluteMoveAsync(ctx.ProfileToken, vec, null), ct).ConfigureAwait(false);
                ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshRetry);
                return true;
            }
            finally { ctx.Busy = false; ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] AbsoluteMove 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<PtzPosition?> GetStatusAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return null;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return null;
                var st = await CallAsync(cameraId, ctx, client.GetStatusAsync(ctx.ProfileToken), ct).ConfigureAwait(false);
                var p = st?.Position;
                if (p?.PanTilt == null) return null;
                return new PtzPosition(p.PanTilt.x, p.PanTilt.y, p.Zoom?.x ?? 0d, p.PanTilt.space, p.Zoom?.space);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] GetStatus 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
    }

    public async Task StopAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return;
        if (ct.IsCancellationRequested) return;   // LWW: 새 제스처가 이미 인계 — 새 이동의 유지 재전송 · 대기를 건드리지 않는다
        CancelKeepAlive(ctx);                      // 유지 재전송 즉시 중단(정지 뒤에 이동이 다시 나가지 않게)
        CancelDrag(ctx);                           // 대기 중 드래그 · 시간 제한 이동의 뒤늦은 정지도 버린다
        await StopCoreAsync(cameraId, ctx, ct).ConfigureAwait(false);
        ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshDelay);   // 멈춘 자리 · 줌을 배경에서 다시 읽는다
    }

    /// <summary>정지 한 번(게이트 맨 앞). 유지 재전송 · 드래그 취소는 부르는 쪽이 한다.</summary>
    private async Task StopCoreAsync(string cameraId, CamCtx ctx, CancellationToken ct)
    {
        if (!HasPtz(ctx)) return;
        try
        {
            // C1: PtzClient(WCF 채널) 동일 인스턴스 병렬 호출 금지(I-05) — Move와 동일 Gate로 직렬화.
            // FR-22: 정지 우선 — 대기 중인 이동을 취소하고 대기열 맨 앞에 선다. 이미 나가는 이동 뒤에는 선다(그래야 멈춘 채 남음).
            // FR-L1(Stop LWW): 뗌 Stop은 호출측이 제스처 토큰(ct)을 전달 — 뗌→즉시 재누름 시 BeginPtzGesture가
            // 이 토큰을 취소해 Gate 대기 중인 Stop을 드롭하고 새 ContinuousMove가 즉시 진행한다
            // (ONVIF §5.3.2: 새 ContinuousMove가 이전 모션을 자동 대체 — Stop 생략 안전). 재누름이 없으면 정상 수행.
            var __swGate = System.Diagnostics.Stopwatch.StartNew();   // [진단] FR-L3: Stop 게이트 대기(재누름 지연 정량화)
            await ctx.Gate.WaitStopAsync(ct).ConfigureAwait(false);
            __swGate.Stop();
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return;
                var __swStop = System.Diagnostics.Stopwatch.StartNew();   // [진단] FR-L3: WCF Stop 왕복(Digest 401 재왕복 시 커짐 — Phase 2 판단 근거)
                await CallAsync(cameraId, ctx, _onvif.StopPTZ(client, ctx.ProfileToken, true, true), ct).ConfigureAwait(false);
                __swStop.Stop();
                _log?.Info($"[PTZ] Stop cam={cameraId} gateWait={__swGate.ElapsedMilliseconds}ms WCF={__swStop.ElapsedMilliseconds}ms");
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[PTZ] Stop 실패 cam={cameraId}: {Mask(ex.Message)}"); }
    }

    /*──────────────── ONVIF 프리셋(FR-C1) — 카메라가 진실원 ────────────────*/
    // 전부 워밍 캐시(ctx.Model.PtzClient + ctx.ProfileToken) 재사용 + ctx.Gate 직렬(I-05: 동일 WCF 채널 병렬 금지).
    // 구 로컬 DB 프리셋(PtzPresetStore)은 팝업에서 분리(OQ-4/5) — 코드/테이블은 롤백 대비 유지.

    public async Task<System.Collections.Generic.IReadOnlyList<PtzPresetInfo>?> GetPresetsAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return null;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return null;
                var list = await CallAsync(cameraId, ctx, _onvif.GetPTZPreset(client, ctx.ProfileToken), ct).ConfigureAwait(false);
                if (list == null) return null;   // SOAP 실패(OnvifService가 로그 후 null) — "조회 실패"로 구분(FR-C3)
                var result = new System.Collections.Generic.List<PtzPresetInfo>(list.Count);
                foreach (var p in list)
                {
                    if (string.IsNullOrEmpty(p?.Token)) continue;   // 토큰 없는 항목은 이동/삭제 불가 — 제외
                    result.Add(new PtzPresetInfo(p.Token, p.Name ?? string.Empty));
                }
                return result;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] GetPresets 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
    }

    public async Task<bool> GotoPresetAsync(string cameraId, string presetToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(presetToken)) return false;
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        CancelKeepAlive(ctx);   // FR-22
        CancelDrag(ctx);
        ctx.PanTiltStale = true;
        ctx.ZoomStale = true;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            ctx.Busy = true;   // AbsoluteMove와 동일 — 프리셋 이동 중 표시
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var moved = await CallAsync(cameraId, ctx, _onvif.GoPTZPreset(client, null!, ctx.ProfileToken, presetToken), ct).ConfigureAwait(false);   // 속도 null=카메라 기본(ToWsdl null-안전)
                ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshRetry);
                return moved;
            }
            finally { ctx.Busy = false; ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] GotoPreset 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> SetPresetAsync(string cameraId, string presetName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presetName)) return false;
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                return await CallAsync(cameraId, ctx, _onvif.SetPTZPreset(client, ctx.ProfileToken, presetName, null!), ct).ConfigureAwait(false);   // presetToken null=카메라가 자동 할당
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] SetPreset 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> RemovePresetAsync(string cameraId, string presetToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(presetToken)) return false;
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                return await CallAsync(cameraId, ctx, _onvif.DeletePTZPreset(client, ctx.ProfileToken, presetToken), ct).ConfigureAwait(false);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] RemovePreset 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> SetHomePresetAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                return await CallAsync(cameraId, ctx, _onvif.SetHomePreset(client, ctx.ProfileToken), ct).ConfigureAwait(false);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] SetHome 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> GotoHomePresetAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        CancelKeepAlive(ctx);   // FR-22
        CancelDrag(ctx);
        ctx.PanTiltStale = true;
        ctx.ZoomStale = true;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            ctx.Busy = true;
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var moved = await CallAsync(cameraId, ctx, _onvif.GoHomePreset(client, null!, ctx.ProfileToken), ct).ConfigureAwait(false);   // Home 미지정 카메라는 폴트 → false
                ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshRetry);
                return moved;
            }
            finally { ctx.Busy = false; ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] GoHome 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    /*──────────────── 영상 옵션(Imaging) ────────────────*/

    public bool IsImagingCapable(string cameraId) => _ctx.TryGetValue(cameraId, out var c) && c.ImagingPossible;

    public async Task<CameraImagingState?> GetImagingAsync(string cameraId, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return null;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.ImagingClient is not { } img) return null;
                var s = await CallAsync(cameraId, ctx, img.GetImagingSettingsAsync(ctx.VsToken), ct).ConfigureAwait(false);
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

    public async Task<bool> SetIrCutFilterAsync(string cameraId, string mode, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return false;
        if (!Enum.TryParse<OnvifImaging.IrCutFilterMode>(mode, true, out var irc)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // read-modify-write — 타 필드(밝기/대비/Focus 등) 보존, 변경 필드만 Specified.
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.ImagingClient is not { } img) return false;
                var s = await CallAsync(cameraId, ctx, img.GetImagingSettingsAsync(ctx.VsToken), ct).ConfigureAwait(false);
                if (s == null) return false;
                s.IrCutFilter = irc;
                s.IrCutFilterSpecified = true;
                await CallAsync(cameraId, ctx, img.SetImagingSettingsAsync(ctx.VsToken, s, false), ct).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] SetIrCutFilter 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    public async Task<bool> SetAutoFocusAsync(string cameraId, bool auto, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.ImagingClient is not { } img) return false;
                var s = await CallAsync(cameraId, ctx, img.GetImagingSettingsAsync(ctx.VsToken), ct).ConfigureAwait(false);
                if (s == null) return false;
                s.Focus ??= new OnvifImaging.FocusConfiguration20();
                s.Focus.AutoFocusMode = auto ? OnvifImaging.AutoFocusMode.AUTO : OnvifImaging.AutoFocusMode.MANUAL;
                await CallAsync(cameraId, ctx, img.SetImagingSettingsAsync(ctx.VsToken, s, false), ct).ConfigureAwait(false);
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
    public async Task<bool> StartFocusAsync(string cameraId, int direction, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return false;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.ImagingClient is not { } img) return false;
                await EnsureFocusOptionsAsync(cameraId, ctx, img, ct).ConfigureAwait(false);   // GetMoveOptions 1회 캐시
                var mag = PtzFocusMath.ClampMagnitude(FocusMoveSpeed, ctx.FocusSpeedMin, ctx.FocusSpeedMax);
                // ContinuousFocus(속도) — RelativeFocus 미지원 카메라 대응(SetAutoFocus처럼 ImagingClient 직접). 정지는 StopFocusAsync.
                var move = new OnvifImaging.FocusMove
                {
                    Continuous = new OnvifImaging.ContinuousFocus { Speed = (float)((direction >= 0 ? 1 : -1) * mag) }
                };
                await CallAsync(cameraId, ctx, img.MoveAsync(ctx.VsToken, move), ct).ConfigureAwait(false);
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] StartFocus 실패 cam={cameraId}: {Mask(ex.Message)}"); return false; }
    }

    /// <summary>포커스 연속 이동 정지(뗌/캡처분실/닫기) — ImagingClient.Stop. PTZ StopAsync와 별개 모터 경로. (FR-PH-02/03/05)</summary>
    public async Task StopFocusAsync(string cameraId, CancellationToken ct = default)
    {
        if (!TryImaging(cameraId, out var ctx)) return;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.ImagingClient is { } img)
                    await CallAsync(cameraId, ctx, img.StopAsync(ctx.VsToken), ct).ConfigureAwait(false);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[PTZ] StopFocus 실패 cam={cameraId}: {Mask(ex.Message)}"); }
    }

    /// <summary>ContinuousFocus 속도 범위(GetMoveOptions)를 1회 조회해 캐시(FR-PH-10). 미지원/실패면 폴백 표시(±FocusMoveSpeed). Gate 보유 중 호출.</summary>
    private async Task EnsureFocusOptionsAsync(string cameraId, CamCtx ctx, OnvifImaging.ImagingPortClient img, CancellationToken ct)
    {
        if (ctx.FocusOptLoaded) return;
        ctx.FocusOptLoaded = true;   // 1회만 시도(실패해도 폴백)
        try
        {
            var opt = await CallAsync(cameraId, ctx, img.GetMoveOptionsAsync(ctx.VsToken), ct).ConfigureAwait(false);
            var sp = opt?.Continuous?.Speed;
            if (sp != null && sp.Max > sp.Min) { ctx.FocusSpeedMin = sp.Min; ctx.FocusSpeedMax = sp.Max; }
            else _log?.Info("[PTZ] 포커스 ContinuousFocus 속도 옵션 없음 — 기본 폴백.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { _log?.Warning($"[PTZ] GetMoveOptions 실패(포커스 폴백): {Mask(ex.Message)}"); }
    }

    /*──────────────── RTSP 스트림 URL 조회(Onvif조회 모드 — CameraPopup_RtspSource_Priority FR-03/07/08) ────────────────*/

    public async Task<string?> ResolveStreamUriAsync(string cameraId, IConnectionModel conn, bool preferSub = true, CancellationToken ct = default,
        int targetWidth = 0, int targetHeight = 0)
    {
        if (conn == null) return null;
        bool sized = targetWidth > 0 && targetHeight > 0;
        // 캐시 fast-path(감사 perf-L6): 워밍된 재오픈은 EnsureReady 전체 재수행(Gate 2회+비PTZ 경고 로그) 없이
        // Gate 1회로 즉시 반환. 캐시는 Resolve 성공 시에만 채워지고 Release 시 CamCtx째 사라지므로 안전.
        if (_ctx.TryGetValue(cameraId, out var cached) && cached.ConnKey == ConnKeyOf(conn))   // 접속 정보가 바뀌었으면 옛 주소를 돌려주지 않는다
        {
            await cached.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!sized && cached.ResolvedStreamUri != null && cached.ResolvedPreferSub == preferSub)
                    return cached.ResolvedStreamUri;
            }
            finally { cached.Gate.Release(); }
        }
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
                if (!sized && ctx.ResolvedStreamUri != null && ctx.ResolvedPreferSub == preferSub)
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
                // 그릴 크기를 알면 "그 크기를 덮는 가장 낮은 해상도"(작은 타일 = 서브, 큰 상자 = 메인), 모르면 preferSub 규칙.
                var token = sized
                    ? OnvifProfileSelector.Select(infos, preferSub, targetWidth, targetHeight)
                    : OnvifProfileSelector.Select(infos, preferSub);
                if (!string.IsNullOrEmpty(token) && ctx.ResolvedUris.TryGetValue(token, out var known))
                    return known;   // 같은 프로파일은 다시 묻지 않는다(타일 60개가 카메라 8대를 나눠 쓴다)
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
                var uri = (await CallAsync(cameraId, ctx, media.GetStreamUriAsync(setup, token), ct).ConfigureAwait(false))?.Uri;
                if (string.IsNullOrWhiteSpace(uri))
                {
                    _log?.Warning($"[PTZ] GetStreamUri 빈 응답 cam={cameraId} profile={token}.");
                    return null;
                }

                ctx.ResolvedUris[token] = uri;
                if (!sized)
                {
                    ctx.ResolvedStreamUri = uri;
                    ctx.ResolvedProfileToken = token;
                    ctx.ResolvedPreferSub = preferSub;
                }
                _log?.Info($"[PTZ] StreamUri 조회 cam={cameraId} profile={token} preferSub={preferSub} target={targetWidth}x{targetHeight} uri={Mask(uri)}");
                return uri;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] StreamUri 조회 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
    }

    /// <summary>메인 스트림 해상도 — 워밍 때 받아 둔 프로파일만 읽는다(추가 ONVIF 호출 없음). ROI 기준 해상도 채우기.</summary>
    public async Task<CameraVideoResolution?> GetMainVideoResolutionAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx)) return null;
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var profiles = ctx.Model?.Profiles;
                if (profiles == null || profiles.Count == 0) return null;
                var infos = profiles
                    .Where(p => p != null)
                    .Select(p => new OnvifProfileSelector.ProfileInfo(
                        p.token ?? string.Empty,
                        p.VideoEncoderConfiguration?.Resolution?.Width ?? 0,
                        p.VideoEncoderConfiguration?.Resolution?.Height ?? 0,
                        p.AudioEncoderConfiguration != null))
                    .ToList();
                var main = OnvifProfileSelector.MainResolution(infos);
                if (main is not { } m)
                {
                    _log?.Info($"[PTZ] 메인 해상도 없음 cam={cameraId} — 프로파일에 해상도 정보 없음(profiles={profiles.Count}).");
                    return null;
                }
                return new CameraVideoResolution(m.Width, m.Height);
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { _log?.Error($"[PTZ] 메인 해상도 조회 실패 cam={cameraId}: {Mask(ex.Message)}"); return null; }
    }

    private bool TryImaging(string cameraId, out CamCtx ctx)
    {
        ctx = null!;
        if (!_ctx.TryGetValue(cameraId, out var c) || !c.ImagingPossible
            || (c.Model is { } m ? m.ImagingClient == null : c.Conn == null) || string.IsNullOrEmpty(c.VsToken)) return false;
        ctx = c;
        return true;
    }

    public void Release(string cameraId)
    {
        // H2: 딕셔너리에서만 제거. SemaphoreSlim은 Dispose하지 않는다 — 진행 중 Move/GetStatus 태스크가
        // 동일 ctx.Gate를 await/Release 중일 수 있어, 여기서 Dispose하면 ObjectDisposedException 경합.
        // SemaphoreSlim은 WaitHandle 미사용 시 정리할 핸들이 없어 미Dispose 비용 무시 가능. 멱등.
        if (_ctx.TryRemove(cameraId, out var ctx))
        {
            CancelKeepAlive(ctx);
            CancelDrag(ctx);
            CancelStatusRefresh(ctx);
        }
    }

    /*──────────────── 내부 헬퍼 ────────────────*/

    private static string ConnKeyOf(IConnectionModel conn)
        => $"{conn.IpAddress}:{conn.PortOnvif}\n{conn.Username}\n{conn.Password}";

    /// <summary>
    /// 카메라 캐시 — 접속 정보(주소 · 포트 · 계정)가 캐시를 만든 때와 다르면 옛 연결을 버리고 새로 만든다. 운용 중 장비 설정에서
    /// 비밀번호 · 주소를 고쳐도 호스트가 옛 연결을 계속 써서, 호스트를 다시 띄울 때까지 PTZ · 영상 주소가 실패하던 것.
    /// </summary>
    private CamCtx CtxFor(string cameraId, string key)
    {
        while (true)
        {
            var ctx = _ctx.GetOrAdd(cameraId, _ => new CamCtx { ConnKey = key });
            if (ctx.ConnKey == key) return ctx;
            if (_ctx.TryRemove(new System.Collections.Generic.KeyValuePair<string, CamCtx>(cameraId, ctx)))
            {
                _log?.Info($"[PTZ] 접속 정보 바뀜 cam={cameraId} — 옛 연결을 버리고 다시 붙는다.");
                CancelKeepAlive(ctx);
                CancelDrag(ctx);
                CancelStatusRefresh(ctx);
                AbortClients(ctx.Model);
            }
        }
    }

    /// <summary>모델의 WCF 클라이언트를 끊는다(진행 중 호출은 곧바로 실패로 끝난다). 던지지 않는다.</summary>
    private static void AbortClients(ICameraOnvifModel? model)
    {
        if (model == null) return;
        foreach (var client in new object?[] { model.PtzClient, model.MediaClient, model.ImagingClient })
        {
            try { (client as System.ServiceModel.ICommunicationObject)?.Abort(); }
            catch (Exception) { /* 이미 닫힘 · 끊김 */ }
        }
    }

    /// <summary>
    /// 게이트 안 ONVIF 호출 한 번. 부른 쪽(<paramref name="ct"/> = 호출 쪽 제한 시간)이 포기했는데 호출이
    /// <see cref="PtzMotionPolicy.StallThreshold"/> 넘게 돌고 있으면 카메라가 응답하지 않는 것으로 보고 연결을 끊는다
    /// (<see cref="AbandonStalled"/>) — 호출이 곧바로 실패로 끝나 게이트가 풀리고, 다음 명령은 새로 붙는다(준비 → 초기화).
    /// 정상 카메라에서 막 끝나 가는 호출은 끊지 않는다(문턱 전까지 기다린다) — 순서(정지가 이동 뒤에 도착)를 지킨다.
    /// </summary>
    private async Task<T> CallAsync<T>(string cameraId, CamCtx ctx, Task<T> call, CancellationToken ct)
    {
        if (call.IsCompleted || !ct.CanBeCanceled) return await call.ConfigureAwait(false);
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await call.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!call.IsCompleted)
        {
            var left = PtzMotionPolicy.StallThreshold - started.Elapsed;
            if (left > TimeSpan.Zero) await Task.WhenAny(call, Task.Delay(left)).ConfigureAwait(false);
            if (call.IsCompleted) return await call.ConfigureAwait(false);
            AbandonStalled(cameraId, ctx, started.Elapsed);
            _ = call.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            throw;
        }
    }

    /// <summary>배경 호출(부른 쪽 제한 시간 없음) — <see cref="PtzMotionPolicy.BackgroundCallTimeout"/> 를 제한 시간으로 <see cref="CallAsync{T}"/>.</summary>
    private async Task<T> BackgroundCallAsync<T>(string cameraId, CamCtx ctx, Task<T> call, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(PtzMotionPolicy.BackgroundCallTimeout);
        return await CallAsync(cameraId, ctx, call, deadline.Token).ConfigureAwait(false);
    }

    private Task BackgroundCallAsync(string cameraId, CamCtx ctx, Task call, CancellationToken token)
        => BackgroundCallAsync(cameraId, ctx, call.ContinueWith(t => { t.GetAwaiter().GetResult(); return true; },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default), token);

    private async Task CallAsync(string cameraId, CamCtx ctx, Task call, CancellationToken ct)
        => await CallAsync(cameraId, ctx, call.ContinueWith(t => { t.GetAwaiter().GetResult(); return true; },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default), ct).ConfigureAwait(false);

    /// <summary>응답 없는 카메라 — 연결을 끊고 모델을 비운다(다음 준비가 새로 만든다). 좌표 공간 · 영상 주소 캐시는 둔다(같은 카메라).</summary>
    private void AbandonStalled(string cameraId, CamCtx ctx, TimeSpan elapsed)
    {
        var model = ctx.Model;
        if (model == null) return;
        ctx.Model = null;
        CancelKeepAlive(ctx);
        CancelStatusRefresh(ctx);
        _log?.Warning($"[PTZ] 카메라 응답 없음 cam={cameraId} — {elapsed.TotalMilliseconds:0} ms 지나도 답이 없어 연결을 끊는다(다음 명령이 다시 붙는다).");
        AbortClients(model);
    }

    /// <summary>연속 이동 유지 재전송 시작(FR-22). 토큰은 이동 세대(<paramref name="epoch"/>)에 묶인다 —
    /// 이 이동이 나가는 사이 정지가 왔다면 이미 취소돼 곧바로 끝난다(정지 뒤에 이동이 다시 나가는 일 없음).</summary>
    private void StartKeepAlive(string cameraId, CamCtx ctx, PtzSpeedDto speed, CancellationToken epoch)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(epoch);
        Interlocked.Exchange(ref ctx.KeepAlive, cts)?.Cancel();
        _ = KeepAliveLoopAsync(cameraId, ctx, speed, cts.Token);
    }

    private static void CancelKeepAlive(CamCtx ctx) => Interlocked.Exchange(ref ctx.KeepAlive, null)?.Cancel();

    private static void CancelDrag(CamCtx ctx) => Interlocked.Exchange(ref ctx.Drag, null)?.Cancel();

    private static void CancelStatusRefresh(CamCtx ctx) => Interlocked.Exchange(ref ctx.StatusRefresh, null)?.Cancel();

    /*──────────────── 영상 위 드래그(DragMove) ────────────────*/

    public Task<PtzDragOutcome> DragMoveAsync(string cameraId, double viewX, double viewY, double viewAspect, CancellationToken ct = default)
        => DragMoveAsync(cameraId, viewX, viewY, viewAspect, 0d, ct);

    public async Task<PtzDragOutcome> DragMoveAsync(string cameraId, double viewX, double viewY, double viewAspect, double speed, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx) || ctx.Spaces is not { } sp)
            return PtzDragOutcome.NotSent("not-ready");
        // 속도 우선순위: 팝업 설정 속도(>0) → 카메라 기본 속도 → (둘 다 없으면) RelativeMove 는 Speed 생략 = 카메라 기본, 시간 제한 연속은 PulseSpeed.
        double? pickedSpeed = speed > 0 && double.IsFinite(speed) ? Math.Clamp(speed, 0.1, 1d) : ctx.DefaultPanTiltSpeed;
        var moveSpeed = ToPanTiltSpeed(sp, pickedSpeed);

        CancelKeepAlive(ctx);      // 누름 이동의 유지 재전송이 이 이동을 덮지 않게
        CancelStatusRefresh(ctx);  // 배경 위치 읽기가 이 드래그 앞에 줄 서지 않게(이미 나간 것은 끝까지 간다)
        // 최신 우선: 아직 게이트를 못 얻은 이전 드래그 · 시간 제한 이동의 대기를 버린다 → 밀린 드래그가 쌓이지 않는다.
        var mine = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Interlocked.Exchange(ref ctx.Drag, mine)?.Cancel();
        var token = mine.Token;

        PtzDragPlan plan;
        CancellationToken epoch;
        var gateWait = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 이동 세대로 줄을 선다 — 정지가 오면 대기 중인 드래그는 취소된다(정지 우선, FR-22).
            epoch = await ctx.Gate.WaitMoveAsync(token).ConfigureAwait(false);
            gateWait.Stop();
            try
            {
                token.ThrowIfCancellationRequested();   // 게이트를 받는 사이 더 새 드래그가 왔으면 보내지 않는다
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client)
                    return PtzDragOutcome.NotSent("not-ready");
                var known = KnownPosition(ctx);
                plan = PtzDragMath.Plan(ToDragSpaces(sp), known, viewX, viewY, viewAspect, null, pickedSpeed ?? PtzDragMath.PulseSpeed);
                var call = System.Diagnostics.Stopwatch.StartNew();
                switch (plan.Kind)
                {
                    case PtzDragMoveKind.RelativeFov:
                        await CallAsync(cameraId, ctx, client.RelativeMoveAsync(ctx.ProfileToken,
                            PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildPanTilt(plan.Pan, plan.Tilt, sp.RelFovUri)), moveSpeed), ct).ConfigureAwait(false);
                        ctx.PanTiltStale = true;   // 화각 단위 이동 — 정규화 좌표로는 얼마나 갔는지 모른다
                        break;
                    case PtzDragMoveKind.RelativeGeneric:
                        await CallAsync(cameraId, ctx, client.RelativeMoveAsync(ctx.ProfileToken,
                            PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildPanTilt(plan.Pan, plan.Tilt, sp.RelPtUri)), moveSpeed), ct).ConfigureAwait(false);
                        if (known.PanTiltKnown && ctx.LastPosition is { } before)
                        {
                            // 보낸 만큼 추적(다시 묻지 않는다). 이동 중에 또 끌면 어긋날 수 있다 — 다음 배경 읽기가 바로잡는다.
                            var (toPan, toTilt) = PtzDragMath.AbsoluteTarget(before.Pan, before.Tilt, plan.Pan, plan.Tilt,
                                sp.AbsXMin, sp.AbsXMax, sp.AbsYMin, sp.AbsYMax, PtzLensModel.Default.PanSpanDeg >= 360d);
                            ctx.LastPosition = before with { Pan = toPan, Tilt = toTilt };
                        }
                        break;
                    case PtzDragMoveKind.Absolute:
                        // 줌은 싣지 않는다(null = 줌 그대로).
                        await CallAsync(cameraId, ctx, client.AbsoluteMoveAsync(ctx.ProfileToken,
                            PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildAbsolute(plan.Pan, plan.Tilt, null, sp.AbsPtUri, null)), moveSpeed), ct).ConfigureAwait(false);
                        if (ctx.LastPosition is { } at) ctx.LastPosition = at with { Pan = plan.Pan, Tilt = plan.Tilt };
                        break;
                    case PtzDragMoveKind.ContinuousPulse:
                        var velocity = new PtzSpeedDto
                        {
                            PanTilt = new Vector2DDto
                            {
                                X = (float)PtzVelocityMath.ScaleToRange(plan.Pan, sp.ContPtXMin, sp.ContPtXMax),
                                Y = (float)PtzVelocityMath.ScaleToRange(plan.Tilt, sp.ContPtYMin, sp.ContPtYMax),
                                Space = sp.ContPtUri,
                            },
                            Zoom = new Vector1DDto { X = 0f, Space = sp.ContZoomUri },
                        };
                        ctx.PanTiltStale = true;
                        if (!await CallAsync(cameraId, ctx, _onvif.MovePTZ(client, velocity, ctx.ProfileToken, PtzMotionPolicy.MoveTimeout), ct).ConfigureAwait(false))
                            return PtzDragOutcome.NotSent("failed");
                        break;
                    default:
                        return PtzDragOutcome.NotSent(plan.Pan == 0 && plan.Tilt == 0 && (viewX != 0 || viewY != 0) ? "no-space" : "zero");
                }
                call.Stop();
                _log?.Info($"[PTZ] Drag cam={cameraId} kind={plan.Kind} view=({viewX:F3},{viewY:F3}) → ({plan.Pan:F4},{plan.Tilt:F4})×{plan.Steps} speed={pickedSpeed?.ToString("F2") ?? "camera"} zoomKnown={known.ZoomKnown} gateWait={gateWait.ElapsedMilliseconds}ms WCF={call.ElapsedMilliseconds}ms");
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return PtzDragOutcome.NotSent("superseded"); }
        catch (Exception ex)
        {
            _log?.Error($"[PTZ] Drag 실패 cam={cameraId}: {Mask(ex.Message)}");
            ctx.PanTiltStale = true;
            ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshDelay);
            return PtzDragOutcome.NotSent("failed");
        }

        if (plan.Kind == PtzDragMoveKind.RelativeFov && plan.Steps > 1)
        {
            // 화각 상대 공간이 한 폭보다 좁다(보통 ±1 = 반 폭) — 나머지 몫은 앞 이동이 멈춘 뒤 이어 보낸다. 한 번에 자르면 반 폭밖에
            // 못 가고, 움직이는 중에 다음 RelativeMove 를 보내면 카메라가 앞 이동을 버린다. 배경에서 돈다(요청 제한 시간과 무관) —
            // 새 드래그 · 정지 · 누름 이동 · 프리셋이 CancelDrag 로 끊는다.
            var follow = new CancellationTokenSource();
            if (Interlocked.CompareExchange(ref ctx.Drag, follow, mine) == mine)
                _ = FollowFovStepsAsync(cameraId, ctx, sp, plan, moveSpeed, follow.Token);
        }
        else if (plan.Kind == PtzDragMoveKind.ContinuousPulse)
        {
            // 옛 방식(위치 · 줌을 모를 때만): 드래그 길이만큼 움직인 뒤 정지. 더 새 드래그 · 정지 · 누름 이동이 오면 이 정지는 버린다
            // (새 명령이 이전 움직임을 대체한다 — ONVIF §5.3.2). 정지가 끝내 못 나가도 카메라는 2초 뒤 스스로 멈춘다.
            try
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(token, epoch);
                await Task.Delay(plan.PulseMs, wait.Token).ConfigureAwait(false);
                await StopCoreAsync(cameraId, ctx, token).ConfigureAwait(false);
                ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshDelay);
            }
            catch (OperationCanceledException) { /* 새 제스처가 인계 */ }
        }
        else if (ctx.PanTiltStale || ctx.ZoomStale
                 || Environment.TickCount64 - Volatile.Read(ref ctx.LastPositionTick) > PtzMotionPolicy.StatusMaxAge.TotalMilliseconds)
        {
            // 보낸 뒤에 배경에서 읽는다 — 이 드래그는 기다리지 않았고, 다음 드래그가 새 줌(화각)을 쓴다.
            ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshRetry);
        }
        return new PtzDragOutcome(true, plan.Kind, plan.Pan, plan.Tilt, null);
    }

    /// <summary>나머지 몫(2번째부터)을 앞 이동이 멈출 때마다 하나씩 보낸다. 끊기면(새 조작) 조용히 끝난다.</summary>
    private async Task FollowFovStepsAsync(string cameraId, CamCtx ctx, SpaceInfo sp, PtzDragPlan plan, OnvifSolution.Ptz.PTZSpeed? speed, CancellationToken token)
    {
        try
        {
            for (int step = 2; step <= plan.Steps; step++)
            {
                await WaitUntilStoppedAsync(cameraId, ctx, token).ConfigureAwait(false);
                await ctx.Gate.WaitMoveAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (ctx.Model?.PtzClient is not { } client) return;
                    await BackgroundCallAsync(cameraId, ctx, client.RelativeMoveAsync(ctx.ProfileToken,
                        PtzVectorMapper.ToPtzVector(PtzVectorMapper.BuildPanTilt(plan.Pan, plan.Tilt, sp.RelFovUri)), speed), token).ConfigureAwait(false);
                }
                finally { ctx.Gate.Release(); }
                _log?.Info($"[PTZ] Drag cam={cameraId} 나눠 보냄 {step}/{plan.Steps} ({plan.Pan:F4},{plan.Tilt:F4})");
            }
            ScheduleStatusRefresh(cameraId, ctx, PtzMotionPolicy.StatusRefreshRetry);
        }
        catch (OperationCanceledException) { /* 새 조작이 인계 */ }
        catch (Exception ex) { _log?.Error($"[PTZ] Drag 나눠 보내기 실패 cam={cameraId}: {Mask(ex.Message)}"); }
    }

    /// <summary>앞 이동이 멈출 때까지(MoveStatus IDLE) 짧게 묻는다. MoveStatus 를 안 주는 카메라는 어림 시간만 기다린다.</summary>
    private async Task WaitUntilStoppedAsync(string cameraId, CamCtx ctx, CancellationToken token)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(PtzMotionPolicy.StepPollInterval, token).ConfigureAwait(false);   // 막 보낸 이동이 아직 시작 전(IDLE)일 수 있다
        while (sw.Elapsed < PtzMotionPolicy.StepIdleTimeout)
        {
            var moving = await ReadMovingAsync(cameraId, ctx, token).ConfigureAwait(false);
            if (moving == false) return;
            if (moving == null)
            {
                var left = PtzMotionPolicy.StepEstimate - sw.Elapsed;
                if (left > TimeSpan.Zero) await Task.Delay(left, token).ConfigureAwait(false);
                return;
            }
            await Task.Delay(PtzMotionPolicy.StepPollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>GetStatus 의 MoveStatus 한 번 읽기(게이트 안). 못 읽거나 MoveStatus 가 없으면 null.</summary>
    private async Task<bool?> ReadMovingAsync(string cameraId, CamCtx ctx, CancellationToken token)
    {
        if (ctx.Model?.PtzClient is not { } client || ctx.StatusUnsupported) return null;
        await ctx.Gate.WaitAsync(token).ConfigureAwait(false);
        OnvifSolution.Ptz.PTZStatus? status;
        try
        {
            token.ThrowIfCancellationRequested();
            try { status = await CallAsync(cameraId, ctx, client.GetStatusAsync(ctx.ProfileToken), token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
        }
        finally { ctx.Gate.Release(); }
        var ms = status?.MoveStatus;
        if (ms == null || (!ms.PanTiltSpecified && !ms.ZoomSpecified)) return null;
        return (ms.PanTiltSpecified && ms.PanTilt == OnvifSolution.Ptz.MoveStatus.MOVING)
            || (ms.ZoomSpecified && ms.Zoom == OnvifSolution.Ptz.MoveStatus.MOVING);
    }

    public async Task<bool?> GetMoveStatusAsync(string cameraId, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx)) return null;
        try { return await ReadMovingAsync(cameraId, ctx, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return null; }
    }

    public (double? PanTilt, double? Zoom) GetDefaultSpeeds(string cameraId)
        => _ctx.TryGetValue(cameraId, out var c) ? (c.DefaultPanTiltSpeed, c.DefaultZoomSpeed) : (null, null);

    /// <summary>
    /// 카메라 기본 속도 읽기 — 워밍 때 받아 둔 프로파일의 PTZConfiguration.DefaultPTZSpeed(= GetConfiguration 값, 추가 호출 없음)를
    /// 노드 속도 공간(PanTiltSpeedSpace · ZoomSpeedSpace = GetConfigurationOptions 의 속도 범위와 같은 값)으로 0.1..1 정규화.
    /// </summary>
    private void ReadDefaultSpeeds(string cameraId, CamCtx ctx, SpaceInfo sp)
    {
        var profile = ctx.Model?.Profiles?.FirstOrDefault(p => p?.token == ctx.ProfileToken) ?? ctx.Model?.Profiles?.FirstOrDefault(p => p?.PTZConfiguration != null);
        var speed = profile?.PTZConfiguration?.DefaultPTZSpeed;
        ctx.DefaultPanTiltSpeed = speed?.PanTilt is { } pt ? NormalizeSpeed(pt.x, sp.PtSpeedMin, sp.PtSpeedMax) : null;
        ctx.DefaultZoomSpeed = speed?.Zoom is { } z ? NormalizeSpeed(z.x, sp.ZSpeedMin, sp.ZSpeedMax) : null;
        _log?.Info($"[PTZ] 카메라 기본 속도 cam={cameraId} panTilt={ctx.DefaultPanTiltSpeed?.ToString("F2") ?? "없음"} zoom={ctx.DefaultZoomSpeed?.ToString("F2") ?? "없음"}");
    }

    /// <summary>속도 공간 값 → 0.1..1(팝업 속도 슬라이더와 같은 단위). 범위가 이상하면 0..1 로 본다.</summary>
    public static double? NormalizeSpeed(double value, double min, double max)
    {
        if (!double.IsFinite(value)) return null;
        if (!(max > min)) (min, max) = (0d, 1d);
        return Math.Clamp((value - min) / (max - min), 0.1, 1d);
    }

    /// <summary>0.1..1 속도 → RelativeMove · AbsoluteMove 의 Speed(속도 공간으로 펼침). null 이면 Speed 생략(카메라 기본).</summary>
    private static OnvifSolution.Ptz.PTZSpeed? ToPanTiltSpeed(SpaceInfo sp, double? speed)
    {
        if (speed is not { } s) return null;
        double min = sp.PtSpeedMin, max = sp.PtSpeedMax;
        if (!(max > min)) (min, max) = (0d, 1d);
        float v = (float)(min + (s * (max - min)));
        return new OnvifSolution.Ptz.PTZSpeed { PanTilt = new OnvifSolution.Ptz.Vector2D { x = v, y = v, space = sp.PtSpeedUri } };
    }

    private static PtzKnownPosition KnownPosition(CamCtx ctx)
        => ctx.LastPosition is { } p
            ? new PtzKnownPosition(p.Pan, p.Tilt, p.Zoom, !ctx.PanTiltStale, !ctx.ZoomStale)
            : PtzKnownPosition.Unknown;

    private static PtzDragSpaces ToDragSpaces(SpaceInfo sp) => new(
        sp.HasRelFov, sp.RelFovXMin, sp.RelFovXMax, sp.RelFovYMin, sp.RelFovYMax,
        sp.HasRel, sp.RelXMin, sp.RelXMax, sp.RelYMin, sp.RelYMax,
        sp.HasAbs, sp.AbsXMin, sp.AbsXMax, sp.AbsYMin, sp.AbsYMax,
        sp.AbsZMin, sp.AbsZMax,
        sp.HasCont);

    /// <summary>
    /// 위치 · 줌을 배경에서 다시 읽어 캐시에 담는다(드래그 환산이 쓴다). 드래그 경로에서는 절대 기다리지 않는다.
    /// 카메라가 아직 움직이는 중이면 멈출 때까지 몇 번 더 읽는다. 새 이동이 오면 취소된다.
    /// </summary>
    private void ScheduleStatusRefresh(string cameraId, CamCtx ctx, TimeSpan delay)
    {
        if (ctx.StatusUnsupported) return;
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref ctx.StatusRefresh, cts)?.Cancel();
        _ = RefreshStatusAsync(cameraId, ctx, delay, cts.Token);
    }

    private async Task RefreshStatusAsync(string cameraId, CamCtx ctx, TimeSpan delay, CancellationToken token)
    {
        try
        {
            for (int attempt = 0; attempt < PtzMotionPolicy.StatusRefreshMaxAttempts; attempt++)
            {
                var wait = attempt == 0 ? delay : PtzMotionPolicy.StatusRefreshRetry;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, token).ConfigureAwait(false);
                if (ctx.Model?.PtzClient is not { } client) return;
                OnvifSolution.Ptz.PTZStatus? status;
                await ctx.Gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    status = await BackgroundCallAsync(cameraId, ctx, client.GetStatusAsync(ctx.ProfileToken), token).ConfigureAwait(false);
                }
                finally { ctx.Gate.Release(); }
                var p = status?.Position;
                if (p?.PanTilt == null)
                {
                    ctx.StatusUnsupported = true;   // 위치를 안 알려주는 카메라 — 드래그는 시간 제한 이동으로 간다(다시 묻지 않는다)
                    return;
                }
                var ms = status!.MoveStatus;
                bool moving = ms != null
                    && ((ms.PanTiltSpecified && ms.PanTilt == OnvifSolution.Ptz.MoveStatus.MOVING)
                        || (ms.ZoomSpecified && ms.Zoom == OnvifSolution.Ptz.MoveStatus.MOVING));
                if (token.IsCancellationRequested) return;   // 읽는 사이 새 이동이 시작됐다 — 옛 위치를 "안다"로 표시하지 않는다
                ctx.LastPosition = new PtzPosition(p.PanTilt.x, p.PanTilt.y, p.Zoom?.x ?? 0d, p.PanTilt.space, p.Zoom?.space);
                Volatile.Write(ref ctx.LastPositionTick, Environment.TickCount64);
                if (moving) continue;
                ctx.PanTiltStale = false;
                ctx.ZoomStale = p.Zoom == null;
                return;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ctx.LastPosition == null) ctx.StatusUnsupported = true;   // 한 번도 못 읽었다 — 위치를 안 알려주는 카메라로 본다
            _log?.Warning($"[PTZ] 위치 읽기 실패 cam={cameraId}(드래그는 시간 제한 이동으로 대체): {Mask(ex.Message)}");
        }
    }

    private async Task KeepAliveLoopAsync(string cameraId, CamCtx ctx, PtzSpeedDto speed, CancellationToken token)
    {
        try
        {
            var held = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                await Task.Delay(PtzMotionPolicy.KeepAliveInterval, token).ConfigureAwait(false);
                if (held.Elapsed >= PtzMotionPolicy.MaxHoldDuration)
                {
                    // 뗌(정지)이 끝내 오지 않았다 — 재전송을 끊어 카메라가 2초 안에 스스로 멈추게 한다.
                    _log?.Warning($"[PTZ] 이동 유지 최대 시간({PtzMotionPolicy.MaxHoldDuration.TotalSeconds:0}초) 초과 cam={cameraId} — 재전송 중단.");
                    return;
                }
                await ctx.Gate.WaitMoveAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();   // 게이트를 받는 사이 정지 · 새 이동이 왔으면 보내지 않는다
                    if (ctx.Model?.PtzClient is not { } client) return;
                    await BackgroundCallAsync(cameraId, ctx, _onvif.MovePTZ(client, speed, ctx.ProfileToken, PtzMotionPolicy.MoveTimeout), token).ConfigureAwait(false);
                }
                finally { ctx.Gate.Release(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[PTZ] 이동 유지 재전송 실패 cam={cameraId}: {Mask(ex.Message)}"); }
    }

    private static bool IsCapable(CamCtx c)
        => (c.Model?.IsPtzPossible ?? false) && c.Spaces is { } s && (s.HasRel || s.HasAbs || s.HasRelFov);

    /// <summary>카메라 space(Rel/Abs/Zoom)를 읽어 캐시. 초기화가 프로필과 병렬로 미리 읽은 노드(<c>Model.PtzNode</c>, FR-23)를 쓰고,
    /// 없으면 GetConfigurations → NodeToken → GetNode 로 직접 조회(폴백).</summary>
    private async Task<SpaceInfo?> LoadSpacesAsync(CamCtx ctx)
    {
        var ptz = ctx.Model!.PtzClient!;
        try
        {
            var node = ctx.Model.PtzNode;
            if (node == null)
            {
                var cfgs = await ptz.GetConfigurationsAsync().ConfigureAwait(false);
                var nodeToken = cfgs?.PTZConfiguration?.FirstOrDefault()?.NodeToken;
                if (string.IsNullOrEmpty(nodeToken)) return null;
                node = await ptz.GetNodeAsync(nodeToken).ConfigureAwait(false);
            }
            var irLamp = ParseIrLampCommands(node?.AuxiliaryCommands);
            ctx.IrLampCommands = irLamp;
            _log?.Info($"[PTZ] IR 조명 판정 — 노드 보조 명령=[{string.Join(", ", node?.AuxiliaryCommands ?? Array.Empty<string>())}] → "
                       + (irLamp.Count == 0 ? "직접 제어 지원 안 함(tt:IRLamp|On/Off/Auto 없음)" : $"지원 {string.Join("·", irLamp.Keys)}"));
            var sp = node?.SupportedPTZSpaces;
            if (sp == null) return null;

            // 상대 공간이 여럿일 수 있다 — 일반 공간과 화각(FOV) 공간을 URI 로 가른다(첫 항목을 무조건 쓰면 단위가 뒤바뀐다).
            var relFov = sp.RelativePanTiltTranslationSpace?.FirstOrDefault(s => IsFovSpace(s?.URI));
            var rel = sp.RelativePanTiltTranslationSpace?.FirstOrDefault(s => s != null && !IsFovSpace(s.URI));
            var abs = sp.AbsolutePanTiltPositionSpace?.FirstOrDefault();
            var absZ = sp.AbsoluteZoomPositionSpace?.FirstOrDefault();
            var relZ = sp.RelativeZoomTranslationSpace?.FirstOrDefault();
            var contPt = sp.ContinuousPanTiltVelocitySpace?.FirstOrDefault();   // ContinuousMove 팬틸트 velocity space
            var contZ = sp.ContinuousZoomVelocitySpace?.FirstOrDefault();       // ContinuousMove 줌 velocity space
            var ptSpeed = sp.PanTiltSpeedSpace?.FirstOrDefault();               // 속도 공간(RelativeMove · AbsoluteMove Speed)
            var zSpeed = sp.ZoomSpeedSpace?.FirstOrDefault();

            // FR-PTR-02 진단(code-review MED): 연속속도 범위가 단방향/비대칭(min≥0)이면 역방향(음수)이 0으로 죽어
            // "한 방향만 안 움직임" 오진을 유발 → 1회 경고로 필드 진단 가능하게.
            if ((contPt?.XRange?.Min ?? -1d) >= 0d || (contPt?.YRange?.Min ?? -1d) >= 0d || (contZ?.XRange?.Min ?? -1d) >= 0d)
                _log?.Warning($"[PTZ] 연속속도 범위 단방향/비대칭 — ptX.min={(contPt?.XRange?.Min ?? -1d):F2} ptY.min={(contPt?.YRange?.Min ?? -1d):F2} z.min={(contZ?.XRange?.Min ?? -1d):F2}. 역방향(음수) 이동이 0으로 제한될 수 있음(카메라 space 특성).");

            return new SpaceInfo(
                HasRelFov: relFov != null, RelFovUri: relFov?.URI,
                RelFovXMin: relFov?.XRange?.Min ?? -1d, RelFovXMax: relFov?.XRange?.Max ?? 1d,
                RelFovYMin: relFov?.YRange?.Min ?? -1d, RelFovYMax: relFov?.YRange?.Max ?? 1d,
                HasCont: contPt != null,
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
                ContZMin: contZ?.XRange?.Min ?? -1d, ContZMax: contZ?.XRange?.Max ?? 1d,
                PtSpeedUri: ptSpeed?.URI, PtSpeedMin: ptSpeed?.XRange?.Min ?? 0d, PtSpeedMax: ptSpeed?.XRange?.Max ?? 1d,
                ZSpeedMin: zSpeed?.XRange?.Min ?? 0d, ZSpeedMax: zSpeed?.XRange?.Max ?? 1d);
        }
        catch (Exception ex)
        {
            _log?.Error($"[PTZ] GetNode space 로드 실패: {Mask(ex.Message)}");
            return null;
        }
    }

    /*──────────────── IR 조명(보조 명령) ────────────────*/

    // ONVIF PTZ 규격 "Auxiliary operations": IR 조명 = tt:IRLamp|On · tt:IRLamp|Off · tt:IRLamp|Auto. 대소문자 · tt: 접두가 다른
    // 카메라가 있어(예: "irlamp|on") 둘은 받아 준다. IRLight 같은 다른 이름은 근거가 없어 받지 않는다(추측 구현 금지).
    private static readonly Regex IrLampAux = new(@"^\s*(?:tt:)?IRLamp\|(On|Off|Auto)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>보조 명령 목록 → IR 조명 값별 카메라 글자(처음 나온 것). 없으면 빈 사전.</summary>
    internal static System.Collections.Generic.IReadOnlyDictionary<string, string> ParseIrLampCommands(System.Collections.Generic.IEnumerable<string>? aux)
    {
        var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in aux ?? Array.Empty<string>())
        {
            if (raw is null) continue;
            var m = IrLampAux.Match(raw);
            if (m.Success) map.TryAdd(CanonicalIrLamp(m.Groups[1].Value)!, raw.Trim());
        }
        return map;
    }

    private static string? CanonicalIrLamp(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "on" => Contracts.Protocol.IrLampMode.On,
        "off" => Contracts.Protocol.IrLampMode.Off,
        "auto" => Contracts.Protocol.IrLampMode.Auto,
        _ => null,
    };

    public System.Collections.Generic.IReadOnlyList<string> GetIrLampModes(string cameraId)
        => _ctx.TryGetValue(cameraId, out var c) && c.IrLampCommands is { Count: > 0 } cmds
            ? new[] { Contracts.Protocol.IrLampMode.On, Contracts.Protocol.IrLampMode.Off, Contracts.Protocol.IrLampMode.Auto }.Where(cmds.ContainsKey).ToArray()
            : Array.Empty<string>();

    public string? GetLastIrLamp(string cameraId) => _ctx.TryGetValue(cameraId, out var c) ? c.LastIrLamp : null;

    public async Task<bool> SetIrLampAsync(string cameraId, string mode, CancellationToken ct = default)
    {
        if (!_ctx.TryGetValue(cameraId, out var ctx) || !HasPtz(ctx)) return false;
        var key = CanonicalIrLamp(mode);
        if (key == null || ctx.IrLampCommands is not { } cmds || !cmds.TryGetValue(key, out var aux))
        {
            _log?.Warning($"[PTZ] IR 조명 {mode} 안 보냄 cam={cameraId} — 카메라가 이 보조 명령을 알리지 않았다.");
            return false;
        }
        try
        {
            await ctx.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if ((await LiveModelAsync(cameraId, ctx, ct).ConfigureAwait(false))?.PtzClient is not { } client) return false;
                var reply = await CallAsync(cameraId, ctx, client.SendAuxiliaryCommandAsync(ctx.ProfileToken, aux), ct).ConfigureAwait(false);
                ctx.LastIrLamp = key;
                _log?.Info($"[PTZ] IR 조명 cam={cameraId} {aux} → 응답 '{reply}'");
                return true;
            }
            finally { ctx.Gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { _log?.Error($"[PTZ] IR 조명 실패 cam={cameraId} {aux}: {Mask(ex.Message)}"); return false; }
    }

    /// <summary>ONVIF 화각 상대 공간(<c>…/PanTiltSpaces/TranslationSpaceFov</c>)인가.</summary>
    internal static bool IsFovSpace(string? uri)
        => !string.IsNullOrEmpty(uri) && uri.EndsWith("TranslationSpaceFov", StringComparison.OrdinalIgnoreCase);

    /// <summary>자격증명/엔드포인트 마스킹(NFR-SEC-01). rtsp://user:pass@host → rtsp://***@host.</summary>
    private static string Mask(string? msg)
        => Regex.Replace(msg ?? string.Empty, @"//[^/@\s]+@", "//***@");
}

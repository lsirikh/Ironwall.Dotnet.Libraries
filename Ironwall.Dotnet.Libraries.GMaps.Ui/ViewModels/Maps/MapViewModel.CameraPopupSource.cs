using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : 카메라 팝업 RTSP 소스 Onvif조회 모드 (CameraPopup_RtspSource_Priority FR-05/06/08)
   Created By   : Claude Code
   Created On   : 2026-07-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public partial class MapViewModel
{
    /// <summary>Onvif조회 전체 타임아웃 — InitializePtz 워밍(첫 오픈 &lt;5s) + GetStreamUri 왕복 여유. 초과 시 URL조회 폴백.</summary>
    private const int OnvifResolveTimeoutMs = 12000;

    /// <summary>진행 중 Onvif조회 취소 소스(cameraId당 1개 — 팝업 dedupe로 카메라당 팝업 1개).
    /// 팝업 닫힘/심볼 삭제 시 <see cref="CloseCameraPopupAsync"/>가 취소해 in-flight 조회를 끊는다(M-6, PRD §5-B).</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, CancellationTokenSource> _onvifResolveCts = new();

    /// <summary>
    /// Onvif조회 모드(FR-05/06/08): 팝업은 이미 열려 있음(IsResolvingSource=true) —
    /// ONVIF GetStreamUri로 프로파일별 URL 확보(<see cref="Services.Ptz.IPtzController.ResolveStreamUriAsync"/>)
    /// → 자격증명 조합(<see cref="OnvifRtspUrlComposer"/>) → ConnectionInfo 주입(플레이어 late-bind 연결).
    /// 실패/타임아웃 시 수동 URL(fallback) 폴백, 둘 다 없으면 팝업 닫기.
    /// </summary>
    private async Task ResolveOnvifSourceAndConnectAsync(
        CameraStreamPopupViewModel vm, ICameraDeviceModel? cam, RtspConnectionInfo? fallback)
    {
        string? resolvedUri = null;
        try
        {
            var ptz = ResolvePtzController();
            if (ptz == null)
            {
                _log?.Warning($"[CameraPopup] Onvif조회 불가 — IPtzController 미해석 cam={vm.CameraId} (메인 OnvifServiceModule 등록 확인). URL조회 폴백.");
            }
            else if (cam == null || string.IsNullOrWhiteSpace(cam.IpAddress))
            {
                _log?.Warning($"[CameraPopup] Onvif조회 불가 — 카메라 IP 없음 cam={vm.CameraId}. URL조회 폴백.");
            }
            else
            {
                // 타임아웃 + 닫힘 취소 겸용 CTS(M-6) — 딕셔너리 등록으로 CloseCameraPopupAsync가 취소 가능.
                var cts = new CancellationTokenSource(OnvifResolveTimeoutMs);
                _onvifResolveCts[vm.CameraId] = cts;
                try
                {
                    var conn = new ConnectionModel
                    {
                        IpAddress = cam.IpAddress,
                        PortOnvif = cam.IpPort > 0 ? cam.IpPort : 80,   // EnsurePtzReadyAsync와 동일 규칙(IpPort 겸용 함정 인지 — 분석 §6-3)
                        Username = cam.UserName,
                        Password = cam.UserPassword,
                    };
                    resolvedUri = await ptz.ResolveStreamUriAsync(vm.CameraId, conn, preferSub: true, cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    _onvifResolveCts.TryRemove(vm.CameraId, out _);
                    cts.Dispose();   // Close 경로가 먼저 제거·Dispose했어도 CTS.Dispose는 멱등
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log?.Info($"[CameraPopup] Onvif조회 취소 cam={vm.CameraId} (팝업 닫힘 또는 타임아웃 {OnvifResolveTimeoutMs}ms) — URL조회 폴백.");
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] Onvif조회 예외 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)} — URL조회 폴백.");
        }

        RtspConnectionInfo? final;
        if (!string.IsNullOrWhiteSpace(resolvedUri))
        {
            // 조회 URL엔 자격증명이 없음(VER-01 실측) → 임베드. RTSP Basic/Digest-MD5 협상은 LibVLC가 자동(FR-04).
            var composed = OnvifRtspUrlComposer.Compose(resolvedUri!, cam?.UserName, cam?.UserPassword);
            final = new RtspConnectionInfo
            {
                Url = composed,
                Username = cam?.UserName ?? string.Empty,
                Password = cam?.UserPassword ?? string.Empty,
                IpAddress = cam?.IpAddress ?? string.Empty,
                Port = cam != null && cam.IpPort > 0 ? cam.IpPort : 554,
                // 정보성 필드(스트림 키/재생 미사용) — preferSub 조회 의도 표기. 실제 선택 프로파일은
                // 단일 프로파일 카메라 등에서 메인일 수 있음(M-2, 선택 토큰은 PtzController 로그로 추적).
                StreamType = 1,
            };
            _log?.Info($"[CameraPopup] Onvif조회 성공 cam={vm.CameraId} URL={MaskRtspCredentials(composed)}");
        }
        else
        {
            final = fallback;
            _log?.Warning($"[CameraPopup] Onvif조회 실패 → URL조회 폴백 cam={vm.CameraId} fallback={(fallback != null ? MaskRtspCredentials(fallback.GetFullUrl()) : "(없음)")}");
        }

        await OnUiAsync(() =>
        {
            vm.IsResolvingSource = false;
            if (_cameraPopups == null || !_cameraPopups.Contains(vm)) return;   // 조회 중 닫힘(FR-13/수동) — 주입·연결 생략
            if (final == null)
            {
                _log?.Warning($"[CameraPopup] 재생 가능한 URL 없음(Onvif 실패+수동 URL 없음) cam={vm.CameraId} — 팝업 닫기. 카메라 상세보기 > URLs 탭 입력 또는 ONVIF 계정/포트 확인.");
                _ = CloseCameraPopupAsync(vm);
                return;
            }
            vm.ConnectionInfo = final;   // 세터가 StreamVm 동기 + PropertyChanged → 템플릿 바인딩 → 플레이어 late-bind 연결(FR-05)
        }).ConfigureAwait(false);
    }
}

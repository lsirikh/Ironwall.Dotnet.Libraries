using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 이동/크기/회전 통합 커맨드(adorner 편집). before/after=위치·너비·높이·방위.
   Note         : Map_Edit_Undo_Redo FR-03. before/after는 MarkerEditCompletedEventArgs에서 확보.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class TransformCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly bool _isImage;   // Id 충돌 대비 대상 타입 캡처(이미지↔심볼 오적용 방지)
    private readonly ILogService? _log;   // [DIAG-TFM] undo/redo 적용 추적용(리사이즈 위치드리프트 진단)
    private readonly (PointLatLng pos, double w, double h, double bearing) _before, _after;

    public TransformCommand(IUndoApplyContext ctx, int markerId, bool isImage,
        (PointLatLng pos, double w, double h, double bearing) before,
        (PointLatLng pos, double w, double h, double bearing) after, ILogService? log = null) : base(ctx)
    {
        _id = markerId; _isImage = isImage; _before = before; _after = after; _log = log;
    }

    public override string Description => "심볼 이동/크기/회전";

    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);

    private async Task Apply((PointLatLng pos, double w, double h, double bearing) s, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, _isImage);   // 타입인지 조회 — 이미지 크기가 엉뚱한 Controller에 적용되던 손상 차단
        if (m == null) return;
        // 개별 마커 변형 예외(예: 라인/그룹 마커의 크로스스레드 UI 접근)가 배치 전체를 막지 않도록 흡수.
        // 마커 base 메서드는 내부에서 로그하며, 데이터(위치/크기/방위)는 UI 예외 이전에 이미 반영됨.
        // ★ 위치가 이 편집에서 실제로 변한 경우에만 위치 복원. 순수 리사이즈(before.pos==after.pos)는 위치를
        //   건드리지 않음 — 이미지 Position이 바운즈 중심과 어긋나(desync) 있어도 undo가 엉뚱한 위치로 점프시키지 않음.
        //   (증상: 이미지 이동 undo는 정상인데 리사이즈 undo만 위치가 튀던 문제)
        bool posChanged = _before.pos.Lat != _after.pos.Lat || _before.pos.Lng != _after.pos.Lng;
        // [DIAG-TFM] undo/redo 적용 직전 상태 — 리사이즈 undo에서 위치가 어긋나는지 확정용(undo 1회당 1줄).
        var cB = (m as GMapImageMarker)?.Center ?? m.Position;
        _log?.Info($"[DIAG-TFM] id={_id} img={_isImage} posChanged={posChanged} → target.pos={s.pos.Lat:F6},{s.pos.Lng:F6} w={s.w:F0} h={s.h:F0} | before.pos={_before.pos.Lat:F6},{_before.pos.Lng:F6} after.pos={_after.pos.Lat:F6},{_after.pos.Lng:F6} | 적용前 center={cB.Lat:F6},{cB.Lng:F6} pos={m.Position.Lat:F6},{m.Position.Lng:F6}");
        try
        {
            m.UpdateSize(s.w, s.h);
            m.UpdateRotation(s.bearing);
            if (posChanged) m.UpdateLocation(s.pos);
        }
        catch { /* 마커 변형 흡수 — 아래 영속·재렌더는 계속 */ }
        var cA = (m as GMapImageMarker)?.Center ?? m.Position;
        _log?.Info($"[DIAG-TFM] id={_id} 적용後 center={cA.Lat:F6},{cA.Lng:F6} pos={m.Position.Lat:F6},{m.Position.Lng:F6} (UpdateLocation호출={posChanged})");
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}

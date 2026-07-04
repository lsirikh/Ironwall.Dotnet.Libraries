using System.Threading;
using System.Threading.Tasks;
using GMap.NET;

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
    private readonly (PointLatLng pos, double w, double h, double bearing) _before, _after;

    public TransformCommand(IUndoApplyContext ctx, int markerId, bool isImage,
        (PointLatLng pos, double w, double h, double bearing) before,
        (PointLatLng pos, double w, double h, double bearing) after) : base(ctx)
    {
        _id = markerId; _isImage = isImage; _before = before; _after = after;
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
        try { m.UpdateSize(s.w, s.h); m.UpdateRotation(s.bearing); m.UpdateLocation(s.pos); }
        catch { /* 마커 변형 흡수 — 아래 영속·재렌더는 계속 */ }
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : Line/Area(폴리라인·폴리곤) 리사이즈(점 스케일) Undo 커맨드 — before/after=LinePoints+Position.
   Note         : LineArea_Symbol_Resize FR-04. TransformCommand는 W/H/pos/bearing만 담아 점을 복원 못 함(§5-C R-07) →
                  점 기하 전용 커맨드. `ILineEditableMarker.ApplyGeometry`로 복원(SyncModelPoints+UI마샬 4단계).
                  line은 항상 non-image → FindMarkerById(id,false) 타입인지(이미지↔심볼 Id 충돌 오적용 방지).
   Created On   : 2026-07-13 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class LineGeometryCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly (List<PointLatLng> pts, PointLatLng pos) _before, _after;

    public LineGeometryCommand(IUndoApplyContext ctx, int markerId,
        (List<PointLatLng> pts, PointLatLng pos) before,
        (List<PointLatLng> pts, PointLatLng pos) after) : base(ctx)
    {
        _id = markerId; _before = before; _after = after;
    }

    public override string Description => "라인/폴리곤 크기조절";

    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);

    private async Task Apply((List<PointLatLng> pts, PointLatLng pos) s, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, false);   // line=non-image, 타입인지
        if (m is not ILineEditableMarker line) return;
        try
        {
            // ApplyGeometry가 내부에서 UI 스레드 마샬 → 아래 await ConfigureAwait(false) 이전에 UI 반영 완료(크로스스레드 방지).
            line.ApplyGeometry(s.pts, s.pos);
        }
        catch { /* 마커 변형 흡수 — 아래 영속은 계속 */ }
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}

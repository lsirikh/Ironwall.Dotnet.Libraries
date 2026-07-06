using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 오버레이 이미지(GMapImageMarker) 어도너 이동/크기/회전 통합 취소·재실행.
                  before/after = 지리 ImageBounds(RectLatLng, 도) + 회전각.
   Note         : 이미지 크기의 픽셀 표현은 줌 의존(OnRender가 매 프레임 재계산)이라, TransformCommand의 픽셀 w/h를
                  저장하면 편집~undo 사이 줌이 바뀔 때 옛 줌 크기로 어긋난다(줌-간섭 크기오차). ImageBounds(도)는
                  줌 불변 SSOT이며 위치(Center)+크기(Left/Top/Right/Bottom)를 원자적으로 담으므로, 이동·크기 모두
                  줌과 무관하게 정확 복원한다. (심볼식 정렬 후속 — 지리 bounds 봉인)
   Created On   : 2026-07-06 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class ImageTransformCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly (RectLatLng bounds, double bearing) _before, _after;

    public ImageTransformCommand(IUndoApplyContext ctx, int imageId,
        (RectLatLng bounds, double bearing) before, (RectLatLng bounds, double bearing) after) : base(ctx)
    {
        _id = imageId; _before = before; _after = after;
    }

    public override string Description => "이미지 이동/크기/회전";

    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);

    private async Task Apply((RectLatLng bounds, double bearing) s, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, isImage: true);   // 타입인지 — 이미지만
        if (m is not GMapImageMarker img) return;
        try
        {
            img.UpdateBounds(s.bounds);      // 지리 bounds(도) = 위치+크기 원자 복원, 줌 불변
            img.UpdateRotation(s.bearing);
        }
        catch { /* 변형 흡수 — 영속·재렌더는 계속 */ }
        // ApplyMarkerUpdateAsync가 이미지 컨트롤 재계산(UpdateScreenPosition)+캔버스 재투영(ForceUpdateLocalPosition)
        //   +레이어명 동기까지 수행(현행 seam) → bounds 변경이 화면·DB에 정합 반영.
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 꾸미기 항목(나무 · 바닥 · 소품 · 담장) 넣기 · 고치기 · 다시 놓기 · 지우기 Undo 1단위 — map-3d 에 그 항목을 앞(before) · 뒤(after)
                  상태로 다시 저장한다(null = 지움). 직접 그린 건물과 같은 공용 바탕(AreaStateCommand).
   Note         : 설명은 Map3DDecoration.UndoPrefix 로 시작한다 — 꾸미기 중 Ctrl+Z · Ctrl+Y 가 이 기록만 되돌린다.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class DecorationCommand : AreaStateCommand<Map3DDecorationItem>
{
    public DecorationCommand(string description, string job, string uid, Map3DDecorationItem? before, Map3DDecorationItem? after,
                             Func<string, string, Map3DDecorationItem?, CancellationToken, Task> apply, Func<string, UbPoint, string>? resolveJob = null)
        : base(description, job, uid, before, after, apply, resolveJob) { }

    protected override IReadOnlyList<UbPoint> OutlineOf(Map3DDecorationItem state) => state.Points;
}

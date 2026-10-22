using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 넣은 모델(외부 3D 모델) 넣기 · 옮기기 · 이름 · 지우기 Undo 1단위 — map-3d 에 그 모델을 앞(before) · 뒤(after) 상태로 다시 저장한다
                  (null = 지움). 꾸미기와 같은 공용 바탕(AreaStateCommand) — 「다시 만들기」로 job 이 바뀌어도 같은 영역에.
   Note         : 모델 uid 는 엔진이 만든다 — 지운 모델을 되살리면(상태의 Data 를 다시 올림) uid 가 바뀌므로 적용 함수가 옛 uid → 새 uid 를 기억한다
                  (MapViewModel.Model3D ResolveModelUid). 설명은 Map3DModels.UndoPrefix(= 「꾸미기 · 모델」)로 시작한다.
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class ModelCommand : AreaStateCommand<Map3DModelState>
{
    public ModelCommand(string description, string job, string uid, Map3DModelState? before, Map3DModelState? after,
                        Func<string, string, Map3DModelState?, CancellationToken, Task> apply, Func<string, UbPoint, string>? resolveJob = null)
        : base(description, job, uid, before, after, apply, resolveJob) { }

    protected override IReadOnlyList<UbPoint> OutlineOf(Map3DModelState state) => new[] { new UbPoint(state.Item.Lon, state.Item.Lat) };
}

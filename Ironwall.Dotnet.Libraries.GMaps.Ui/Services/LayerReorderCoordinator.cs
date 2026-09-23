using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 오버레이 레이어 순서 바꾸기의 적용 · 기록(D-36). 끌기 · Alt+↑↓ · 우클릭 '위로/아래로' 가 전부 이 한 곳으로 온다.
/// </summary>
/// <remarks>
/// <para><b>순서</b>: ① 트리 노드를 제자리에서 옮긴다(Clear 없이 Move — 선택 · 초점 유지)
/// ② 새 ZOrder 를 모델에 매기고 섹션 <b>전 행</b>의 지도 렌더 순서를 맞춘다
/// ③ DB 에 <b>한 번</b> 기록한다(<see cref="IGMapDbService.BatchUpdateMapLayerZOrderAsync"/>, 한 트랜잭션)
/// ④ 성공하면 Undo 1건을 쌓는다(바뀐 행만, 이전 → 새 ZOrder).</para>
/// <para><b>실패 복구 = DB 재조회</b>(되돌리기 계산이 아니다): 기록이 실패하면 DB 는 트랜잭션이 되돌렸으므로
/// <c>reloadFromDb</c> 로 트리 · 렌더 순서를 DB 기준으로 다시 세운다. 그 뒤에 줄 서 있던 요청은 옛 화면을 기준으로
/// 계산된 것이라 버린다(세대 번호).</para>
/// <para>①②는 호출한 스레드(UI)에서 <b>동기로</b> 끝난다 — 커널이 드롭 직후 같은 행을 다시 고르는 예약(Background)보다
/// 앞서야 연달아 Alt+↑ 를 누를 수 있다. 기록은 한 줄로 세운다(<see cref="SemaphoreSlim"/>). UI 컴포넌트라
/// <c>ConfigureAwait(false)</c> 를 쓰지 않는다 — 실패 복구가 트리 · 마커를 만진다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class LayerReorderCoordinator
{
    public const string UndoDescription = "레이어 순서 변경";

    private readonly IGMapDbService _db;
    private readonly Action<IMapLayerModel> _syncRender;
    private readonly Func<Task> _reloadFromDb;
    private readonly IEditRecorder? _recorder;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _generation;

    /// <param name="db">MapLayers 저장소.</param>
    /// <param name="syncRender">한 레이어의 지도 렌더 순서를 모델 ZOrder 에 맞춘다(오버레이 맵 캔버스 · 이미지 마커).</param>
    /// <param name="reloadFromDb">실패 복구 — DB 에서 트리를 다시 세우고 렌더 순서를 DB 값에 맞춘다.</param>
    /// <param name="recorder">Undo 기록기(없으면 기록하지 않는다).</param>
    /// <param name="log">로그.</param>
    public LayerReorderCoordinator(IGMapDbService db, Action<IMapLayerModel> syncRender, Func<Task> reloadFromDb,
        IEditRecorder? recorder, ILogService? log)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _syncRender = syncRender ?? throw new ArgumentNullException(nameof(syncRender));
        _reloadFromDb = reloadFromDb ?? throw new ArgumentNullException(nameof(reloadFromDb));
        _recorder = recorder;
        _log = log;
    }

    /// <summary>
    /// <paramref name="section"/> 의 자식을 <paramref name="newOrder"/> 순서로 바꾸고 기록한다.
    /// </summary>
    /// <returns>DB 기록이 성공하면 true. 순서가 그대로면(쓸 것이 없음) · 실패하면 · 앞선 실패로 버려지면 false.</returns>
    public async Task<bool> ReorderAsync(LayerTreeNode section, IReadOnlyList<LayerTreeNode> newOrder, CancellationToken ct = default)
    {
        if (section == null || newOrder == null || newOrder.Count == 0) return false;
        if (newOrder.Any(n => n.Model == null)) return false;
        if (newOrder.SequenceEqual(section.Children)) return false;

        // ① 제자리 이동 — 동기
        LayerReorderRules.ApplyOrderInPlace(section.Children, newOrder);

        // ② ZOrder 매기기 + 섹션 전 행 렌더 동기화 — 동기.
        //    바뀐 행만 맞추면 안 된다: 오버레이 맵은 기동 때 캔버스 ZIndex 를 '활성화 순번'으로 잡아(CustomMapOverlayService)
        //    DB 값과 눈금이 다르다 — 일부만 DB 값으로 바꾸면 안 바뀐 행과 높낮이가 뒤섞인다.
        var models = newOrder.Select(n => n.Model!).ToList();
        var plan = LayerReorderRules.AssignZOrders(models);
        foreach (var p in plan) p.Layer.ZOrder = p.NewZOrder;
        foreach (var m in models) _syncRender(m);

        var generation = _generation;
        var writes = plan.Select(p => (p.Layer.Id, p.NewZOrder)).ToList();
        var changed = plan.Where(p => p.IsChanged).ToList();

        // ③ 한 번의 기록 — 한 줄로 세운다
        await _writeGate.WaitAsync(ct);
        try
        {
            if (generation != _generation)
            {
                _log?.Warning($"[레이어 순서] 앞선 기록 실패로 화면을 DB 에서 다시 세웠음 — 이 요청({section.Name})은 옛 화면 기준이라 버림");
                return false;
            }

            bool ok;
            try
            {
                ok = await _db.BatchUpdateMapLayerZOrderAsync(writes, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log?.Error($"[레이어 순서] DB 기록 실패({section.Name}, {writes.Count}건): {ex.Message}");
                ok = false;
            }

            if (ok)
            {
                // ④ Undo 1건 — 바뀐 행만. 되돌리기는 IUndoApplyContext.ApplyLayerFieldsAsync 가 행마다 적용한다.
                if (changed.Count > 0)
                    _recorder?.RecordLayerChange(UndoDescription,
                        changed.Select(p => new LayerFields(p.Layer.Id, null, null, p.OldZOrder)).ToList(),
                        changed.Select(p => new LayerFields(p.Layer.Id, null, null, p.NewZOrder)).ToList());
                _log?.Info($"[레이어 순서] {section.Name}: {string.Join(" → ", models.Select(m => $"{m.Name}(Z={m.ZOrder})"))}");
                return true;
            }

            // 실패 복구 — DB 재조회. 트랜잭션이 되돌렸으므로 DB 가 정본이다.
            _generation++;
            _log?.Warning($"[레이어 순서] 기록 실패 — DB 에서 트리와 렌더 순서를 다시 세움({section.Name})");
            await _reloadFromDb();
            return false;
        }
        finally
        {
            _writeGate.Release();
        }
    }
}

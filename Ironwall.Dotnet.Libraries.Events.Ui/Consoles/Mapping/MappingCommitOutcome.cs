using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 커밋 결과 4분류 — 생성 / 건너뜀 / 실패 / 없음(순수 함수)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// [적용] 한 번의 결과 누계. <b>"저장되었습니다" 로 뭉개지 않기 위한 타입</b>이다.
/// </summary>
/// <remarks>
/// 벌크는 <c>200 OK</c> 안에 성공·건너뜀·실패가 <b>함께</b> 온다. 이걸 성공 하나로 접으면
/// 사용자는 저장되지 않은 설정을 저장된 것으로 믿는다.
/// </remarks>
public sealed class MappingCommitOutcome
{
    /// <summary>새로 등록된 건수.</summary>
    public int Created { get; private set; }

    /// <summary>해제된 건수.</summary>
    public int Released { get; private set; }

    /// <summary>값·순서가 수정된 건수.</summary>
    public int Patched { get; private set; }

    /// <summary>이미 등록돼 있어 건너뛴 건수 — <b>실패가 아니다</b>.</summary>
    public int Skipped { get; private set; }

    /// <summary>실패 건수.</summary>
    public int Failed { get; private set; }

    /// <summary>사용자에게 보여 줄 실패 사유(한국어 라벨 + 짧은 원문). 중복 없이 모은다.</summary>
    public IReadOnlyList<string> FailureNotes => _failures;
    private readonly List<string> _failures = new();

    /// <summary>실패한 행 — 화면에 남겨 두고 배지를 붙인다.</summary>
    public IReadOnlyList<MappingBoardRow> FailedRows => _failedRows;
    private readonly List<MappingBoardRow> _failedRows = new();

    /// <summary>성공으로 정착해 baseline 을 갱신해도 되는 행.</summary>
    public IReadOnlyList<(MappingBoardRow Row, int ConfigId)> SettledRows => _settled;
    private readonly List<(MappingBoardRow Row, int ConfigId)> _settled = new();

    /// <summary>해제가 확정된 행 — 목록에서 뺀다.</summary>
    public IReadOnlyList<MappingBoardRow> ReleasedRows => _releasedRows;
    private readonly List<MappingBoardRow> _releasedRows = new();

    /// <summary>하나라도 실패했는가.</summary>
    public bool HasFailure => Failed > 0;

    /// <summary>한 건도 못 보냈는가(네트워크·권한 등으로 통째 중단).</summary>
    public bool IsAborted { get; private set; }

    /// <summary>통째 중단 사유(한국어).</summary>
    public string? AbortReason { get; private set; }

    /// <summary>통째 중단으로 표시한다 — Draft 는 <b>절대 버리지 않는다</b>.</summary>
    public void Abort(string reason)
    {
        IsAborted = true;
        AbortReason = reason;
    }

    /// <summary>
    /// 벌크 등록 응답 한 건을 접수한다.
    /// </summary>
    /// <param name="rows">이 요청에 실어 보낸 행들(요청 순서 그대로).</param>
    /// <param name="result">서버 응답 <c>data</c>.</param>
    public void AcceptCreate(IReadOnlyList<MappingBoardRow> rows, MappingBulkCreateResultDto result)
    {
        var createdIds = result.CreatedIds ?? new List<int>();
        var failedIndexes = new HashSet<int>((result.FailedItems ?? new List<MappingBulkFailedItemDto>()).Select(f => f.Index));

        Created += createdIds.Count;
        Skipped += result.SkippedConfigIds?.Count ?? 0;

        // created_ids 는 "요청 순서 보존" 이지만 실패·건너뜀이 섞이면 행과 1:1 이 아니다.
        // 그래서 id 를 행에 억지로 붙이지 않고, 실패하지 않은 행만 "정착" 으로 표시한 뒤 재조회로 확정한다.
        for (var i = 0; i < rows.Count; i++)
        {
            if (failedIndexes.Contains(i))
            {
                Failed++;
                _failedRows.Add(rows[i]);
                continue;
            }
            _settled.Add((rows[i], 0));
        }

        foreach (var failure in result.FailedItems ?? new List<MappingBulkFailedItemDto>())
            AddFailure("등록", failure.Error);

        var notFound = result.NotFoundConfigIds?.Count ?? 0;
        if (notFound > 0)
        {
            Failed += notFound;
            AddFailure("등록", $"서버에 없는 장비 {notFound}건");
        }
    }

    /// <summary>벌크 해제 응답 한 건을 접수한다.</summary>
    /// <param name="rows">이 요청에 실어 보낸 행들.</param>
    /// <param name="result">서버 응답 <c>data</c>.</param>
    public void AcceptRelease(IReadOnlyList<MappingBoardRow> rows, MappingBulkUnassignResultDto result)
    {
        var removed = new HashSet<int>(result.RemovedConfigIds ?? new List<int>());
        var notFound = new HashSet<int>(result.NotFoundConfigIds ?? new List<int>());
        var skipped = new HashSet<int>(result.SkippedConfigIds ?? new List<int>());

        foreach (var row in rows)
        {
            // 행이 이미 없는 것(not_found)도 "이 매핑에 더는 없다" 는 목적을 이뤘으므로 해제로 친다.
            if (removed.Contains(row.ConfigId) || notFound.Contains(row.ConfigId))
            {
                Released++;
                _releasedRows.Add(row);
            }
            else if (skipped.Contains(row.ConfigId))
            {
                Failed++;
                _failedRows.Add(row);
                AddFailure("해제", "다른 이벤트 맵핑에 속한 배선");
            }
        }
    }

    /// <summary>한 행 PATCH 결과를 접수한다.</summary>
    public void AcceptPatch(MappingBoardRow row, bool ok, string? reason)
    {
        if (ok)
        {
            Patched++;
            _settled.Add((row, row.ConfigId));
            return;
        }
        Failed++;
        _failedRows.Add(row);
        AddFailure("수정", reason);
    }

    private void AddFailure(string stage, string? reason)
    {
        // 서버 원문은 영문이라 그대로 내지 않는다 — 한국어 라벨을 앞에 붙이고 원문은 괄호로 짧게 남긴다.
        var text = string.IsNullOrWhiteSpace(reason)
            ? $"{stage} 실패"
            : $"{stage} 실패 — {Shorten(reason!)}";
        if (!_failures.Contains(text)) _failures.Add(text);
    }

    private static string Shorten(string raw)
    {
        var trimmed = raw.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return trimmed.Length <= 120 ? trimmed : trimmed[..120] + "…";
    }

    /// <summary>
    /// 상태줄에 쓸 한 줄. <b>배열 길이로 클라가 직접 센 값</b>이다 —
    /// 서버 <c>message</c> 의 셈법이 카메라와 스피커·경광등에서 다르기 때문이다.
    /// </summary>
    public string ToMessage()
    {
        if (IsAborted) return AbortReason ?? "적용하지 못했습니다. 변경은 그대로 남아 있습니다.";

        var parts = new List<string>();
        if (Created > 0) parts.Add($"등록 {Created}건");
        if (Released > 0) parts.Add($"해제 {Released}건");
        if (Patched > 0) parts.Add($"수정 {Patched}건");
        if (Skipped > 0) parts.Add($"중복 {Skipped}건 제외");

        var body = parts.Count == 0 ? "바뀐 것이 없습니다" : string.Join(" · ", parts);
        if (Failed == 0) return $"적용 완료 — {body}";

        var sb = new StringBuilder();
        sb.Append($"부분 적용 — {body} · 실패 {Failed}건은 화면에 남았습니다");
        return sb.ToString();
    }
}

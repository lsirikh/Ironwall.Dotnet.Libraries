using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

/// <summary>Draft 한 건을 적용한 결과 — 부분 실패 4분류.</summary>
public enum DraftOutcome
{
    /// <summary>서버에 반영됐다.</summary>
    Applied,
    /// <summary>실패 — 트레이에 남는다.</summary>
    Failed,
    /// <summary>이미 그 상태라 보내지 않았다.</summary>
    Skipped,
    /// <summary>대상이 사라졌다(다른 곳에서 삭제).</summary>
    Missing,
}

/// <summary>
/// 드롭 한 번이 만든 미적용 변경. 같은 <see cref="TargetKey"/> + <see cref="CallKind"/> 는 하나로 합쳐지고 나중 것이 이긴다.
/// </summary>
public sealed class DraftEntry
{
    public DraftEntry(string targetKey, string callKind, string description, Func<CancellationToken, Task<DraftOutcome>> apply)
    {
        TargetKey = targetKey ?? throw new ArgumentNullException(nameof(targetKey));
        CallKind = callKind ?? throw new ArgumentNullException(nameof(callKind));
        Description = description ?? string.Empty;
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    /// <summary>무엇에 대한 변경인가 — 예: <c>camera:12</c>, <c>group:3</c>.</summary>
    public string TargetKey { get; }
    /// <summary>어떤 호출인가 — 예: <c>PATCH unit_id</c>. 화면에도 그대로 보인다.</summary>
    public string CallKind { get; }
    /// <summary>사람이 읽는 한 줄 — "정문 카메라 → 1소대".</summary>
    public string Description { get; }
    /// <summary>실제 호출. 커널은 서버를 모른다 — 창이 실어 준다.</summary>
    public Func<CancellationToken, Task<DraftOutcome>> Apply { get; }

    /// <summary>마지막 적용이 실패했을 때의 사유 한 줄.</summary>
    public string? FailureReason { get; internal set; }
}

/// <summary>적용 한 번의 요약.</summary>
public sealed record DraftApplySummary(int Applied, int Failed, int Skipped, int Missing, bool WasCancelled)
{
    public int Total => Applied + Failed + Skipped + Missing;

    public string ToMessage()
    {
        var parts = new List<string> { $"적용 {Applied}" };
        if (Failed > 0) parts.Add($"실패 {Failed}");
        if (Skipped > 0) parts.Add($"건너뜀 {Skipped}");
        if (Missing > 0) parts.Add($"없음 {Missing}");
        return (WasCancelled ? "중단 — " : "적용 완료 — ") + string.Join(" · ", parts);
    }
}

/// <summary>
/// Draft 트레이 — 드래그가 <b>N회 호출</b>로 번질 때 곧바로 보내지 않고 여기에 쌓았다가 [적용] 때 모아 보낸다.
/// 호출 1회로 끝나는 조작은 트레이를 거치지 않고 즉시 보낸다.
/// (설계 정본 all-windows-drag-wireframe.html L432 · L769-810)
/// </summary>
/// <remarks>호출 스레드: UI. 적용은 순차 실행 — 서버 쓰기는 직렬이 안전하고 진행률이 정직하다.</remarks>
public sealed class DraftTrayViewModel : PropertyChangedBase
{
    // 화면의 목록이 그대로 따라오도록 관찰 가능한 컬렉션으로 둔다.
    private readonly BindableCollection<DraftEntry> _entries = new();
    private bool _isApplying;
    private int _progressDone;
    private int _progressTotal;
    private string _message = string.Empty;

    public IReadOnlyList<DraftEntry> Entries => _entries;
    public int Count => _entries.Count;
    public bool HasEntries => _entries.Count > 0;
    public bool CanApply => HasEntries && !_isApplying;
    public bool CanRevert => HasEntries && !_isApplying;

    public bool IsApplying
    {
        get => _isApplying;
        private set { _isApplying = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanApply)); NotifyOfPropertyChange(nameof(CanRevert)); }
    }

    public int ProgressDone { get => _progressDone; private set { _progressDone = value; NotifyOfPropertyChange(); } }
    public int ProgressTotal { get => _progressTotal; private set { _progressTotal = value; NotifyOfPropertyChange(); } }
    public string Message { get => _message; private set { _message = value; NotifyOfPropertyChange(); } }

    /// <summary>쌓는다. 같은 대상 · 같은 호출이면 <b>나중 것으로 바꾼다</b>(자리는 그대로).</summary>
    public void Add(DraftEntry entry)
    {
        if (_isApplying) throw new InvalidOperationException("적용 중에는 Draft 를 더할 수 없습니다.");

        var index = -1;
        for (var i = 0; i < _entries.Count; i++)
            if (_entries[i].TargetKey == entry.TargetKey && _entries[i].CallKind == entry.CallKind) { index = i; break; }
        if (index >= 0) _entries[index] = entry;
        else _entries.Add(entry);

        Message = $"Draft {_entries.Count}건 — [적용] 때 모아 보냅니다";
        RaiseEntriesChanged();
    }

    public void AddRange(IEnumerable<DraftEntry> entries)
    {
        foreach (var e in entries) Add(e);
    }

    /// <summary>되돌리기 — 서버 호출 0.</summary>
    public void Revert()
    {
        if (_isApplying || _entries.Count == 0) return;
        _entries.Clear();
        Message = "Draft 를 버렸습니다 — 서버 호출 0";
        RaiseEntriesChanged();
    }

    /// <summary>
    /// 순서대로 적용한다. 성공 · 건너뜀 · 없음은 트레이에서 빠지고, <b>실패한 행은 남는다</b>(사유와 함께) — 다시 [적용] 하면 그것만 재시도한다.
    /// 취소하면 아직 안 보낸 행은 그대로 남는다.
    /// </summary>
    public async Task<DraftApplySummary> ApplyAsync(CancellationToken token = default)
    {
        if (_isApplying) throw new InvalidOperationException("이미 적용 중입니다.");
        if (_entries.Count == 0) return new DraftApplySummary(0, 0, 0, 0, false);

        var queue = _entries.ToList();
        int applied = 0, failed = 0, skipped = 0, missing = 0;
        var cancelled = false;

        IsApplying = true;
        ProgressTotal = queue.Count;
        ProgressDone = 0;
        try
        {
            foreach (var entry in queue)
            {
                if (token.IsCancellationRequested) { cancelled = true; break; }

                DraftOutcome outcome;
                try
                {
                    entry.FailureReason = null;
                    outcome = await entry.Apply(token).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    outcome = DraftOutcome.Failed;
                    entry.FailureReason = ex.Message;
                }

                switch (outcome)
                {
                    case DraftOutcome.Applied: applied++; _entries.Remove(entry); break;
                    case DraftOutcome.Skipped: skipped++; _entries.Remove(entry); break;
                    case DraftOutcome.Missing: missing++; _entries.Remove(entry); break;
                    default:
                        failed++;
                        entry.FailureReason ??= "실패";
                        break;
                }
                ProgressDone++;
            }
        }
        finally
        {
            IsApplying = false;
        }

        var summary = new DraftApplySummary(applied, failed, skipped, missing, cancelled);
        Message = summary.ToMessage();
        RaiseEntriesChanged();
        return summary;
    }

    private void RaiseEntriesChanged()
    {
        NotifyOfPropertyChange(nameof(Entries));
        NotifyOfPropertyChange(nameof(Count));
        NotifyOfPropertyChange(nameof(HasEntries));
        NotifyOfPropertyChange(nameof(CanApply));
        NotifyOfPropertyChange(nameof(CanRevert));
    }
}

using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;

/// <summary>조치 한 건을 실제로 보내는 일. 창이 실어 준다 — 트레이는 서버를 모른다.</summary>
/// <param name="candidate">보낼 원본.</param>
/// <param name="content">조치 문구(문구 목록에서 고른 것 또는 '기타' 메모).</param>
public delegate Task<DraftOutcome> ActionReportSender(ActionTrayCandidate candidate, string content, CancellationToken token);

/// <summary>
/// 조치 트레이 — 고른 이벤트를 <b>Draft</b> 로 쌓았다가 <b>[적용]</b> 때 한 건씩 보낸다.
/// </summary>
/// <remarks>
/// <para>정본 all-windows-drag-wireframe.html L423: 조치 생성은 <b>벌크가 없어</b> N건이면 N회다 → 드롭은 서버를 부르지 않는다.</para>
/// <para>진행률 · 부분 실패 4분류 · 실패 줄만 재시도는 커널 <see cref="DraftTrayViewModel"/> 계약 그대로다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class ActionTrayViewModel : PropertyChangedBase
{
    /// <summary>
    /// 기본 문구 — 서버의 조치보고 문구 관리 목록을 못 읽을 때만 쓴다(<see cref="ActionReportPhraseSource.Fallback"/>).
    /// 실제로 보이는 목록은 <see cref="PhraseOptions"/> 다(<see cref="ApplyPhrases"/> 가 서버 목록으로 갈아 끼운다).
    /// </summary>
    public static IReadOnlyList<string> Phrases => ActionReportPhraseSource.Fallback;

    /// <summary>'기타' 를 고르면 메모가 문구가 된다.</summary>
    public const string EtcPhrase = ActionReportPhraseSource.EtcPhrase;

    private readonly ActionReportSender _send;
    private readonly Dictionary<string, ActionTrayCandidate> _queued = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private string? _inFlightKey;
    // 고르지 않은 문구로 [적용] 이 살아 있으면 아무도 고르지 않은 사유로 기록이 남는다 — 빈 값에서 시작한다(R14).
    private string _phrase = string.Empty;
    private string _memo = string.Empty;
    private string _statusLine = string.Empty;

    public ActionTrayViewModel(ActionReportSender send)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        Draft = new DraftTrayViewModel();
        Draft.PropertyChanged += (_, _) => RaiseAll();
    }

    /// <summary>커널 트레이 — 목록 · 진행률 · 적용 · 되돌리기를 전부 쥐고 있다.</summary>
    public DraftTrayViewModel Draft { get; }

    /// <summary>고른 문구. <see cref="EtcPhrase"/> 면 <see cref="Memo"/> 가 실린다.</summary>
    public string Phrase
    {
        get => _phrase;
        set { _phrase = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsEtc)); NotifyOfPropertyChange(nameof(CanApply)); NotifyOfPropertyChange(nameof(ApplyBlockedReason)); }
    }

    public bool IsEtc => string.Equals(_phrase, EtcPhrase, StringComparison.Ordinal);

    public string Memo
    {
        get => _memo;
        set { _memo = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanApply)); NotifyOfPropertyChange(nameof(ApplyBlockedReason)); }
    }

    /// <summary>실제로 보낼 문구 — '기타' 면 메모, 아니면 고른 문구. 고르지 않았으면 빈 글자.</summary>
    public string EffectiveContent => IsEtc ? Memo.Trim() : _phrase.Trim();

    /// <summary>문구를 고르지 않았다 — [적용] 이 꺼져 있는 까닭을 화면이 말해 준다.</summary>
    /// <remarks>서버 <c>content</c> 는 1~500자다 — 넘치면 줄마다 422 가 났다(실서버 왕복 E7b). 보내기 전에 막는다.</remarks>
    public string ApplyBlockedReason => EffectiveContent.Length == 0
        ? (IsEtc ? "기타 내용을 적어야 보낼 수 있습니다" : "문구를 먼저 고르세요")
        : Helpers.ActionReportRules.ValidateContent(EffectiveContent) ?? string.Empty;

    /// <summary>고를 수 있는 문구 — 조치보고 문구 관리 목록(순서 그대로) + 맨 끝 '기타'.</summary>
    public IReadOnlyList<string> PhraseOptions
    {
        get => _phraseOptions;
        private set { _phraseOptions = value; NotifyOfPropertyChange(); }
    }
    private IReadOnlyList<string> _phraseOptions = WithEtc(Phrases);

    /// <summary>문구가 조치보고 문구 관리 목록에서 왔는가(아니면 기본 문구).</summary>
    public bool PhrasesFromServer { get; private set; }

    /// <summary>
    /// 문구 목록을 갈아 끼운다(서버의 조치보고 문구 관리 목록 또는 기본 문구).
    /// 고른 문구가 새 목록에도 있으면 그대로 두고, 없어졌으면 비운다 — 사라진 문구로 조치보고가 나가지 않게.
    /// '기타' 와 그 메모는 늘 살아 있다.
    /// </summary>
    public void ApplyPhrases(ActionReportPhraseSet set)
    {
        if (set is null) return;
        var next = WithEtc(set.Phrases is { Count: > 0 } p ? p : Phrases);
        PhrasesFromServer = set.FromServer && set.Phrases is { Count: > 0 };
        NotifyOfPropertyChange(nameof(PhrasesFromServer));
        if (next.SequenceEqual(_phraseOptions, StringComparer.Ordinal)) return;

        PhraseOptions = next;
        if (_phrase.Length > 0 && !next.Contains(_phrase, StringComparer.Ordinal)) Phrase = string.Empty;
    }

    private static IReadOnlyList<string> WithEtc(IEnumerable<string> phrases)
        => phrases.Where(p => !string.Equals(p, EtcPhrase, StringComparison.Ordinal)).Concat(new[] { EtcPhrase }).ToList();

    public int Count => Draft.Count;
    public bool HasEntries => Draft.HasEntries;
    public bool IsApplying => Draft.IsApplying;
    public int ProgressDone => Draft.ProgressDone;
    public int ProgressTotal => Draft.ProgressTotal;

    /// <summary>"3 / 7" — 진행 중에만 뜻이 있다.</summary>
    public string ProgressText => Draft.IsApplying ? $"{Draft.ProgressDone} / {Draft.ProgressTotal}" : string.Empty;

    /// <summary>문구가 비면 보내지 않는다 — 내용 없는 조치보고는 기록이 아니다.</summary>
    public bool CanApply => Draft.CanApply && EffectiveContent.Length > 0 && Helpers.ActionReportRules.ValidateContent(EffectiveContent) is null;
    public bool CanRevert => Draft.CanRevert;
    public bool CanCancel => Draft.IsApplying;

    /// <summary>
    /// 화면이 그리는 줄들. <b>불러올 때마다 새 목록</b>을 만든다 — <c>DraftEntry</c> 는 변경 통지를 하지 않아
    /// 같은 인스턴스를 다시 주면 적용 뒤의 실패 사유가 화면에 영영 안 뜬다.
    /// </summary>
    public IReadOnlyList<DraftEntry> Entries => Draft.Entries.ToList();

    /// <summary>실패해 트레이에 남은 줄들 — 사유와 함께 보인다.</summary>
    public IReadOnlyList<DraftEntry> FailedEntries
        => Draft.Entries.Where(e => !string.IsNullOrEmpty(e.FailureReason)).ToList();

    public bool HasFailures => FailedEntries.Count > 0;

    /// <summary>담기 · 적용 뒤의 한 줄.</summary>
    public string StatusLine
    {
        get => _statusLine;
        private set { _statusLine = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>계획대로 담는다. 서버 호출 0. 같은 대상은 한 줄로 합쳐진다(커널 <c>Add</c>).</summary>
    public string Enqueue(TrayPlan plan)
    {
        if (!plan.CanQueue)
        {
            StatusLine = ActionTrayDrop.ResultLine(plan);
            return StatusLine;
        }
        if (Draft.IsApplying)
        {
            StatusLine = "적용 중에는 더 담을 수 없습니다.";
            return StatusLine;
        }

        foreach (var candidate in plan.Accepted)
        {
            _queued[candidate.TargetKey] = candidate;
            var snapshot = candidate;
            Draft.Add(new DraftEntry(
                snapshot.TargetKey,
                "POST /events/actions",
                snapshot.Label,
                token =>
                {
                    // 중단이 어느 줄에서 걸렸는지를 알아야 "결과 미확인" 을 표시할 수 있다.
                    _inFlightKey = snapshot.TargetKey;
                    return _send(snapshot, EffectiveContent, token);
                }));
        }

        StatusLine = ActionTrayDrop.ResultLine(plan);
        RaiseAll();
        return StatusLine;
    }

    /// <summary>순차로 보낸다 — 한 건당 호출 1회. 실패한 줄은 남아 다음 [적용] 때만 다시 간다.</summary>
    public async Task<DraftApplySummary> ApplyAsync()
    {
        if (!CanApply) return new DraftApplySummary(0, 0, 0, 0, false);

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try
        {
            _inFlightKey = null;
            var summary = await Draft.ApplyAsync(_cts.Token).ConfigureAwait(true);
            StatusLine = summary.ToMessage();
            if (summary.Failed > 0) StatusLine += " — 실패한 줄은 트레이에 남습니다. [조치 적용] 을 다시 누르면 그것만 보냅니다.";
            if (summary.WasCancelled && UnverifiedKey is not null)
                StatusLine += " — ⚠ 중단 순간 보내는 중이던 1건은 결과 미확인입니다. 다시 [조치 적용] 하면 중복될 수 있습니다.";
            RaiseAll();
            return summary;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>적용을 멈춘다 — 아직 안 보낸 줄은 그대로 남는다(보낸 것은 되돌리지 않는다).</summary>
    public void Cancel()
    {
        if (_cts is not { IsCancellationRequested: false }) return;
        // 보내는 중이던 줄은 서버에 이미 닿았을 수 있다 — 미전송으로 남기지 않는다.
        UnverifiedKey = _inFlightKey;
        _cts.Cancel();
        NotifyOfPropertyChange(nameof(UnverifiedKey));
    }

    /// <summary>전부 버린다 — 서버 호출 0.</summary>
    public void Revert()
    {
        if (Draft.IsApplying) return;
        Draft.Revert();
        _queued.Clear();
        UnverifiedKey = null;
        StatusLine = "조치 트레이를 비웠습니다.";
        RaiseAll();
    }

    /// <summary>
    /// 중단 순간 <b>보내는 중이던</b> 줄의 대상 키. 서버에 이미 만들어졌을 수 있어
    /// 다시 [적용] 하면 중복된다 — 화면에 "결과 미확인" 으로 낸다(N-07 R7).
    /// </summary>
    public string? UnverifiedKey { get; private set; }

    /// <summary>이 줄이 "결과 미확인" 인가.</summary>
    public bool IsUnverified(DraftEntry entry) => entry.TargetKey == UnverifiedKey;

    /// <summary>트레이에 이 대상이 있는가(같은 행을 두 번 담아도 줄이 늘지 않음을 화면에서 보이기 위해).</summary>
    public bool Contains(string targetKey) => Draft.Entries.Any(e => e.TargetKey == targetKey);

    private void RaiseAll()
    {
        NotifyOfPropertyChange(nameof(Count));
        NotifyOfPropertyChange(nameof(HasEntries));
        NotifyOfPropertyChange(nameof(IsApplying));
        NotifyOfPropertyChange(nameof(ProgressDone));
        NotifyOfPropertyChange(nameof(ProgressTotal));
        NotifyOfPropertyChange(nameof(ProgressText));
        NotifyOfPropertyChange(nameof(CanApply));
        NotifyOfPropertyChange(nameof(ApplyBlockedReason));
        NotifyOfPropertyChange(nameof(CanRevert));
        NotifyOfPropertyChange(nameof(CanCancel));
        NotifyOfPropertyChange(nameof(Entries));
        NotifyOfPropertyChange(nameof(FailedEntries));
        NotifyOfPropertyChange(nameof(HasFailures));
    }
}

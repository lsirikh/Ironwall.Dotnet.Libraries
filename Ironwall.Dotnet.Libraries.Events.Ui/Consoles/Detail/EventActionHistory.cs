using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;

/// <summary>조치 내역 한 줄.</summary>
/// <param name="Index">1부터.</param>
/// <param name="When">시각.</param>
/// <param name="User">작성자.</param>
/// <param name="Content">내용.</param>
public sealed record EventActionLine(int Index, string When, string User, string Content);

/// <summary>조치 내역 칸이 지금 어떤 꼴인가.</summary>
public enum ActionHistoryState
{
    /// <summary>부르지 않았다(조치 여부가 꺼진 이벤트 — 정본 E-D4: 그러면 호출하지 않는다).</summary>
    NotRequested,
    Loading,
    Loaded,
    /// <summary>서버에 닿지 못했다 — 0건과 구별해서 보인다.</summary>
    Failed,
}

/// <summary>
/// 원본 한 건의 <b>조치 내역</b> — 이미 있는 원본별 조회 API 를 <b>열 때 한 번</b> 부른다
/// (정본 결정 E-D4, <c>window-layout-system-storyboard.html</c> L1119 · L1102).
/// </summary>
/// <remarks>
/// <para>이 API 는 <c>EventApiService</c> 에 있었지만 <b>어느 화면도 쓰지 않았다</b>(메모리 <c>event_action_1n_edit_boundary</c>).</para>
/// <para><b>낡은 응답 차단</b>: A 를 골랐다가 곧바로 B 를 고르면 A 의 응답이 늦게 와 B 밑에 붙을 수 있다 —
/// 요청마다 토큰을 올려 마지막 요청의 답만 화면에 들인다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class EventActionHistoryViewModel : PropertyChangedBase
{
    private readonly Func<IEventApiService?> _api;
    private readonly ILogService? _log;
    private int _token;
    private CancellationTokenSource? _cts;
    private ActionHistoryState _state = ActionHistoryState.NotRequested;
    private string _failureReason = string.Empty;

    public EventActionHistoryViewModel(Func<IEventApiService?> api, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _log = log;
        Lines = new ObservableCollection<EventActionLine>();
    }

    public ObservableCollection<EventActionLine> Lines { get; }

    public ActionHistoryState State
    {
        get => _state;
        private set { _state = value; NotifyOfPropertyChange(); RaiseDerived(); }
    }

    public string FailureReason
    {
        get => _failureReason;
        private set { _failureReason = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public int Count => Lines.Count;
    public bool IsLoading => State == ActionHistoryState.Loading;
    public bool IsFailed => State == ActionHistoryState.Failed;
    public bool IsEmpty => State == ActionHistoryState.Loaded && Lines.Count == 0;
    public bool HasLines => Lines.Count > 0;

    /// <summary>절 머리에 찍는 글자.</summary>
    public string Title => State switch
    {
        ActionHistoryState.NotRequested => "조치 내역 · 없음",
        ActionHistoryState.Loading => "조치 내역 · 불러오는 중",
        ActionHistoryState.Failed => "조치 내역 · 불러오지 못함",
        _ => $"조치 내역 · {Lines.Count}건",
    };

    /// <summary>선택이 비었을 때 — 부르지도 않고 비운다.</summary>
    public void Clear()
    {
        _token++;
        Cancel();
        Lines.Clear();
        FailureReason = string.Empty;
        _last = null;
        State = ActionHistoryState.NotRequested;
    }

    /// <summary>
    /// 원본 하나의 조치 내역을 <b>한 번</b> 불러온다.
    /// <paramref name="hasActions"/> 가 거짓이면 아예 부르지 않는다(정본 E-D4).
    /// </summary>
    public async Task LoadAsync(EventDetailKind kind, int originId, bool hasActions)
    {
        var mine = ++_token;
        Cancel();
        Lines.Clear();
        FailureReason = string.Empty;
        _last = (kind, originId, hasActions);

        if (originId <= 0 || !hasActions || kind is not (EventDetailKind.Detection or EventDetailKind.Malfunction))
        {
            State = ActionHistoryState.NotRequested;
            return;
        }

        var api = _api();
        if (api is null)
        {
            State = ActionHistoryState.NotRequested;
            return;
        }

        State = ActionHistoryState.Loading;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var response = kind == EventDetailKind.Detection
                ? await api.GetDetectionActionsAsync(originId, token).ConfigureAwait(true)
                : await api.GetMalfunctionActionsAsync(originId, token).ConfigureAwait(true);

            if (mine != _token) return;      // 그 사이 다른 행을 골랐다 — 낡은 응답은 버린다

            if (response is null || !response.Success)
            {
                // 서버 원문은 로그로만 — 화면에는 무엇이 안 됐고 어떻게 하면 되는지만 적는다.
                _log?.Warning($"[EventConsole] 조치 내역 조회 거절(origin={originId}): {response?.Message}");
                FailureReason = FailureText;
                State = ActionHistoryState.Failed;
                return;
            }

            Fill(response.Data);
            State = ActionHistoryState.Loaded;
        }
        catch (OperationCanceledException)
        {
            // 다른 행으로 옮겨 간 것 — 화면을 건드리지 않는다.
        }
        catch (Exception ex)
        {
            if (mine != _token) return;
            _log?.Error($"[EventConsole] 조치 내역 조회 실패(origin={originId}): {ex.Message}");
            FailureReason = FailureText;
            State = ActionHistoryState.Failed;
        }
    }

    /// <summary>불러오지 못했을 때의 안내 — 서버 · 예외 원문은 싣지 않는다.</summary>
    public const string FailureText = "조치 내역을 불러오지 못했습니다. [다시 불러오기]를 누르세요.";

    private (EventDetailKind Kind, int OriginId, bool HasActions)? _last;

    /// <summary>마지막으로 부른 원본을 다시 부른다(실패 뒤 [다시 불러오기] · 조치 적용 뒤 재조회).</summary>
    public Task ReloadAsync()
        => _last is { } last ? LoadAsync(last.Kind, last.OriginId, last.HasActions) : Task.CompletedTask;

    private void Fill(List<ActionEventDto>? data)
    {
        var rows = (data ?? new List<ActionEventDto>())
            .OrderBy(a => a.CreatedAt ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            var a = rows[i];
            Lines.Add(new EventActionLine(
                i + 1,
                Shorten(a.CreatedAt),
                string.IsNullOrWhiteSpace(a.User) ? "—" : a.User,
                string.IsNullOrWhiteSpace(a.Content) ? "—" : a.Content));
        }
        RaiseDerived();
    }

    private static string Shorten(string? iso)
        => DateTime.TryParse(iso, out var when) ? when.ToString("yyyy-MM-dd HH:mm") : (iso ?? "—");

    private void Cancel()
    {
        if (_cts is { IsCancellationRequested: false }) _cts.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void RaiseDerived()
    {
        NotifyOfPropertyChange(nameof(Count));
        NotifyOfPropertyChange(nameof(IsLoading));
        NotifyOfPropertyChange(nameof(IsFailed));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(HasLines));
        NotifyOfPropertyChange(nameof(Title));
    }
}

using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;

/// <summary>조치보고 문구 목록 한 벌 — 어디서 왔는지와 함께.</summary>
/// <param name="Phrases">보일 순서 그대로의 문구('기타' 는 넣지 않는다 — 화면이 끝에 붙인다).</param>
/// <param name="FromServer">서버의 조치보고 문구 관리 목록에서 왔는가. false 면 기본 문구다.</param>
public sealed record ActionReportPhraseSet(IReadOnlyList<string> Phrases, bool FromServer);

/// <summary>조치보고 문구를 어디서 가져오는가 — 조치 트레이와 조치보고 창이 같은 것을 쓴다.</summary>
public interface IActionReportPhraseSource
{
    /// <summary>마지막으로 받은 목록(아직 한 번도 못 받았으면 기본 문구). 창을 여는 순간 바로 채울 때 쓴다.</summary>
    ActionReportPhraseSet LastKnown { get; }

    /// <summary>서버의 조치보고 문구 관리 목록을 읽는다. 못 읽거나 비어 있으면 기본 문구를 준다(예외를 던지지 않는다).</summary>
    Task<ActionReportPhraseSet> LoadAsync(CancellationToken token = default);
}

/// <summary>
/// 조치보고 문구 — <b>조치보고 문구 콘솔이 관리하는 서버 목록</b>(<c>/api/events/action-report-templates</c>)을
/// <c>display_order</c> 순서로 쓴다. 서버가 그 기능을 모르거나(운영 6.3.2 = 404) 닿지 않거나 목록이 비었으면
/// 예전부터 쓰던 기본 문구 다섯 개로 간다 — 문구가 하나도 없으면 조치보고를 할 수 없기 때문이다.
/// </summary>
/// <remarks>
/// 서버 원문 · 예외 글은 화면에 싣지 않는다 — 로그로만 남긴다.
/// 호출 스레드: 아무 곳(결과만 돌려준다 — 화면 반영은 호출부가 UI 스레드에서 한다).
/// </remarks>
public sealed class ActionReportPhraseSource : IActionReportPhraseSource
{
    /// <summary>서버 목록을 쓸 수 없을 때의 기본 문구(예전 조치보고 창과 같은 다섯 개).</summary>
    public static readonly IReadOnlyList<string> Fallback = new[]
    {
        "야생동물출현",
        "강풍/폭우",
        "울타리 점검/작업",
        "침입발생 특경출동조치",
        "오경보",
    };

    /// <summary>'기타' — 메모가 문구가 된다. 서버 목록에 같은 글자가 있어도 한 번만 보인다.</summary>
    public const string EtcPhrase = "기타";

    private readonly Func<IActionReportTemplateApiService?> _api;
    private readonly ILogService? _log;
    private ActionReportPhraseSet _lastKnown = new(Fallback, false);

    /// <param name="api">문구 API — 늦게 푼다. 없으면(미등록 · 시험) null 을 돌려주면 되고, 그러면 기본 문구를 쓴다.</param>
    public ActionReportPhraseSource(Func<IActionReportTemplateApiService?> api, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _log = log;
    }

    public ActionReportPhraseSet LastKnown => Volatile.Read(ref _lastKnown);

    public async Task<ActionReportPhraseSet> LoadAsync(CancellationToken token = default)
    {
        IActionReportTemplateApiService? api;
        try { api = _api(); }
        catch (Exception ex)
        {
            _log?.Warning($"[ActionReportPhrase] 문구 API 를 풀지 못했습니다 — 기본 문구를 씁니다: {ex.Message}");
            api = null;
        }
        if (api is null) return Remember(new ActionReportPhraseSet(Fallback, false));

        try
        {
            var res = await api.GetTemplatesAsync(token).ConfigureAwait(false);
            if (!res.Success || res.Data is null)
            {
                _log?.Warning($"[ActionReportPhrase] 문구 목록을 받지 못했습니다 — 기본 문구를 씁니다: {res.Error?.Message ?? res.Message}");
                return Remember(new ActionReportPhraseSet(Fallback, false));
            }

            var ordered = Order(res.Data);
            if (ordered.Count == 0)
            {
                _log?.Info("[ActionReportPhrase] 서버에 등록된 조치보고 문구가 없습니다 — 기본 문구를 씁니다.");
                return Remember(new ActionReportPhraseSet(Fallback, false));
            }
            return Remember(new ActionReportPhraseSet(ordered, true));
        }
        catch (OperationCanceledException) { return LastKnown; }
        catch (Exception ex)
        {
            _log?.Warning($"[ActionReportPhrase] 문구 목록 조회 실패 — 기본 문구를 씁니다: {ex.Message}");
            return Remember(new ActionReportPhraseSet(Fallback, false));
        }
    }

    /// <summary>
    /// 서버 목록 → 보일 문구. <c>display_order</c> 오름차순, 같으면 id 오름차순(서버가 주는 순서와 같다 — 그래도 믿지 않고 다시 세운다).
    /// 빈 글 · 중복 · '기타' 는 뺀다('기타' 는 화면이 끝에 따로 붙인다).
    /// </summary>
    public static IReadOnlyList<string> Order(IEnumerable<ActionReportTemplateDto>? templates)
    {
        if (templates is null) return Array.Empty<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (var t in templates.Where(t => t is not null).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id))
        {
            var text = t.Content?.Trim();
            if (string.IsNullOrEmpty(text) || text == EtcPhrase || !seen.Add(text)) continue;
            list.Add(text);
        }
        return list;
    }

    private ActionReportPhraseSet Remember(ActionReportPhraseSet set)
    {
        Volatile.Write(ref _lastKnown, set);
        return set;
    }
}

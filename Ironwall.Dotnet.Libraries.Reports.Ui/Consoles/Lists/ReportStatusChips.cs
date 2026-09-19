using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;

/// <summary>
/// 상태 필터 칩 하나(WL L1279 — 콤보를 칩 5종으로 바꾼다).
/// </summary>
/// <remarks>
/// <see cref="Value"/> 는 <b>서버 어휘</b>(대문자)이고 <see cref="Display"/> 는 화면 글자다.
/// 색이 아니라 <b>눌림 형태</b>로 고른 것을 보인다 — 라이트에서 Primary 와 Selection 이 같은 색이다.
/// </remarks>
public sealed class ReportStatusChip : PropertyChangedBase
{
    private bool _isSelected;

    public ReportStatusChip(string value, string display)
    {
        Value = value;
        Display = display;
    }

    /// <summary>서버로 보내는 코드(PENDING · GENERATING · COMPLETED · FAILED · CANCELLED).</summary>
    public string Value { get; }

    public string Display { get; }

    /// <summary>계측 이름 — 칩마다 따로 짚을 수 있어야 한다.</summary>
    public string AutomationId => $"Console.Reports.Filter.{Value}";

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; NotifyOfPropertyChange(); }
    }
}

/// <summary>
/// 상태 칩의 규칙 — <b>순수</b>. 어휘는 <see cref="ReportGenerationStatus"/> 한 곳에서만 만든다.
/// </summary>
/// <remarks>
/// ★ "전체"는 <b>값이 아니라 미전송</b>이다(WL L1292). 빈 문자열을 실어 보내면 v8 서버가 그 자리에서 422 다
/// — "필터 없음"은 파라미터를 <b>붙이지 않는 것</b>이다.
/// </remarks>
public static class ReportStatusChipRules
{
    /// <summary>아무 칩도 안 눌린 상태를 부르는 이름. <b>서버 어휘가 아니다</b> — 화면 표지일 뿐이다.</summary>
    public const string AllLabel = "전체";

    public static string DisplayOf(string status) => status switch
    {
        ReportGenerationStatus.Pending => "대기",
        ReportGenerationStatus.Generating => "생성중",
        ReportGenerationStatus.Completed => "완료",
        ReportGenerationStatus.Failed => "실패",
        ReportGenerationStatus.Cancelled => "취소",
        _ => status,
    };

    /// <summary>칩 5종을 목업 순서(수명주기)대로 만든다.</summary>
    public static IReadOnlyList<ReportStatusChip> Create()
        => ReportGenerationStatus.All.Select(s => new ReportStatusChip(s, DisplayOf(s))).ToList();

    /// <summary>
    /// 눌린 칩 → 서버에 보낼 <c>status</c> 파라미터. 아무것도 안 눌렸으면 <c>null</c>(= 파라미터를 붙이지 않는다).
    /// </summary>
    /// <remarks>
    /// 서버는 <c>status</c> 를 <b>하나</b>만 받는다. 그래서 칩은 서로 배타적이고, 혹시 여럿이 눌려 있으면
    /// 첫 번째만 쓴다(여러 개를 합쳐 보낼 방법이 없으므로 조용히 어긋나게 두지 않는다).
    /// </remarks>
    public static string? ToRequestParameter(IEnumerable<ReportStatusChip> chips)
    {
        var picked = chips.Where(c => c.IsSelected).Select(c => c.Value).FirstOrDefault();
        return ReportGenerationStatus.Normalize(picked);
    }

    /// <summary>칩 하나를 누른 결과를 반영한다 — 배타 선택 + 같은 칩을 다시 누르면 "전체"로 돌아간다.</summary>
    public static void Toggle(IEnumerable<ReportStatusChip> chips, ReportStatusChip target)
    {
        var turnOn = !target.IsSelected;
        foreach (var chip in chips) chip.IsSelected = turnOn && ReferenceEquals(chip, target);
    }

    /// <summary>상태 띠 · 툴팁에 쓸 지금 필터 이름.</summary>
    public static string SummaryOf(IEnumerable<ReportStatusChip> chips)
        => chips.FirstOrDefault(c => c.IsSelected)?.Display ?? AllLabel;
}

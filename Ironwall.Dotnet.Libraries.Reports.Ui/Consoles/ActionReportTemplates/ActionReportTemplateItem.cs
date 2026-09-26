using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;

/// <summary>
/// 조치보고 문구 한 줄 — 목록 · 드래그 보드가 함께 쓴다.
/// </summary>
/// <remarks>
/// <see cref="Content"/> 가 곧 화면 표시값(<see cref="Display"/>)이다 — 별도 라벨을 두지 않는다.
/// </remarks>
public sealed class ActionReportTemplateItem : PropertyChangedBase
{
    public ActionReportTemplateItem(int id, string content, int displayOrder)
    {
        Id = id;
        _content = content ?? string.Empty;
        _displayOrder = displayOrder;
    }

    /// <summary>서버 PK.</summary>
    public int Id { get; }

    private string _content;
    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            _content = value ?? string.Empty;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(Display));
        }
    }

    private int _displayOrder;
    /// <summary>서버가 마지막으로 확인해 준 순번 — 화면 순서 자체는 <see cref="ActionReportTemplateBoard.Items"/> 가 쥔다.</summary>
    public int DisplayOrder
    {
        get => _displayOrder;
        set { if (_displayOrder == value) return; _displayOrder = value; NotifyOfPropertyChange(); }
    }

    private int _position;
    /// <summary>
    /// 화면 순번(1부터) — 목록의 순번 칸이 이것을 보인다. 서버 <c>display_order</c> 는 0부터라(A1: 목록이 0 · 1 · 2 … 로
    /// 찍혔다) 그대로 보이지 않고, 보드가 화면 순서대로 매긴다(<see cref="ActionReportTemplateBoard"/>).
    /// </summary>
    public int Position
    {
        get => _position;
        set { if (_position == value) return; _position = value; NotifyOfPropertyChange(); }
    }

    public string Display => Content;

    /// <summary>바인딩식 자동화 식별자 — 고정 리터럴은 인스턴스마다 복제된다.</summary>
    public string AutomationId => $"Reports.ActionReportTemplate.Row.{Id}";

    public override string ToString() => Content;
}

using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 표의 한 줄 — 모델(<see cref="WiringSensorRow"/>)을 화면에 붙인다. 판정은 하지 않는다.
/// </summary>
/// <remarks>칸은 <b>한 번 누르면 바로 입력</b>이라(WS L360) 칸마다 입력 컨트롤이 늘 떠 있고, 여기 속성이 그 값이다.</remarks>
public sealed class SensorRowViewModel : PropertyChangedBase
{
    private readonly Func<WiringSensorRow, WiringPlacement?> _placement;
    private readonly Action<SensorRowViewModel>? _edited;

    public SensorRowViewModel(WiringSensorRow row, Func<WiringSensorRow, WiringPlacement?> placement, Action<SensorRowViewModel>? edited = null)
    {
        Row = row ?? throw new ArgumentNullException(nameof(row));
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        _edited = edited;
    }

    public WiringSensorRow Row { get; }

    public int Key => Row.Key;

    public string NumberText
    {
        get => Row.Facts.Number.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return;
            if (number < 1 || number > SensorTableEdit.MAX_NUMBER) return;
            if (number == Row.Facts.Number) return;
            Row.Facts = Row.Facts with { Number = number };
            Edited();
        }
    }

    public string Name
    {
        get => Row.Facts.Name;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(text, Row.Facts.Name, StringComparison.Ordinal)) return;
            Row.Facts = Row.Facts with { Name = text };
            Edited();
        }
    }

    public string TypeText
    {
        get => Row.Facts.TypeText;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(text, Row.Facts.TypeText, StringComparison.Ordinal)) return;
            Row.Facts = Row.Facts with { TypeText = text };
            Edited();
        }
    }

    public string Zone
    {
        get => Row.Facts.Zone;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(text, Row.Facts.Zone, StringComparison.Ordinal)) return;
            Row.Facts = Row.Facts with { Zone = text };
            Edited();
        }
    }

    /// <summary>버스 주소 — 결선 순번과 다를 수 있어 둘 다 보인다(WS L478).</summary>
    public string ChannelText => Row.Channel is { } channel ? $"주소 {channel}" : "—";

    /// <summary>결선 자리 — "1차 4번" · "미배치".</summary>
    public string PlacementText => _placement(Row)?.Text ?? "미배치";

    /// <summary>저장 전까지 Draft(WS L181) — 새 줄이거나 값·자리가 바뀐 줄.</summary>
    public bool IsDraft => Row.IsNew || Row.FactsChanged || !WiringSpec.SamePlacement(_placement(Row), Row.BaselinePlacement);

    /// <summary>색이 아니라 글자로 — 새 줄 ＋ · 고친 줄 ● · 그대로면 빈 칸.</summary>
    public string StateGlyph => Row.IsNew ? "＋" : IsDraft ? "●" : string.Empty;

    public string StateTip => Row.IsNew ? "새로 만들 줄입니다 — [저장하기] 때 서버에 만듭니다."
        : IsDraft ? "고친 줄입니다 — [저장하기] 때 보냅니다."
        : "서버와 같습니다.";

    public string Display => Row.Display;

    /// <summary>읽지 못한 결선 값이 있으면 그 까닭.</summary>
    public string? LoadIssue => Row.LoadIssue;
    public bool HasLoadIssue => !string.IsNullOrEmpty(Row.LoadIssue);

    /// <summary>모델이 밖에서 바뀌었을 때(일괄 적용 · 되돌리기 · 저장) 화면을 맞춘다.</summary>
    public void Refresh()
    {
        NotifyOfPropertyChange(nameof(NumberText));
        NotifyOfPropertyChange(nameof(Name));
        NotifyOfPropertyChange(nameof(TypeText));
        NotifyOfPropertyChange(nameof(Zone));
        NotifyOfPropertyChange(nameof(ChannelText));
        NotifyOfPropertyChange(nameof(PlacementText));
        NotifyOfPropertyChange(nameof(IsDraft));
        NotifyOfPropertyChange(nameof(StateGlyph));
        NotifyOfPropertyChange(nameof(StateTip));
        NotifyOfPropertyChange(nameof(Display));
        NotifyOfPropertyChange(nameof(LoadIssue));
        NotifyOfPropertyChange(nameof(HasLoadIssue));
    }

    public override string ToString() => Display;

    private void Edited()
    {
        Refresh();
        _edited?.Invoke(this);
    }
}

/// <summary>
/// 결선맵의 칸 하나 — 빈 칸이거나 센서가 꽂힌 칸(WS L289-297).
/// </summary>
public sealed class WiringSlotViewModel : PropertyChangedBase
{
    private WiringSensorRow? _row;
    private int _order;
    private bool _isSelected;

    public WiringSlotViewModel(int line, int index)
    {
        Line = line;
        Index = index;
    }

    /// <summary>1 = 1차(나감) · 2 = 2차(들어옴).</summary>
    public int Line { get; }

    /// <summary>칸의 자리(0부터). 순번과 다르다 — 순번은 <b>찬 칸만</b> 센다.</summary>
    public int Index { get; }

    /// <summary>계측용 — <c>Devices.Wiring.Slot.1-3</c>.</summary>
    public string AutomationKey => $"{Line}-{Index}";

    public WiringSensorRow? Row
    {
        get => _row;
        internal set { _row = value; RefreshAll(); }
    }

    /// <summary>그 선에서의 순번(1부터). 빈 칸이면 0.</summary>
    public int Order
    {
        get => _order;
        internal set { _order = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(OrderText)); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; NotifyOfPropertyChange(); }
    }

    public bool IsFilled => _row is not null;
    public bool IsEmpty => _row is null;
    public string OrderText => _order > 0 ? _order.ToString(CultureInfo.InvariantCulture) : string.Empty;
    public string Title => _row?.Display ?? "빈 칸";
    public string Subtitle => _row is null
        ? string.Empty
        : _row.Channel is { } channel ? $"{_row.Facts.Number} · 주소 {channel}" : $"{_row.Facts.Number}";

    /// <summary>버스 주소와 순번이 다른가 — 화면에 둘 다 보이고, 고치지는 않는다.</summary>
    public bool HasChannelMismatch => _row?.Channel is { } channel && _order > 0 && channel != _order;

    /// <summary>드래그 고스트에 찍히는 글자.</summary>
    public string Display => _row?.Display ?? $"{Line}차 {Index + 1}번 칸";

    internal void RefreshAll()
    {
        NotifyOfPropertyChange(nameof(Row));
        NotifyOfPropertyChange(nameof(IsFilled));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(Title));
        NotifyOfPropertyChange(nameof(Subtitle));
        NotifyOfPropertyChange(nameof(HasChannelMismatch));
        NotifyOfPropertyChange(nameof(Display));
    }

    public override string ToString() => Display;
}

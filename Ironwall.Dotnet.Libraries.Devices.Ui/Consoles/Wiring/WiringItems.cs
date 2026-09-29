using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
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
    private readonly Func<WiringSensorRow, string?>? _placementText;

    /// <param name="placement"><b>저장될</b> 자리 — Draft 판정의 기준(제안 센서는 <c>null</c>).</param>
    /// <param name="placementText">자리 글자(없으면 <paramref name="placement"/> 의 글자) — 링의 "A3 · B32" · 제안 표지처럼 저장값과 다르게 보일 때.</param>
    public SensorRowViewModel(WiringSensorRow row, Func<WiringSensorRow, WiringPlacement?> placement, Action<SensorRowViewModel>? edited = null,
                              Func<WiringSensorRow, string?>? placementText = null)
    {
        Row = row ?? throw new ArgumentNullException(nameof(row));
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        _edited = edited;
        _placementText = placementText;
    }

    public WiringSensorRow Row { get; }

    public int Key => Row.Key;

    private string? _numberDraft;

    /// <summary>
    /// 번호 칸. 숫자가 아니거나 범위 밖이면 <b>값을 버리지 않고</b> 친 글자를 그대로 두고 까닭을 보인다(C11).
    /// </summary>
    public string NumberText
    {
        get => _numberDraft ?? Row.Facts.Number.ToString(CultureInfo.InvariantCulture);
        set
        {
            var text = (value ?? string.Empty).Trim();
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                _numberDraft = text;
                NumberError = $"번호는 숫자로 적어 주세요(1 ~ {SensorTableEdit.MAX_NUMBER}).";
                Edited();
                return;
            }
            if (number < 1 || number > SensorTableEdit.MAX_NUMBER)
            {
                _numberDraft = text;
                NumberError = $"번호는 1 과 {SensorTableEdit.MAX_NUMBER} 사이여야 합니다.";
                Edited();
                return;
            }

            _numberDraft = null;
            NumberError = null;
            if (number == Row.Facts.Number) { Edited(); return; }
            Row.Facts = Row.Facts with { Number = number };
            Edited();
        }
    }

    /// <summary>번호 칸의 까닭(없으면 <c>null</c>) — 저장은 이 줄을 건너뛰지 않는다. 고칠 때까지 옛 번호가 남는다.</summary>
    public string? NumberError { get; private set; }

    public bool HasNumberError => !string.IsNullOrEmpty(NumberError);

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
            NotifyOfPropertyChange(nameof(TypeDisplayText));
        }
    }

    /// <summary>
    /// 종류 콤보가 실제로 그리는 칸 — "한국어 (코드)"(<see cref="DeviceEnumDisplay.SensorTypeBilingual"/>).
    /// 서버로 나가는 값은 <see cref="TypeText"/> 그대로다: 드롭다운에서 병기 표시를 고르든 코드를 직접
    /// 타이핑하든, set 이 <see cref="DeviceEnumDisplay.ExtractSensorTypeCode"/> 로 코드만 추려 <see cref="TypeText"/>
    /// 에 쓴다(raw enum 이름이 칸에 남지 않으면서도 wire value 는 바뀌지 않는다).
    /// </summary>
    public string TypeDisplayText
    {
        get => DeviceEnumDisplay.SensorTypeBilingual(TypeText);
        set => TypeText = DeviceEnumDisplay.ExtractSensorTypeCode(value);
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

    /// <summary>결선 자리 — "A4 · B31" · "왼쪽 2번" · "제안 · A5" · "미배치".</summary>
    public string PlacementText => _placementText?.Invoke(Row) ?? _placement(Row)?.Text ?? "미배치";

    /// <summary>저장 전까지 Draft(WS L181) — 새 줄이거나 값·자리가 바뀐 줄.</summary>
    public bool IsDraft => Row.IsNew || Row.FactsChanged || !WiringSpec.SameWiring(_placement(Row), Row.BaselinePlacement);

    /// <summary>색이 아니라 글자로 — 새 줄 ＋ · 고친 줄 ● · 그대로면 빈 칸.</summary>
    public string StateGlyph => Row.IsNew ? "＋" : IsDraft ? "●" : string.Empty;

    public string StateTip => Row.IsNew ? "새로 만들 줄입니다 — [저장하기]를 누르면 등록됩니다."
        : IsDraft ? "고친 줄입니다 — [저장하기]를 누르면 저장됩니다."
        : "저장된 상태입니다.";

    public string Display => Row.Display;

    /// <summary>읽지 못한 결선 값이 있으면 그 까닭.</summary>
    public string? LoadIssue => Row.LoadIssue;
    public bool HasLoadIssue => !string.IsNullOrEmpty(Row.LoadIssue);

    /// <summary>모델이 밖에서 바뀌었을 때(일괄 적용 · 되돌리기 · 저장) 화면을 맞춘다.</summary>
    public void Refresh()
    {
        NotifyOfPropertyChange(nameof(NumberText));
        NotifyOfPropertyChange(nameof(NumberError));
        NotifyOfPropertyChange(nameof(HasNumberError));
        NotifyOfPropertyChange(nameof(Name));
        NotifyOfPropertyChange(nameof(TypeText));
        NotifyOfPropertyChange(nameof(TypeDisplayText));
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
    private string _portText = string.Empty;
    private bool _isSuggested;
    private string _lineName = string.Empty;

    public WiringSlotViewModel(int line, int index)
    {
        Line = line;
        Index = index;
    }

    /// <summary>목록 보기의 선 — 링 · 한 줄은 1(체인), 양쪽 가지는 1 = 왼쪽 · 2 = 오른쪽.</summary>
    public int Line { get; }

    /// <summary>목록 안의 자리(0부터) — 순번 = 자리 + 1. 선 끝의 빈 칸은 "끝에 붙이기" 자리다.</summary>
    public int Index { get; }

    /// <summary>링 칩의 양 포트 번호 "A3 · B32"(링이 아니면 빈 글자).</summary>
    public string PortText
    {
        get => _portText;
        internal set { _portText = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasPortText)); NotifyOfPropertyChange(nameof(Tooltip)); }
    }

    public bool HasPortText => _portText.Length > 0;

    /// <summary>번호순 <b>제안</b>으로 붙은 센서인가(FR-03) — 모서리 "제안" 글자로 보인다(색이 아니라 글자).</summary>
    public bool IsSuggested
    {
        get => _isSuggested;
        internal set { _isSuggested = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Tooltip)); }
    }

    /// <summary>사람이 읽는 선 이름 — "체인" · "왼쪽 가지" · "오른쪽 가지".</summary>
    public string LineName
    {
        get => _lineName.Length > 0 ? _lineName : $"{Line}차 선";
        internal set { _lineName = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Tooltip)); }
    }

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
        internal set
        {
            _order = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(OrderText));
            NotifyOfPropertyChange(nameof(HasChannelMismatch));
            NotifyOfPropertyChange(nameof(Tooltip));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; NotifyOfPropertyChange(); }
    }

    public bool IsFilled => _row is not null;
    public bool IsEmpty => _row is null;

    /// <summary>2차 선인가 — 배지 모양을 가르는 값(형태로 선을 구분한다 · W4).</summary>
    public bool IsSecondLine => Line == WiringSpec.LINE_SECONDARY;

    public string OrderText => _order > 0 ? _order.ToString(CultureInfo.InvariantCulture) : string.Empty;
    public string Title => _row?.Display ?? "빈 칸";

    /// <summary>선 끝의 빈 칸 — 여기 놓으면 끝에 붙는다(체인에는 가운데 빈 칸이 없다).</summary>
    public string SlotLabel => "여기 놓으면 끝에 붙습니다";

    /// <summary>좁은 칸(104)에서는 번호가 먼저다 — 이름은 잘리고 전체는 툴팁에 있다(W8).</summary>
    public string NumberText => _row is null ? string.Empty : _row.Facts.Number.ToString(CultureInfo.InvariantCulture);

    public string AddressText => _row?.Channel is { } channel ? $"주소 {channel}" : string.Empty;

    /// <summary>칸에 마우스를 올리면 전부 보인다(좁은 칸에서 잘린 이름 · 주소 · 자리).</summary>
    public string Tooltip
    {
        get
        {
            if (_row is null) return $"{LineName} 끝 — 여기 놓으면 끝에 붙습니다";

            var address = _row.Channel is { } channel ? $" · 버스 주소 {channel}" : string.Empty;
            var port = HasPortText ? $" · {_portText}" : string.Empty;
            var suggested = _isSuggested ? " · 번호순 제안(아직 저장 대기 아님)" : string.Empty;
            return string.Join(Environment.NewLine,
                _row.Display,
                $"번호 {_row.Facts.Number}{address}",
                $"{LineName} {Index + 1}번 자리{port}{suggested}");
        }
    }

    public string Subtitle => _row is null
        ? string.Empty
        : _row.Channel is { } channel ? $"{_row.Facts.Number} · 주소 {channel}" : $"{_row.Facts.Number}";

    /// <summary>버스 주소와 순번이 다른가 — 화면에 둘 다 보이고, 고치지는 않는다.</summary>
    public bool HasChannelMismatch => _row?.Channel is { } channel && _order > 0 && channel != _order;

    #region - 표 보기 열(표 보기 정리 · 2026-09-30) -
    private string _typeText = string.Empty;
    private string _typeIcon = "Radar";
    private string _facingText = string.Empty;
    private string _gapText = string.Empty;
    private bool _isDraft;
    private bool _isDuplicateNumber;
    private bool _enclosureBefore;
    private string _enclosureLabel = string.Empty;

    /// <summary>종류 짧은 글 — "스마트 복합" · "복합" · "펜스" · "지진동".</summary>
    public string TypeText { get => _typeText; internal set { _typeText = value ?? string.Empty; NotifyOfPropertyChange(); } }

    /// <summary>종류 아이콘 이름(<c>PackIconKind</c> — 뷰의 변환기가 없는 이름이면 기본 아이콘으로 바꾼다).</summary>
    public string TypeIcon { get => _typeIcon; internal set { _typeIcon = value ?? "Radar"; NotifyOfPropertyChange(); } }

    /// <summary>방향 "앞" · "뒤" — 기둥 센서(스마트 복합 · 복합)만, 그 밖은 "—"(FR-20).</summary>
    public string FacingText { get => _facingText; internal set { _facingText = value ?? string.Empty; NotifyOfPropertyChange(); } }

    /// <summary>앞 센서와의 간격 "6m"(첫 센서는 "—") — 간격 표(두 종류 중 작은 값 · 현장 펜스 간격).</summary>
    public string GapText { get => _gapText; internal set { _gapText = value ?? string.Empty; NotifyOfPropertyChange(); } }

    /// <summary>저장 전 바뀐 줄(새 줄 · 값 · 자리 · 방향) — 상태 열 "미저장".</summary>
    public bool IsDraft { get => _isDraft; internal set { _isDraft = value; NotifyOfPropertyChange(); } }

    /// <summary>같은 장비번호가 둘 이상 — 상태 열 "번호 같음"(id 순).</summary>
    public bool IsDuplicateNumber { get => _isDuplicateNumber; internal set { _isDuplicateNumber = value; NotifyOfPropertyChange(); } }

    /// <summary>이 줄 <b>앞</b>이 함체(제어기) 자리 — 체인이 여기서 A 쪽 · B 쪽으로 갈린다(구분 띠).</summary>
    public bool EnclosureBefore { get => _enclosureBefore; internal set { _enclosureBefore = value; NotifyOfPropertyChange(); } }

    /// <summary>구분 띠 글자 — "▲ A 쪽 1~6 · 함체(제어기) · B 쪽 7~13 ▼".</summary>
    public string EnclosureLabel { get => _enclosureLabel; internal set { _enclosureLabel = value ?? string.Empty; NotifyOfPropertyChange(); } }

    /// <summary>버스 주소 숫자만("—" 없음은 빈 글자).</summary>
    public string ChannelNumberText => _row?.Channel is { } channel ? channel.ToString(CultureInfo.InvariantCulture) : "—";
    #endregion

    /// <summary>드래그 고스트에 찍히는 글자.</summary>
    public string Display => _row?.Display ?? $"{LineName} 끝";

    internal void RefreshAll()
    {
        NotifyOfPropertyChange(nameof(Row));
        NotifyOfPropertyChange(nameof(IsFilled));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(Title));
        NotifyOfPropertyChange(nameof(Subtitle));
        NotifyOfPropertyChange(nameof(NumberText));
        NotifyOfPropertyChange(nameof(AddressText));
        NotifyOfPropertyChange(nameof(Tooltip));
        NotifyOfPropertyChange(nameof(SlotLabel));
        NotifyOfPropertyChange(nameof(HasChannelMismatch));
        NotifyOfPropertyChange(nameof(Display));
        NotifyOfPropertyChange(nameof(ChannelNumberText));
    }

    public override string ToString() => Display;
}

/// <summary>
/// 그룹 3상태 칸 하나(WS L614-618) — 전부 · 하나도 · <b>줄마다 다름</b>.
/// </summary>
/// <remarks>섞인 칸은 <b>색이 아니라 글자</b>로도 말한다("줄마다 다름") — 색만으로 뜻을 전하지 않는다.</remarks>
public sealed class WiringGroupCheckViewModel : PropertyChangedBase
{
    private GroupCheck _state = GroupCheck.None;
    private bool _isTouched;

    public WiringGroupCheckViewModel(WiringGroupInfo group)
    {
        Id = group.Id;
        Name = group.Name;
    }

    public int Id { get; }
    public string Name { get; }

    public GroupCheck State => _state;

    /// <summary>체크 모양 — 섞임은 <c>null</c>(3상태 체크박스).</summary>
    public bool? IsChecked => _state switch
    {
        GroupCheck.All => true,
        GroupCheck.None => false,
        _ => null,
    };

    /// <summary>사람이 이 그룹을 건드렸는가 — 건드린 그룹만 저장 때 나간다.</summary>
    public bool IsTouched => _isTouched;

    public bool IsMixed => _state == GroupCheck.Mixed;

    /// <summary>섞인 칸에 붙는 글자(WS L617).</summary>
    public string MixedText => IsMixed ? "줄마다 다름" : string.Empty;

    internal void Update(GroupCheck state, bool touched)
    {
        _state = state;
        _isTouched = touched;
        NotifyOfPropertyChange(nameof(State));
        NotifyOfPropertyChange(nameof(IsChecked));
        NotifyOfPropertyChange(nameof(IsTouched));
        NotifyOfPropertyChange(nameof(IsMixed));
        NotifyOfPropertyChange(nameof(MixedText));
    }

    public override string ToString() => Name;
}

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 날짜 하나(또는 날짜 + 시각 하나)를 적거나 고르는 커널 칸 — <see cref="DateTimeRangeField"/> 의 단일 값 짝.
/// </summary>
/// <remarks>
/// <para>B7(window-design-inventory-analysis.md #49 · #50 · #51) — 콘솔 안에 남아 있던 WPF <c>DatePicker</c>(MDIX 기본 달력 · Teal)와
/// <c>mah:DateTimePicker</c>(펼친 달력은 MahApps 기본)를 대체한다. 달력 판 · 낱칸 · 머리 단추 · 팝업 틀 · 미리보기 · [취소][적용] 은
/// 범위 칸과 <b>같은 부품</b>이다(Generic.xaml 의 <c>Console.DateTimeRange.*</c> 스타일 · <see cref="ConsoleCalendarSkin"/>) — 베끼지 않는다.</para>
/// <para>운영자 흐름은 옛 피커 그대로 — ① 글자로 바로 적는다(<c>yyyy-MM-dd</c> / <c>yyyy-MM-dd HH:mm</c>) ② 달력 단추(또는 Alt+↓ · F4)로
/// 팝업을 열어 고른다. 날짜 칸은 날짜를 누르면 곧바로 확정되고(옛 DatePicker 와 같다), 시각 칸은 날짜 · 시각을 고른 뒤 [적용](Enter).
/// Esc 는 확정 없이 닫는다. <see cref="AllowEmpty"/> 면 비울 수 있다(글자를 지우거나 [비우기]).</para>
/// <para>값 검증(시작 ≤ 종료 등)은 옛 피커처럼 칸이 아니라 뷰모델이 한다 — 칸은 "날짜로 읽을 수 없는 글자" 만 막고, 그때는 확정하지 않은 채
/// 굵은 위험 테두리(형태)로 알리다가 칸을 떠나면 마지막 값으로 되돌린다.</para>
/// <para>자동화: 칸 자신이 소비자가 준 AutomationId 를 달고 UIA 에 나온다(ExpandCollapse · Value 패턴). 그 안의 글자 칸이 Edit 로 나와
/// 옛 DatePicker 처럼 ValuePattern.SetValue 로 값을 넣을 수 있다 — 호스트 UiTests(AppSweep T-ACT009~015)가 그 길을 쓴다.</para>
/// </remarks>
[TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Trigger", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_PopupContent", Type = typeof(UIElement))]
[TemplatePart(Name = "PART_Calendar", Type = typeof(Calendar))]
[TemplatePart(Name = "PART_Time", Type = typeof(TimePicker))]
[TemplatePart(Name = "PART_Apply", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Cancel", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Clear", Type = typeof(ButtonBase))]
public class DateTimeField : Control
{
    static DateTimeField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DateTimeField), new FrameworkPropertyMetadata(typeof(DateTimeField)));
        // 칸 자신은 탭 멈춤이 아니다 — 포커스는 안쪽 글자 칸이 받는다(탭 한 번에 바로 적을 수 있게).
        FocusableProperty.OverrideMetadata(typeof(DateTimeField), new FrameworkPropertyMetadata(false));
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(DateTimeField), new FrameworkPropertyMetadata(false));
    }

    #region - Properties -
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(DateTime?), typeof(DateTimeField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    /// <summary>값 — 날짜 칸은 자정, 시각 칸은 분 단위. <see cref="AllowEmpty"/> 가 아니면 칸이 null 을 확정하지 않는다
    /// (그래서 <c>DateTime</c>(null 불가) 뷰모델 속성에 그대로 바인딩해도 안전하다).</summary>
    public DateTime? Value { get => (DateTime?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public static readonly DependencyProperty IncludeTimeProperty = DependencyProperty.Register(
        nameof(IncludeTime), typeof(bool), typeof(DateTimeField), new PropertyMetadata(false, OnFormatChanged));
    /// <summary>시각(시:분)까지 다루는가 — 한시 부여처럼 "오늘 몇 시부터" 가 뜻을 갖는 곳.</summary>
    public bool IncludeTime { get => (bool)GetValue(IncludeTimeProperty); set => SetValue(IncludeTimeProperty, value); }

    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(
        nameof(AllowEmpty), typeof(bool), typeof(DateTimeField), new PropertyMetadata(false));
    /// <summary>비울 수 있는가(값 null) — 예: 한시 부여 종료 "비우면 기한 없음".</summary>
    public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(DateTimeField), new PropertyMetadata("날짜 선택"));
    /// <summary>비었을 때 칸에 흐리게 보이는 안내.</summary>
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(DateTimeField),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));
    public bool IsDropDownOpen { get => (bool)GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    private static readonly DependencyPropertyKey IsInputInvalidPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsInputInvalid), typeof(bool), typeof(DateTimeField), new PropertyMetadata(false));
    public static readonly DependencyProperty IsInputInvalidProperty = IsInputInvalidPropertyKey.DependencyProperty;
    /// <summary>지금 칸의 글자를 날짜로 읽을 수 없다(확정하지 않았다) — 템플릿이 굵은 위험 테두리로 알린다.</summary>
    public bool IsInputInvalid { get => (bool)GetValue(IsInputInvalidProperty); private set => SetValue(IsInputInvalidPropertyKey, value); }

    private static readonly DependencyPropertyKey PreviewTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PreviewText), typeof(string), typeof(DateTimeField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty PreviewTextProperty = PreviewTextPropertyKey.DependencyProperty;
    /// <summary>팝업 안 미리보기 — 확정 전 "지금 고른 값".</summary>
    public string PreviewText { get => (string)GetValue(PreviewTextProperty); private set => SetValue(PreviewTextPropertyKey, value); }

    private static readonly DependencyPropertyKey InputHintPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(InputHint), typeof(string), typeof(DateTimeField), new PropertyMetadata(DateTimeFieldText.InputHint(false)));
    public static readonly DependencyProperty InputHintProperty = InputHintPropertyKey.DependencyProperty;
    /// <summary>입력 안내(도움말) — 표기 규칙을 예시로.</summary>
    public string InputHint { get => (string)GetValue(InputHintProperty); private set => SetValue(InputHintPropertyKey, value); }
    #endregion

    private TextBox? _textBox;
    private Calendar? _calendar;
    private TimePicker? _time;
    private UIElement? _popupContent;
    private ButtonBase? _apply;
    private ButtonBase? _cancel;
    private ButtonBase? _clear;
    private DateTime _draft;
    private bool _isWritingText;
    private bool _isCommittingFromText;
    private bool _isSeeding;

    /// <summary>팝업 본문 — 스냅샷 도구가 <c>RenderTargetBitmap</c> 으로 직접 찍으려고 쓴다(<see cref="DateTimeRangeField.PopupContent"/> 와 같다). 제품 코드는 쓰지 않는다.</summary>
    public UIElement? PopupContent => _popupContent;

    /// <summary>안쪽 글자 칸 — 시험 · 미리보기 전용.</summary>
    public TextBox? InputBox => _textBox;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();

        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        _calendar = GetTemplateChild("PART_Calendar") as Calendar;
        _time = GetTemplateChild("PART_Time") as TimePicker;
        _popupContent = GetTemplateChild("PART_PopupContent") as UIElement;
        _apply = GetTemplateChild("PART_Apply") as ButtonBase;
        _cancel = GetTemplateChild("PART_Cancel") as ButtonBase;
        _clear = GetTemplateChild("PART_Clear") as ButtonBase;

        if (_textBox is not null)
        {
            _textBox.TextChanged += OnTextChanged;
            _textBox.LostKeyboardFocus += OnTextLostFocus;
            _textBox.PreviewKeyDown += OnTextPreviewKeyDown;
        }
        if (_calendar is not null)
        {
            _calendar.SelectedDatesChanged += OnCalendarSelectionChanged;
            _calendar.DisplayDateChanged += OnCalendarDisplayDateChanged;
            _calendar.PreviewMouseUp += OnCalendarPreviewMouseUp;
        }
        if (_time is not null) _time.SelectedTimeChanged += OnTimeChanged;
        if (_apply is not null) _apply.Click += OnApplyClick;
        if (_cancel is not null) _cancel.Click += OnCancelClick;
        if (_clear is not null) _clear.Click += OnClearClick;
        if (_popupContent is not null) _popupContent.PreviewKeyDown += OnPopupPreviewKeyDown;

        WriteText();
    }

    private void Unhook()
    {
        if (_textBox is not null)
        {
            _textBox.TextChanged -= OnTextChanged;
            _textBox.LostKeyboardFocus -= OnTextLostFocus;
            _textBox.PreviewKeyDown -= OnTextPreviewKeyDown;
        }
        if (_calendar is not null)
        {
            _calendar.SelectedDatesChanged -= OnCalendarSelectionChanged;
            _calendar.DisplayDateChanged -= OnCalendarDisplayDateChanged;
            _calendar.PreviewMouseUp -= OnCalendarPreviewMouseUp;
        }
        if (_time is not null) _time.SelectedTimeChanged -= OnTimeChanged;
        if (_apply is not null) _apply.Click -= OnApplyClick;
        if (_cancel is not null) _cancel.Click -= OnCancelClick;
        if (_clear is not null) _clear.Click -= OnClearClick;
        if (_popupContent is not null) _popupContent.PreviewKeyDown -= OnPopupPreviewKeyDown;
    }

    #region - 글자 칸 -
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (DateTimeField)d;
        // 글자 칸이 방금 확정한 값이면 글자를 다시 쓰지 않는다 — 입력 중인 커서 · 글자를 흔들지 않는다.
        if (!field._isCommittingFromText) field.WriteText();
        (UIElementAutomationPeer.FromElement(field) as DateTimeFieldAutomationPeer)?.RaiseValueChanged(e.OldValue as DateTime?, e.NewValue as DateTime?);
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (DateTimeField)d;
        field.InputHint = DateTimeFieldText.InputHint(field.IncludeTime);
        field.WriteText();
    }

    /// <summary>지금 값을 고정 표기로 칸에 쓴다(프로그램이 쓰는 글자는 확정 경로를 타지 않는다).</summary>
    private void WriteText()
    {
        IsInputInvalid = false;
        if (_textBox is null) return;
        var text = DateTimeFieldText.Format(Value, IncludeTime);
        if (string.Equals(_textBox.Text, text, StringComparison.Ordinal)) return;
        _isWritingText = true;
        try { _textBox.Text = text; }
        finally { _isWritingText = false; }
    }

    /// <summary>
    /// 글자가 온전한 값이 되는 순간 확정한다 — 키보드 입력이든 자동화의 ValuePattern.SetValue 든 같은 길.
    /// 읽을 수 없는 글자는 확정하지 않고 <see cref="IsInputInvalid"/> 로만 알린다(칸을 떠나면 되돌린다).
    /// </summary>
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isWritingText || _textBox is null) return;
        var result = DateTimeFieldText.Parse(_textBox.Text, IncludeTime, Value);
        switch (result.Kind)
        {
            case DateTimeFieldParseKind.Valid:
                IsInputInvalid = false;
                CommitFromText(result.Value);
                break;
            case DateTimeFieldParseKind.Empty when AllowEmpty:
                IsInputInvalid = false;
                CommitFromText(null);
                break;
            default:
                IsInputInvalid = true;
                break;
        }
    }

    private void CommitFromText(DateTime? value)
    {
        if (Nullable.Equals(Value, value)) return;
        _isCommittingFromText = true;
        try { Value = value; }
        finally { _isCommittingFromText = false; }
    }

    /// <summary>칸을 떠나면 표기를 바로잡는다 — 읽을 수 없던 글자는 마지막 값으로 되돌린다.</summary>
    private void OnTextLostFocus(object sender, KeyboardFocusChangedEventArgs e) => WriteText();

    private void OnTextPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        if (key == Key.F4 || (alt && key == Key.Down))
        {
            e.Handled = true;
            IsDropDownOpen = true;
            return;
        }
        if (key == Key.Enter)
        {
            WriteText();   // 확정은 이미 됐다(글자가 값이 되는 순간) — 표기만 바로잡는다. Enter 는 소비하지 않는다(폼의 기본 단추).
            return;
        }
        if (key == Key.Escape && _textBox is not null
            && !string.Equals(_textBox.Text, DateTimeFieldText.Format(Value, IncludeTime), StringComparison.Ordinal))
        {
            // 되돌릴 것이 있을 때만 Esc 를 쓴다 — 없으면 바깥(목록 선택 해제 · 창 닫기)이 받게 둔다.
            e.Handled = true;
            WriteText();
        }
    }
    #endregion

    #region - 팝업 -
    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (DateTimeField)d;
        if (e.NewValue is true) field.SeedDraft();
        else if (field._popupContent?.IsKeyboardFocusWithin == true) field._textBox?.Focus();   // 키보드로 닫았으면 칸으로 돌아온다
        (UIElementAutomationPeer.FromElement(field) as DateTimeFieldAutomationPeer)?.RaiseExpandCollapseChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    /// <summary>팝업이 열릴 때마다 지금 값으로 초안을 새로 만든다 — 지난번 취소로 남은 초안을 물려주지 않는다.</summary>
    private void SeedDraft()
    {
        _isSeeding = true;
        try
        {
            _draft = DateTimeFieldText.SeedDraft(Value, IncludeTime, DateTime.Now);
            if (_calendar is not null)
            {
                _calendar.SelectedDate = _draft.Date;
                _calendar.DisplayDate = _draft.Date;
            }
            if (_time is not null) _time.SelectedTime = _draft;
        }
        finally { _isSeeding = false; }

        RefreshPreview();

        if (_calendar is not null)
        {
            // 키보드 사용성 — 열자마자 화살표로 날짜를 옮길 수 있게. 처음 열 때는 DisplayDateChanged 가 안 뜨므로 재도색도 여기서.
            var calendar = _calendar;
            Dispatcher.BeginInvoke(new Action(() => calendar.Focus()), DispatcherPriority.Input);
            ConsoleCalendarSkin.RestyleDeferred(calendar);
        }
    }

    private void OnCalendarDisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
    {
        if (_calendar is not null) ConsoleCalendarSkin.RestyleDeferred(_calendar);
    }

    private void OnCalendarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSeeding || _calendar?.SelectedDate is not { } picked) return;
        _draft = DateTimeFieldText.WithDate(_draft, picked, IncludeTime);
        RefreshPreview();
    }

    /// <summary>
    /// 날짜 칸은 날짜를 누르면 곧바로 확정하고 닫는다(옛 DatePicker 와 같은 흐름). 시각 칸은 시각도 골라야 하므로 [적용] 을 기다린다.
    /// 어느 쪽이든 달력이 쥔 마우스 캡처는 푼다 — [적용] · [취소] 가 첫 클릭에 먹도록.
    /// </summary>
    private void OnCalendarPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        ConsoleCalendarSkin.ReleaseCalendarCapture();
        if (!IncludeTime && e.ChangedButton == MouseButton.Left && ConsoleCalendarSkin.IsInsideDayButton(e.OriginalSource))
            Dispatcher.BeginInvoke(new Action(Commit), DispatcherPriority.Input);
    }

    private void OnTimeChanged(object sender, RoutedPropertyChangedEventArgs<DateTime?> e)
    {
        if (_isSeeding || _time?.SelectedTime is not { } picked) return;
        _draft = DateTimeFieldText.WithTime(_draft, picked);
        RefreshPreview();
    }

    private void RefreshPreview() => PreviewText = DateTimeFieldText.Format(_draft, IncludeTime);

    private void OnApplyClick(object sender, RoutedEventArgs e) => Commit();
    private void OnCancelClick(object sender, RoutedEventArgs e) => IsDropDownOpen = false;

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (AllowEmpty) Value = null;
        IsDropDownOpen = false;
    }

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                IsDropDownOpen = false;   // 확정 없이 닫는다 — 초안은 다음 SeedDraft 가 버린다
                break;
            case Key.Enter:
                e.Handled = true;
                Commit();
                break;
        }
    }

    /// <summary>초안을 확정하고 닫는다 — [적용] · Enter · (날짜 칸) 날짜 누름 공용 경로.</summary>
    private void Commit()
    {
        if (!IsDropDownOpen) return;
        Value = DateTimeFieldText.Normalize(_draft, IncludeTime);
        IsDropDownOpen = false;
    }
    #endregion

    #region - 자동화 -
    protected override AutomationPeer OnCreateAutomationPeer() => new DateTimeFieldAutomationPeer(this);

    /// <summary>자동화의 SetValue — 글자 칸에 쓴 것과 같은 규칙으로 읽는다(읽을 수 없으면 거부).</summary>
    internal void SetValueFromAutomation(string text)
    {
        var result = DateTimeFieldText.Parse(text, IncludeTime, Value);
        switch (result.Kind)
        {
            case DateTimeFieldParseKind.Valid:
                Value = result.Value;
                break;
            case DateTimeFieldParseKind.Empty when AllowEmpty:
                Value = null;
                break;
            default:
                throw new ArgumentException(DateTimeFieldText.InputHint(IncludeTime), nameof(text));
        }
    }
    #endregion
}

/// <summary>
/// 칸 자신의 UIA 모습 — 소비자의 AutomationId 로 나오고(커스텀 Control 은 peer 가 없어 트리에 안 나온다 — 그래서 직접 둔다),
/// 펼치기(ExpandCollapse)와 값(Value) 패턴을 낸다. 안쪽 글자 칸(Edit) · 달력 단추는 자식으로 그대로 나온다.
/// </summary>
public sealed class DateTimeFieldAutomationPeer : FrameworkElementAutomationPeer, IExpandCollapseProvider, IValueProvider
{
    public DateTimeFieldAutomationPeer(DateTimeField owner) : base(owner) { }

    private DateTimeField Field => (DateTimeField)Owner;

    protected override string GetClassNameCore() => nameof(DateTimeField);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    protected override string GetLocalizedControlTypeCore() => Field.IncludeTime ? "날짜 시각 입력" : "날짜 입력";

    public override object GetPattern(PatternInterface patternInterface)
        => patternInterface is PatternInterface.ExpandCollapse or PatternInterface.Value ? this : base.GetPattern(patternInterface);

    public ExpandCollapseState ExpandCollapseState => Field.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public void Expand()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        Field.IsDropDownOpen = true;
    }

    public void Collapse()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        Field.IsDropDownOpen = false;
    }

    public bool IsReadOnly => !Field.IsEnabled;

    public string Value => DateTimeFieldText.Format(Field.Value, Field.IncludeTime);

    public void SetValue(string value)
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        Field.SetValueFromAutomation(value);
    }

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
        => RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                                     oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                                     newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

    internal void RaiseValueChanged(DateTime? oldValue, DateTime? newValue)
        => RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty,
                                     DateTimeFieldText.Format(oldValue, Field.IncludeTime),
                                     DateTimeFieldText.Format(newValue, Field.IncludeTime));
}

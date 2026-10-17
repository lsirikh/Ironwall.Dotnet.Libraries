using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 날짜·시각 범위를 <b>한 팝업</b>에서 고른다 — 클릭한 그 자리에 뜨는 달력(연속 범위 선택) + 시작/종료 시각.
/// </summary>
/// <remarks>
/// <para>두 개의 <c>mah:DateTimePicker</c> 를 나란히 두던 옛 방식을 대체한다(사용자 결정, 2026-09-23) —
/// "직접 해서 날짜를 입력하는 것은 custom control 로 만들던지... 팝업해서 달력의 영역과 시간을 고르는 ui".</para>
/// <para>달력은 WPF 표준 <see cref="Calendar"/>(<see cref="Calendar.SelectionMode"/> = <c>SingleRange</c>) —
/// 시작·끝을 한 번의 드래그/Shift+클릭으로 함께 고른다. 시각은 MaterialDesignThemes <see cref="TimePicker"/> —
/// 이 앱이 <c>MaterialDesign3.Defaults.xaml</c> 을 실제로 병합하므로(App.xaml), MahApps 컨트롤에서 실측한
/// "베이스 사전 미병합 → DynamicResource 미해결 → MDIX 암시 스타일 침투" 결함군이 여기서는 재현되지 않는다.</para>
/// <para>표기는 <see cref="DateTimeRangeText"/> 로 고정한다(<c>yyyy-MM-dd HH:mm</c>, 고정 문화권) — 네 가지
/// 원 결함(영문 워터마크 · 미국식 포맷 · Teal 누출 · 폭 잘림) 은 새 컨트롤에서 원리적으로 재현되지 않는다.</para>
/// <para>키보드: 트리거는 <c>ToggleButton</c>(Space/Enter 로 열림) · 팝업 안 <c>Esc</c> 는 커밋 없이 닫는다 ·
/// <c>Enter</c> 는 [적용] 과 같다. 트리거는 계속 포커스 가능해 자동화가 좌표 클릭에 묶이지 않는다.</para>
/// </remarks>
[TemplatePart(Name = "PART_Trigger", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_PopupContent", Type = typeof(UIElement))]
[TemplatePart(Name = "PART_Calendar", Type = typeof(Calendar))]
[TemplatePart(Name = "PART_StartTime", Type = typeof(TimePicker))]
[TemplatePart(Name = "PART_EndTime", Type = typeof(TimePicker))]
[TemplatePart(Name = "PART_Apply", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Cancel", Type = typeof(ButtonBase))]
public class DateTimeRangeField : Control
{
    static DateTimeRangeField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DateTimeRangeField), new FrameworkPropertyMetadata(typeof(DateTimeRangeField)));
        // D-30 — 기본 제한폭. 툴바 왼쪽(필터) 칸은 Grid 의 Auto 열이라 폭이 모자라도 줄지 않는다
        // (ConsoleLayoutMath.ResolveToolbarSearchMinWidth 주석) — 그래서 이 컨트롤 스스로 "쉬는 상태"의
        // 요구폭을 줄여야 오버플로가 없어진다. 소비자가 XAML 에 로컬 MaxWidth 를 주면 그 값이 이긴다
        // (WPF 값 우선순위 — 타입 기본 메타데이터는 로컬 값보다 낮다).
        MaxWidthProperty.OverrideMetadata(typeof(DateTimeRangeField), new FrameworkPropertyMetadata(DateTimeRangeText.DefaultMaxWidth));
    }

    #region - Properties -
    public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(
        nameof(RangeStart), typeof(DateTime), typeof(DateTimeRangeField),
        new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));
    public DateTime RangeStart { get => (DateTime)GetValue(RangeStartProperty); set => SetValue(RangeStartProperty, value); }

    public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(
        nameof(RangeEnd), typeof(DateTime), typeof(DateTimeRangeField),
        new FrameworkPropertyMetadata(DateTime.Today.AddHours(1), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));
    public DateTime RangeEnd { get => (DateTime)GetValue(RangeEndProperty); set => SetValue(RangeEndProperty, value); }

    private static readonly DependencyPropertyKey DisplayStartTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DisplayStartText), typeof(string), typeof(DateTimeRangeField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DisplayStartTextProperty = DisplayStartTextPropertyKey.DependencyProperty;
    /// <summary>트리거에 실제로 찍히는 시작쪽 글자(D-30) — 배정받은 폭에 따라 짧아진 tier 표기.
    /// 전체 정밀도는 항상 <c>ToolTip</c>(<see cref="DateTimeRangeText.RangeText"/>)에 있다.</summary>
    public string DisplayStartText { get => (string)GetValue(DisplayStartTextProperty); private set => SetValue(DisplayStartTextPropertyKey, value); }

    private static readonly DependencyPropertyKey DisplayEndTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DisplayEndText), typeof(string), typeof(DateTimeRangeField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DisplayEndTextProperty = DisplayEndTextPropertyKey.DependencyProperty;
    /// <summary>트리거에 실제로 찍히는 종료쪽 글자(D-30) — <see cref="DisplayStartText"/> 와 같은 tier 로 맞춘다.</summary>
    public string DisplayEndText { get => (string)GetValue(DisplayEndTextProperty); private set => SetValue(DisplayEndTextPropertyKey, value); }

    public static readonly DependencyProperty FromAutomationIdProperty = Reg(nameof(FromAutomationId), string.Empty);
    /// <summary>트리거의 시작 텍스트에 붙는 AutomationId — 옛 두 피커 시절의 값을 그대로 물려받는다.</summary>
    public string FromAutomationId { get => (string)GetValue(FromAutomationIdProperty); set => SetValue(FromAutomationIdProperty, value); }

    public static readonly DependencyProperty ToAutomationIdProperty = Reg(nameof(ToAutomationId), string.Empty);
    public string ToAutomationId { get => (string)GetValue(ToAutomationIdProperty); set => SetValue(ToAutomationIdProperty, value); }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(DateTimeRangeField),
        new PropertyMetadata(false, OnIsDropDownOpenChanged));
    public bool IsDropDownOpen { get => (bool)GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    public static readonly DependencyProperty PreviewTextProperty = DependencyProperty.Register(
        nameof(PreviewText), typeof(string), typeof(DateTimeRangeField), new PropertyMetadata(string.Empty));
    /// <summary>팝업 안 미리보기 — 커밋 전 "지금 고른 값" 을 그대로 글자로 보여준다.</summary>
    public string PreviewText { get => (string)GetValue(PreviewTextProperty); private set => SetValue(PreviewTextProperty, value); }
    #endregion

    private Calendar? _calendar;
    private TimePicker? _startTime;
    private TimePicker? _endTime;
    private UIElement? _popupContent;
    private DateTime _draftStart;
    private DateTime _draftEnd;

    /// <summary>
    /// D-30 — 지금까지 관찰한 실제 배정폭. 첫 Arrange 이전에도 <see cref="DisplayStartText"/> 가 비어 있지
    /// 않도록 기본 <c>MaxWidth</c>(<see cref="DateTimeRangeText.DefaultMaxWidth"/>)로 씨앗을 둔다.
    /// </summary>
    private double _arrangedWidth = DateTimeRangeText.DefaultMaxWidth;

    /// <summary>
    /// 팝업 본문(<c>PART_PopupContent</c>) — <c>Popup</c> 은 자기 자신의 렌더 계층에 뜨므로
    /// (닫힌 창의 시각 트리를 걷어서는 안 닿는다) 스냅샷 도구가 <c>RenderTargetBitmap</c> 으로
    /// 직접 찍으려면 이 참조가 필요하다. 테스트/미리보기 전용 — 제품 코드는 쓰지 않는다.
    /// </summary>
    public UIElement? PopupContent => _popupContent;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();

        _calendar = GetTemplateChild("PART_Calendar") as Calendar;
        _startTime = GetTemplateChild("PART_StartTime") as TimePicker;
        _endTime = GetTemplateChild("PART_EndTime") as TimePicker;
        _popupContent = GetTemplateChild("PART_PopupContent") as UIElement;

        if (_calendar is not null)
        {
            _calendar.SelectedDatesChanged += OnCalendarSelectionChanged;
            // ⚠ 달력 낱칸 재도색(RestyleCalendarChildren) 배선 — 아래 그 메서드의 주석(K-13) 참조.
            _calendar.DisplayDateChanged += OnCalendarDisplayDateChanged;
            _calendar.PreviewMouseUp += OnCalendarPreviewMouseUp;
        }
        if (_startTime is not null) _startTime.SelectedTimeChanged += OnTimeChanged;
        if (_endTime is not null) _endTime.SelectedTimeChanged += OnTimeChanged;
        if (GetTemplateChild("PART_Apply") is ButtonBase apply) { apply.Click += OnApplyClick; _apply = apply; }
        if (GetTemplateChild("PART_Cancel") is ButtonBase cancel) { cancel.Click += OnCancelClick; _cancel = cancel; }
        if (_popupContent is not null) _popupContent.PreviewKeyDown += OnPopupPreviewKeyDown;

        // D-30 — 템플릿이 막 붙었으면 트리거 글자도 바로 채운다(다음 Arrange 를 기다리지 않는다).
        RefreshDisplayText();
    }

    /// <summary>
    /// D-30 — 부모가 실제로 제안한 폭(<c>MeasureOverride</c> 의 <paramref name="availableSize"/>)을 관찰해
    /// 다시 잰다. <b>Arrange 의 finalSize 가 아니라 Measure 의 availableSize 를 쓴다</b> — 이 컨트롤은
    /// 자기 내용에 맞춰 스스로 줄어드는(shrink-to-fit) Auto 열의 자식이라, Arrange 의 finalSize 는 "지금
    /// 고른 tier 자신이 필요로 하는 크기"를 그대로 되돌려줄 뿐이다(자기참조). 그 값으로 다음 tier 를
    /// 판정하면 매 패스마다 "방금 고른 tier 도 못 담는다"는 착시가 반복돼 Compact 까지 무조건 굴러떨어진다
    /// (실측: 1360px 에서도 9/22~9/23 로 끝까지 줄어들었다 — 09-22 17:00 처럼 더 긴 tier 가 맞는데도).
    /// availableSize 는 부모(무제한 StackPanel)와 <c>MaxWidth</c>(기본 <see cref="DateTimeRangeText.DefaultMaxWidth"/>,
    /// 소비자가 로컬로 더 주면 그 값)의 교집합이라 내용과 무관하게 고정된 "예산"이다 — 그래서 idempotent 하다.
    /// 0.5px 미만 흔들림은 무시한다(레이아웃 진동 방지).
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsPositiveInfinity(availableSize.Width) && Math.Abs(availableSize.Width - _arrangedWidth) > 0.5)
        {
            _arrangedWidth = availableSize.Width;
            RefreshDisplayText();
        }
        return base.MeasureOverride(availableSize);
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimeRangeField field) field.RefreshDisplayText();
    }

    /// <summary>
    /// <see cref="DateTimeRangeText.ResolveTier"/> 로 지금 폭에 맞는 tier 를 고르고, 양쪽 글자를 다시
    /// 채운다. 값이 실제로 바뀔 때만 <c>SetValue</c> 한다 — 불필요한 바인딩 재평가·재측정을 막는다.
    /// </summary>
    private void RefreshDisplayText()
    {
        var now = DateTime.Now;
        var tier = DateTimeRangeText.ResolveTier(RangeStart, RangeEnd, _arrangedWidth, now);
        var (start, end) = DateTimeRangeText.FormatPair(RangeStart, RangeEnd, tier, now);
        if (!string.Equals(start, DisplayStartText, StringComparison.Ordinal)) DisplayStartText = start;
        if (!string.Equals(end, DisplayEndText, StringComparison.Ordinal)) DisplayEndText = end;
    }

    private ButtonBase? _apply;
    private ButtonBase? _cancel;

    private void Unhook()
    {
        if (_calendar is not null)
        {
            _calendar.SelectedDatesChanged -= OnCalendarSelectionChanged;
            _calendar.DisplayDateChanged -= OnCalendarDisplayDateChanged;
            _calendar.PreviewMouseUp -= OnCalendarPreviewMouseUp;
        }
        if (_startTime is not null) _startTime.SelectedTimeChanged -= OnTimeChanged;
        if (_endTime is not null) _endTime.SelectedTimeChanged -= OnTimeChanged;
        if (_apply is not null) _apply.Click -= OnApplyClick;
        if (_cancel is not null) _cancel.Click -= OnCancelClick;
        if (_popupContent is not null) _popupContent.PreviewKeyDown -= OnPopupPreviewKeyDown;
    }

    private void OnCalendarDisplayDateChanged(object? sender, CalendarDateChangedEventArgs e) => RestyleCalendarChildrenDeferred();

    /// <summary>
    /// K-13 — 달력 낱칸 재도색. 경로와 실측 근거는 <see cref="ConsoleCalendarSkin"/> 에 있다(B7 — 단일 날짜 칸
    /// <see cref="DateTimeField"/> 와 나눠 쓰려고 옮겼다. 동작은 같다).
    /// </summary>
    private void RestyleCalendarChildrenDeferred()
    {
        if (_calendar is not null) ConsoleCalendarSkin.RestyleDeferred(_calendar);
    }

    /// <summary>날짜를 누른 뒤 달력이 쥔 마우스 캡처를 푼다 — [적용] 이 첫 클릭에 먹도록(<see cref="ConsoleCalendarSkin.ReleaseCalendarCapture"/>).</summary>
    private void OnCalendarPreviewMouseUp(object sender, MouseButtonEventArgs e) => ConsoleCalendarSkin.ReleaseCalendarCapture();

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimeRangeField field && e.NewValue is true) field.SeedDraft();
    }

    /// <summary>팝업이 열릴 때마다 지금 값으로 다시 씨앗을 뿌린다 — 지난번 취소로 남은 초안을 물려주지 않는다.</summary>
    private void SeedDraft()
    {
        _draftStart = RangeStart;
        _draftEnd = RangeEnd;

        if (_calendar is not null)
        {
            // ⚠ SelectionMode=SingleRange 에서 .Add() 를 날짜마다 반복 호출하면 하루만 남는다(실측:
            // 22~23 두 밤을 넣어도 23 한 칸만 선택돼 보였다) — AddRange 한 번이 범위를 통째로 넣는 정본 API.
            _calendar.SelectedDates.Clear();
            _calendar.SelectedDates.AddRange(_draftStart.Date, _draftEnd.Date);
            _calendar.DisplayDate = _draftStart.Date;
        }
        if (_startTime is not null) _startTime.SelectedTime = _draftStart;
        if (_endTime is not null) _endTime.SelectedTime = _draftEnd;

        RefreshPreview();

        if (_calendar is not null)
        {
            // 키보드 사용성 — 팝업을 열자마자 화살표로 날짜를 옮길 수 있어야 한다.
            Dispatcher.BeginInvoke(new Action(() => _calendar?.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            // K-13 — 달력을 처음 열 때는 DisplayDateChanged 가 안 뜬다(DisplayDate 가 이미 그 값이면 변화가 없다).
            // 그래서 여기서도 한 번 재도색을 예약한다 — 낱칸은 이 시점엔 아직 만들어지지 않았을 수 있어 지연 호출.
            RestyleCalendarChildrenDeferred();
        }
    }

    private void OnCalendarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_calendar is null || _calendar.SelectedDates.Count == 0) return;
        var min = _calendar.SelectedDates.Min();
        var max = _calendar.SelectedDates.Max();
        _draftStart = DateTimeRangeRules.Combine(min, _draftStart.TimeOfDay);
        _draftEnd = DateTimeRangeRules.Combine(max, _draftEnd.TimeOfDay);
        RefreshPreview();
    }

    private void OnTimeChanged(object sender, RoutedPropertyChangedEventArgs<DateTime?> e)
    {
        if (sender == _startTime && _startTime?.SelectedTime is { } st) _draftStart = DateTimeRangeRules.Combine(_draftStart, st.TimeOfDay);
        if (sender == _endTime && _endTime?.SelectedTime is { } et) _draftEnd = DateTimeRangeRules.Combine(_draftEnd, et.TimeOfDay);
        RefreshPreview();
    }

    private void RefreshPreview() => PreviewText = DateTimeRangeText.RangeText(_draftStart, _draftEnd);

    private void OnApplyClick(object sender, RoutedEventArgs e) => Commit();
    private void OnCancelClick(object sender, RoutedEventArgs e) => IsDropDownOpen = false;

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                IsDropDownOpen = false;   // 커밋 없이 닫는다 — 초안은 다음 SeedDraft 가 버린다
                break;
            case Key.Enter:
                e.Handled = true;
                Commit();
                break;
        }
    }

    /// <summary>초안을 검증하고(끝&lt;=시작이면 밀어 올림) 확정한다 — [적용] · Enter 공용 경로.</summary>
    private void Commit()
    {
        RangeStart = _draftStart;
        RangeEnd = DateTimeRangeRules.ClampEnd(_draftStart, _draftEnd);
        IsDropDownOpen = false;
    }

    private static DependencyProperty Reg<T>(string name, T defaultValue)
        => DependencyProperty.Register(name, typeof(T), typeof(DateTimeRangeField), new PropertyMetadata(defaultValue));
}

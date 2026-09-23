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
    }

    #region - Properties -
    public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(
        nameof(RangeStart), typeof(DateTime), typeof(DateTimeRangeField),
        new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public DateTime RangeStart { get => (DateTime)GetValue(RangeStartProperty); set => SetValue(RangeStartProperty, value); }

    public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(
        nameof(RangeEnd), typeof(DateTime), typeof(DateTimeRangeField),
        new FrameworkPropertyMetadata(DateTime.Today.AddHours(1), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public DateTime RangeEnd { get => (DateTime)GetValue(RangeEndProperty); set => SetValue(RangeEndProperty, value); }

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
        }
        if (_startTime is not null) _startTime.SelectedTimeChanged += OnTimeChanged;
        if (_endTime is not null) _endTime.SelectedTimeChanged += OnTimeChanged;
        if (GetTemplateChild("PART_Apply") is ButtonBase apply) { apply.Click += OnApplyClick; _apply = apply; }
        if (GetTemplateChild("PART_Cancel") is ButtonBase cancel) { cancel.Click += OnCancelClick; _cancel = cancel; }
        if (_popupContent is not null) _popupContent.PreviewKeyDown += OnPopupPreviewKeyDown;
    }

    private ButtonBase? _apply;
    private ButtonBase? _cancel;

    private void Unhook()
    {
        if (_calendar is not null)
        {
            _calendar.SelectedDatesChanged -= OnCalendarSelectionChanged;
            _calendar.DisplayDateChanged -= OnCalendarDisplayDateChanged;
        }
        if (_startTime is not null) _startTime.SelectedTimeChanged -= OnTimeChanged;
        if (_endTime is not null) _endTime.SelectedTimeChanged -= OnTimeChanged;
        if (_apply is not null) _apply.Click -= OnApplyClick;
        if (_cancel is not null) _cancel.Click -= OnCancelClick;
        if (_popupContent is not null) _popupContent.PreviewKeyDown -= OnPopupPreviewKeyDown;
    }

    private void OnCalendarDisplayDateChanged(object? sender, CalendarDateChangedEventArgs e) => RestyleCalendarChildrenDeferred();

    /// <summary>
    /// K-13 — CalendarDayButton/CalendarButton/Button(헤더·화살표)은 Style.Resources 도, Calendar 인스턴스
    /// 자신의 Resources 도, CalendarItem 자신의 ControlTemplate.Resources 도 전부 무력했다(실측 5종:
    /// Lime/Red 배경 · Visibility=Collapsed · CalendarItem 자체 Background 까지 반영 0). Aero2 가 이 내부
    /// 타입들의 Style 을 자기 자신의 테마 사전에서 성공적으로(=조용히 실패가 아니라) 찾아 명시로 물려서
    /// 어떤 암시/스코프 리소스도 끼어들 여지가 없다 — 유일하게 이기는 것은 <b>로컬 값</b> 뿐이다(WPF 속성
    /// 우선순위 최상단). 그래서 달력이 실제로 그 낱칸들을 만든 뒤(초기 · 월 이동 · 새로 열 때마다) 시각
    /// 트리를 걸어 Style 을 인스턴스별로 직접 대입한다. 이 메서드가 '진짜로 먹히는' 유일한 경로다(실측).
    /// </summary>
    private void RestyleCalendarChildrenDeferred()
        => Dispatcher.BeginInvoke(new Action(RestyleCalendarChildren), System.Windows.Threading.DispatcherPriority.Loaded);

    /// <summary>
    /// K-13 실측 계속 — <c>this.TryFindResource(...)</c> 조차 "Console.DateTimeRange.CalendarDayCell" 를 못
    /// 찾는다(진단: dayStyle=False, day=42 — 42개를 다 찾아 로컬 값(Background=Red)을 주는 건 즉시 반영됐다).
    /// DefaultStyleKey 테마 해석은 그 컨트롤 자신의 키(<c>{x:Type DateTimeRangeField}</c>)만 낱개로 찾아줄 뿐,
    /// Generic.xaml 사전 전체를 인스턴스의 앰비언트 Resources 에 병합하지 않는다 — 그래서 같은 사전 안의
    /// "형제" 키를 FindResource 로 조회할 방법이 없다. 사전을 직접 로드해서 읽는다(로컬 값 대입은 실측으로
    /// 확실히 이긴다 — 위 Background=Red 진단).
    /// </summary>
    private static readonly ResourceDictionary CalendarResources = new()
    {
        Source = new Uri("pack://application:,,,/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml"),
    };

    private void RestyleCalendarChildren()
    {
        if (_calendar is null) return;
        var dayStyle = CalendarResources["Console.DateTimeRange.CalendarDayCell"] as Style;
        var monthStyle = CalendarResources["Console.DateTimeRange.CalendarButtonCell"] as Style;
        var chromeStyle = CalendarResources["Console.DateTimeRange.CalendarButtonChrome"] as Style;

        foreach (var child in Descendants(_calendar))
        {
            switch (child)
            {
                case CalendarDayButton dayButton when dayStyle is not null:
                    dayButton.Style = dayStyle;
                    break;
                case CalendarButton monthButton when monthStyle is not null:
                    monthButton.Style = monthStyle;
                    break;
                case Button plainButton when chromeStyle is not null:
                    plainButton.Style = chromeStyle;
                    break;
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var grandchild in Descendants(child)) yield return grandchild;
        }
    }

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

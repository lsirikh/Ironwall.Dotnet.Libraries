using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Calendar = System.Windows.Controls.Calendar;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// <see cref="DateTimeFieldText"/> — 단일 날짜 칸(<see cref="DateTimeField"/>) 뒤의 순수 규칙. UI 없이 판정만 잡는다.
/// </summary>
public class DateTimeFieldTextTests
{
    [Fact]
    public void should_format_date_only_with_fixed_pattern_when_time_is_not_included()
        => Assert.Equal("2026-09-07", DateTimeFieldText.Format(new DateTime(2026, 9, 7, 15, 52, 12), includeTime: false));

    [Fact]
    public void should_format_date_and_minute_when_time_is_included()
        => Assert.Equal("2026-09-07 15:52", DateTimeFieldText.Format(new DateTime(2026, 9, 7, 15, 52, 12), includeTime: true));

    [Fact]
    public void should_return_empty_text_when_value_is_null()
        => Assert.Equal(string.Empty, DateTimeFieldText.Format(null, includeTime: true));

    [Fact]
    public void should_stay_fixed_regardless_of_thread_culture_when_formatting_and_parsing()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        try
        {
            Assert.Equal("2026-01-05", DateTimeFieldText.Format(new DateTime(2026, 1, 5), includeTime: false));
            Assert.Equal(new DateTime(2026, 1, 5), DateTimeFieldText.Parse("2026-01-05", false, null).Value);
        }
        finally { Thread.CurrentThread.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("2026-09-27")]
    [InlineData("2026.09.27")]
    [InlineData("2026/09/27")]
    [InlineData("  2026-09-27 ")]
    public void should_read_a_complete_date_when_the_text_uses_a_known_separator(string text)
    {
        var result = DateTimeFieldText.Parse(text, includeTime: false, current: null);

        Assert.Equal(DateTimeFieldParseKind.Valid, result.Kind);
        Assert.Equal(new DateTime(2026, 9, 27), result.Value);
    }

    [Theory]
    [InlineData("2026-09-1")]      // 입력 도중 — 1일로 먼저 확정되면 안 된다
    [InlineData("2026-9-27")]
    [InlineData("2026-13-01")]
    [InlineData("2026-02-30")]
    [InlineData("27/09/2026")]
    [InlineData("오늘")]
    public void should_not_read_a_value_when_the_text_is_partial_or_wrong(string text)
        => Assert.Equal(DateTimeFieldParseKind.Invalid, DateTimeFieldText.Parse(text, includeTime: false, current: null).Kind);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void should_report_empty_when_the_text_is_blank(string? text)
        => Assert.Equal(DateTimeFieldParseKind.Empty, DateTimeFieldText.Parse(text, includeTime: true, current: null).Kind);

    [Fact]
    public void should_read_date_and_time_and_drop_seconds_when_time_is_included()
    {
        Assert.Equal(new DateTime(2026, 9, 27, 14, 30, 0), DateTimeFieldText.Parse("2026-09-27 14:30", true, null).Value);
        Assert.Equal(new DateTime(2026, 9, 27, 14, 30, 0), DateTimeFieldText.Parse("2026-09-27 14:30:59", true, null).Value);
    }

    [Fact]
    public void should_keep_the_current_time_when_only_a_date_is_typed_into_a_time_field()
    {
        var current = new DateTime(2026, 9, 1, 9, 45, 0);

        var result = DateTimeFieldText.Parse("2026-09-27", includeTime: true, current);

        Assert.Equal(new DateTime(2026, 9, 27, 9, 45, 0), result.Value);
    }

    [Fact]
    public void should_drop_the_time_when_a_date_and_time_is_typed_into_a_date_field()
        => Assert.Equal(DateTimeFieldParseKind.Invalid, DateTimeFieldText.Parse("2026-09-27 14:30", includeTime: false, current: null).Kind);

    [Fact]
    public void should_seed_the_draft_with_today_when_a_date_field_has_no_value()
        => Assert.Equal(new DateTime(2026, 9, 27), DateTimeFieldText.SeedDraft(null, false, new DateTime(2026, 9, 27, 14, 30, 45)));

    [Fact]
    public void should_seed_the_draft_with_the_current_minute_when_a_time_field_has_no_value()
        => Assert.Equal(new DateTime(2026, 9, 27, 14, 30, 0), DateTimeFieldText.SeedDraft(null, true, new DateTime(2026, 9, 27, 14, 30, 45)));

    [Fact]
    public void should_seed_the_draft_with_the_value_when_the_field_has_one()
        => Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 0), DateTimeFieldText.SeedDraft(new DateTime(2026, 1, 2, 3, 4, 5), true, DateTime.MinValue));

    [Fact]
    public void should_keep_the_draft_time_when_a_calendar_day_is_picked_in_a_time_field()
    {
        var draft = new DateTime(2026, 9, 1, 18, 5, 0);

        Assert.Equal(new DateTime(2026, 9, 20, 18, 5, 0), DateTimeFieldText.WithDate(draft, new DateTime(2026, 9, 20), includeTime: true));
        Assert.Equal(new DateTime(2026, 9, 20), DateTimeFieldText.WithDate(draft, new DateTime(2026, 9, 20), includeTime: false));
    }

    [Fact]
    public void should_keep_the_draft_date_when_a_time_is_picked()
        => Assert.Equal(new DateTime(2026, 9, 1, 7, 15, 0),
                        DateTimeFieldText.WithTime(new DateTime(2026, 9, 1, 18, 5, 0), new DateTime(1999, 1, 1, 7, 15, 33)));
}

/// <summary>
/// <see cref="DateTimeField"/> 를 커널 템플릿으로 실제로 세워 본다 — 글자 칸 확정 · 되돌림 · 팝업 [적용]/[취소]/[비우기] · UIA.
/// </summary>
[Collection(WpfApplicationCollection.Name)]
public class DateTimeFieldControlTests
{
    [Fact]
    public void should_apply_the_kernel_template_with_its_parts_when_the_field_is_shown()
        => OnSta(() => WithField(field =>
        {
            Assert.NotNull(field.InputBox);
            Assert.IsType<Calendar>(field.Template.FindName("PART_Calendar", field));
            Assert.IsType<ToggleButton>(field.Template.FindName("PART_Trigger", field));
            Assert.IsType<TimePicker>(field.Template.FindName("PART_Time", field));
            // 달력은 범위 칸과 같은 스타일을 나눠 쓴다(베끼지 않는다)
            var calendar = (Calendar)field.Template.FindName("PART_Calendar", field);
            Assert.Same(KernelResource("Console.DateTimeRange.CalendarStyle"), calendar.Style);
            Assert.Equal(CalendarSelectionMode.SingleDate, calendar.SelectionMode);
            Assert.Equal("2026-09-20", field.InputBox!.Text);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_commit_the_value_when_the_text_becomes_a_complete_date()
        => OnSta(() => WithField(field =>
        {
            field.InputBox!.Text = "2026-09";
            Assert.Equal(new DateTime(2026, 9, 20), field.Value);   // 입력 도중 — 아직 확정하지 않는다
            Assert.True(field.IsInputInvalid);

            field.InputBox.Text = "2026-08-28";

            Assert.Equal(new DateTime(2026, 8, 28), field.Value);
            Assert.False(field.IsInputInvalid);
            Assert.Equal("2026-08-28", field.InputBox.Text);        // 자동화가 되읽는 글자 그대로
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_restore_the_last_value_when_unreadable_text_loses_focus()
        => OnSta(() => WithField(field =>
        {
            field.InputBox!.Text = "2026-99-99";
            Assert.True(field.IsInputInvalid);

            field.InputBox.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, field.InputBox, null) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });

            Assert.Equal("2026-09-20", field.InputBox.Text);
            Assert.Equal(new DateTime(2026, 9, 20), field.Value);
            Assert.False(field.IsInputInvalid);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_not_commit_an_empty_value_when_the_field_cannot_be_empty()
        => OnSta(() => WithField(field =>
        {
            field.InputBox!.Text = string.Empty;

            Assert.Equal(new DateTime(2026, 9, 20), field.Value);
            Assert.True(field.IsInputInvalid);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_commit_null_when_the_text_is_cleared_and_the_field_can_be_empty()
        => OnSta(() => WithField(field =>
        {
            field.AllowEmpty = true;
            field.InputBox!.Text = string.Empty;

            Assert.Null(field.Value);
            Assert.False(field.IsInputInvalid);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_rewrite_the_text_when_the_bound_value_changes()
        => OnSta(() => WithField(field =>
        {
            field.IncludeTime = true;
            field.Value = new DateTime(2026, 12, 31, 23, 59, 30);

            Assert.Equal("2026-12-31 23:59", field.InputBox!.Text);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_commit_the_picked_day_and_time_when_apply_is_pressed_in_the_popup()
        => OnSta(() => WithField(field =>
        {
            field.IncludeTime = true;
            field.IsDropDownOpen = true;
            var calendar = (Calendar)field.Template.FindName("PART_Calendar", field);
            var time = (TimePicker)field.Template.FindName("PART_Time", field);
            Assert.Equal(new DateTime(2026, 9, 20), calendar.SelectedDate);   // 지금 값으로 씨앗

            calendar.SelectedDate = new DateTime(2026, 9, 25);
            time.SelectedTime = new DateTime(2000, 1, 1, 7, 15, 0);
            Assert.Equal("2026-09-25 07:15", field.PreviewText);
            Click(field, "PART_Apply");

            Assert.Equal(new DateTime(2026, 9, 25, 7, 15, 0), field.Value);
            Assert.False(field.IsDropDownOpen);
            Assert.Equal("2026-09-25 07:15", field.InputBox!.Text);
        }, new DateTime(2026, 9, 20, 9, 0, 0)));

    [Fact]
    public void should_close_without_committing_when_cancel_is_pressed_in_the_popup()
        => OnSta(() => WithField(field =>
        {
            field.IsDropDownOpen = true;
            ((Calendar)field.Template.FindName("PART_Calendar", field)).SelectedDate = new DateTime(2026, 1, 1);

            Click(field, "PART_Cancel");

            Assert.Equal(new DateTime(2026, 9, 20), field.Value);
            Assert.False(field.IsDropDownOpen);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_start_a_fresh_draft_when_the_popup_is_opened_again_after_cancel()
        => OnSta(() => WithField(field =>
        {
            field.IsDropDownOpen = true;
            var calendar = (Calendar)field.Template.FindName("PART_Calendar", field);
            calendar.SelectedDate = new DateTime(2026, 1, 1);
            Click(field, "PART_Cancel");

            field.IsDropDownOpen = true;

            Assert.Equal(new DateTime(2026, 9, 20), calendar.SelectedDate);
            Assert.Equal("2026-09-20", field.PreviewText);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_show_and_honour_the_clear_button_only_when_the_field_can_be_empty()
        => OnSta(() => WithField(field =>
        {
            var clear = (Button)field.Template.FindName("PART_Clear", field);
            Assert.Equal(Visibility.Collapsed, clear.Visibility);

            field.AllowEmpty = true;
            Assert.Equal(Visibility.Visible, clear.Visibility);
            field.IsDropDownOpen = true;
            Click(field, "PART_Clear");

            Assert.Null(field.Value);
            Assert.False(field.IsDropDownOpen);
            Assert.Equal(string.Empty, field.InputBox!.Text);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_show_the_time_row_only_when_the_field_includes_time()
        => OnSta(() => WithField(field =>
        {
            var row = (FrameworkElement)field.Template.FindName("TimeRow", field);
            Assert.Equal(Visibility.Collapsed, row.Visibility);

            field.IncludeTime = true;

            Assert.Equal(Visibility.Visible, row.Visibility);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_expose_the_consumer_id_the_edit_box_and_both_patterns_when_automation_reads_the_field()
        => OnSta(() => WithField(field =>
        {
            AutomationProperties.SetAutomationId(field, "Reports.Create.StartDatePicker");
            var peer = Assert.IsType<DateTimeFieldAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(field));

            Assert.Equal("Reports.Create.StartDatePicker", peer.GetAutomationId());
            Assert.Equal(nameof(DateTimeField), peer.GetClassName());
            var edit = Assert.Single(peer.GetChildren()!, c => c.GetAutomationControlType() == AutomationControlType.Edit);
            Assert.Equal("Console.DateTimeField.Text", edit.GetAutomationId());

            var value = (IValueProvider)peer.GetPattern(PatternInterface.Value);
            Assert.Equal("2026-09-20", value.Value);
            value.SetValue("2026-10-01");
            Assert.Equal(new DateTime(2026, 10, 1), field.Value);
            Assert.Throws<ArgumentException>(() => value.SetValue("내일"));

            var expand = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);
            expand.Expand();
            Assert.True(field.IsDropDownOpen);
            Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
            expand.Collapse();
            Assert.False(field.IsDropDownOpen);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_open_the_popup_when_f4_is_pressed_in_the_text_box()
        => OnSta(() => WithField(field =>
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, new FakeSource(), 0, Key.F4) { RoutedEvent = Keyboard.PreviewKeyDownEvent };

            field.InputBox!.RaiseEvent(args);

            Assert.True(args.Handled);
            Assert.True(field.IsDropDownOpen);
        }, new DateTime(2026, 9, 20)));

    [Fact]
    public void should_leave_escape_to_the_outside_when_there_is_nothing_to_revert()
        => OnSta(() => WithField(field =>
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, new FakeSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };

            field.InputBox!.RaiseEvent(args);

            Assert.False(args.Handled);   // 목록 선택 해제 · 창 닫기 같은 바깥 Esc 를 가로채지 않는다
        }, new DateTime(2026, 9, 20)));

    // ── 템플릿 글자 계약 ─────────────────────────────────────────────

    [Fact]
    public void should_use_tokens_only_and_share_the_range_calendar_when_the_template_is_read()
    {
        var kernel = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Utils", "Themes", "Generic.xaml"));
        var start = kernel.IndexOf("<Style TargetType=\"{x:Type c:DateTimeField}\">", StringComparison.Ordinal);
        Assert.True(start > 0, "DateTimeField 스타일을 찾지 못했다");
        var section = kernel[start..kernel.IndexOf("</Style>\r\n\r\n", start, StringComparison.Ordinal)];

        Assert.DoesNotMatch(new Regex("\"#[0-9A-Fa-f]{3,8}\""), section);                 // 색은 토큰으로만
        Assert.Contains("{StaticResource Console.DateTimeRange.CalendarStyle}", section);  // 달력 판은 범위 칸 것
        Assert.Contains("<c:ConsoleText", section);                                        // 템플릿 글은 UIA 에 나오게
        foreach (var id in new[] { "Console.DateTimeField.Text", "Console.DateTimeField.Calendar", "Console.DateTimeField.Apply", "Console.DateTimeField.Cancel", "Console.DateTimeField.Clear", "Console.DateTimeField.Preview" })
            Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", section);
    }

    // ── 도우미 ──────────────────────────────────────────────────────

    private sealed class FakeSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = new System.Windows.Controls.Grid();
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void Click(DateTimeField field, string part)
        => ((ButtonBase)field.Template.FindName(part, field)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    /// <summary>
    /// 창 없이 세운다(다른 커널 시험과 같은 방식) — 이 컬렉션은 Application 을 먼저 만든 시험의 스레드가 이미 끝나 있어
    /// 창을 띄우면 그 앱의 종료 절차에 걸려 보이지 않는 창이 된다(실측: IsVisible=False). 판정은 템플릿 · 값 · 패턴만 본다.
    /// </summary>
    private static void WithField(Action<DateTimeField> body, DateTime value)
    {
        var field = new DateTimeField { Style = (Style)KernelResource(typeof(DateTimeField)), Value = value };
        try
        {
            field.ApplyTemplate();
            field.Measure(new Size(400, 60));
            field.Arrange(new Rect(0, 0, 400, 60));
            field.UpdateLayout();
            body(field);
        }
        finally
        {
            field.IsDropDownOpen = false;
        }
    }

    private static readonly object EnsureApplicationGate = new();

    /// <summary>스레드마다 한 벌 — 같은 시험 안에서 스타일 동일성(Same)을 볼 수 있게(시험마다 새 STA 스레드다).</summary>
    [ThreadStatic] private static ResourceDictionary? _kernel;

    private static object KernelResource(object key)
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }
        _kernel ??= (ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative));
        return _kernel[key];
    }

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static void OnSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

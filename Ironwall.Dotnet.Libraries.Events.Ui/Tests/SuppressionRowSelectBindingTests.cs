using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 억제 목록의 삭제 선택 칸(<c>Console.Suppression.RowSelect.{id}</c>)을 켜면 행 뷰모델의 <c>IsSelected</c> 가 곧바로 켜지는가.
/// </summary>
/// <remarks>
/// 2026-09-28 헤디드 SC-SUP-015 — 취소한 자기 행을 체크했는데 툴바 [삭제] 가 꺼진 채였다. DataGrid 는 첫 측정에서 행마다
/// BindingGroup 을 두고, 그 안에 든 <b>기본 트리거</b> 바인딩은 WPF 가 <c>Explicit</c> 로 바꾼다 — 읽기 전용 표라 행 편집이 끝날
/// 일도 없어서, 체크는 화면에만 켜지고 뷰모델에는 끝내 닿지 않았다(마우스로 눌러도 같다).
/// 이 시험은 실제 XAML 에서 그 칸을 떼어 같은 설정의 DataGrid(읽기 전용)에 넣고, UIA 토글로 켠다.
/// </remarks>
public class SuppressionRowSelectBindingTests
{
    private static string ListXamlPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Consoles", "SuppressionListView.xaml");

    /// <summary>행 선택 CheckBox 요소를 그대로 떼어 낸다(보임 여부 바인딩만 뺀다 — 뷰 로컬 변환기를 쓴다).</summary>
    private static string RowSelectCheckBox()
    {
        var xaml = File.ReadAllText(ListXamlPath());
        var at = xaml.IndexOf("StringFormat=Console.Suppression.RowSelect.{0}", StringComparison.Ordinal);
        Assert.True(at >= 0, "행 선택 칸을 XAML 에서 찾지 못했다");
        var open = xaml.LastIndexOf("<CheckBox", at, StringComparison.Ordinal);
        var close = xaml.IndexOf("/>", at, StringComparison.Ordinal);
        var element = xaml[open..(close + 2)];
        return Regex.Replace(element, @"\sVisibility=""\{[^""]*\}""", "");
    }

    private static EventSuppressionScheduleDto CancelledDto() => new()
    {
        Id = 309,
        Name = "LRT-UI-SUPW",
        Status = "cancelled",
        TargetType = "all",
        TargetSide = "both",
        EventScope = "all",
        TargetDeviceIds = new List<int>(),
        TargetGroupIds = new List<int>(),
        WindowStart = "2026-09-30T00:00:00.000+09:00",
        WindowEnd = "2026-10-14T00:00:00.000+09:00",
    };

    [Fact]
    public void should_select_the_row_view_model_when_its_delete_checkbox_is_toggled_in_the_grid()
    {
        // Arrange
        var checkBox = RowSelectCheckBox();
        var notified = 0;
        var row = new SuppressionConsoleRow(CancelledDto(), null, null, () => notified++);
        Assert.True(row.IsDeletable);

        // Act — 읽기 전용 DataGrid 에 띄워(첫 측정에서 행 BindingGroup 이 선다) UIA 토글로 켠다
        var (toggled, isChecked) = OnSta(() =>
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                ItemsSource = new[] { row },
            };
            grid.Columns.Add(new DataGridTemplateColumn
            {
                IsReadOnly = true,
                CellTemplate = (DataTemplate)XamlReader.Parse(
                    "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" + checkBox + "</DataTemplate>"),
            });
            var window = new Window
            {
                Content = grid, Width = 300, Height = 160, Left = -20000, Top = -20000,
                ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
            };
            window.Show();
            window.UpdateLayout();

            var box = Find<CheckBox>(grid, "Console.Suppression.RowSelect.309");
            Assert.NotNull(box);
            var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(box!)!.GetPattern(PatternInterface.Toggle);
            toggle.Toggle();
            window.UpdateLayout();
            var result = (toggle.ToggleState == ToggleState.On, box!.IsChecked == true);
            window.Close();
            return result;
        });

        // Assert — 화면만이 아니라 뷰모델이 켜지고, 콘솔에 알린다([삭제] 가 다시 읽는다)
        Assert.True(toggled && isChecked);
        Assert.True(row.IsSelected, "체크는 켜졌는데 행 뷰모델의 IsSelected 는 꺼져 있다(행 BindingGroup 이 갱신을 붙잡았다)");
        Assert.Equal(1, notified);
    }

    private static T? Find<T>(DependencyObject root, string automationId) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && AutomationProperties.GetAutomationId(typed) == automationId) return typed;
            if (Find<T>(child, automationId) is { } nested) return nested;
        }
        return null;
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
}

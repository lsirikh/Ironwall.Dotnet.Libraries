using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 상세의 부품 · 부품 상태 표(카탈로그 §6.8 ASM-209 · 210 · 211 · 214) — 제어기 · 카메라. 뷰모델 + 상세 폼 XAML 을 느슨하게 읽어
/// <b>창 없이</b> Measure/Arrange 한 그림(보이는 것 = 자신과 조상이 모두 Visible)을 센다. 부품 상태는 주입기가 보고한 값과 같은 모양을 모델에 싣는다.
/// </summary>
[Collection("CaliburnIoC")]
public class DetailComponentTableScenarioTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private const string SortId = "Devices.Detail.Components.Sort";

    private static DeviceAxesModel Axes(IEnumerable<ComponentDefinitionModel> parts, IDictionary<string, ComponentStatusModel> status)
    {
        var spec = new HardwareSpecModel();
        foreach (var part in parts) spec.Components.Add(part);
        var observed = new DeviceStatusModel();
        foreach (var (key, value) in status) observed.Components[key] = value;
        return new DeviceAxesModel
        {
            HardwareSpec = spec,
            DeviceStatus = observed,
            Meta = new ResponseMeta("full", new[] { "connection", "hardware_spec", "components", "device_status", "device_config" }),
        };
    }

    private static ControllerDeviceModel SeedController() => new()
    {
        Id = 21, DeviceNumber = 1, DeviceName = "LRT-UI-FCTRL-1", Status = EnumDeviceStatus.ACTIVATED, IpAddress = "127.0.0.1", Port = 9001,
        Axes = Axes(Array.Empty<ComponentDefinitionModel>(), new Dictionary<string, ComponentStatusModel>()),
    };

    private static CameraDeviceModel PtzCamera(string ptzHealth, string? ptzReason = null) => new()
    {
        Id = 41, DeviceNumber = 41, DeviceName = "북측 PTZ 카메라", Status = EnumDeviceStatus.ACTIVATED,
        Axes = Axes(new[]
            {
                new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT" },
                new ComponentDefinitionModel { Key = "ir", Type = "IR_ILLUMINATOR", Label = "적외선 조명" },
            },
            new Dictionary<string, ComponentStatusModel>
            {
                ["ptz"] = new() { State = "IDLE", Health = ptzHealth, FaultReason = ptzReason, ObservedAt = "2026-10-11T09:12:55.000000+09:00" },
                ["ir"] = new() { State = "ON", Health = "OK", ObservedAt = "2026-10-11T09:12:55.000000+09:00" },
            }),
    };

    private static ControllerDeviceModel ControllerWithParts() => new()
    {
        Id = 22, DeviceNumber = 2, DeviceName = "북측 제어기", Status = EnumDeviceStatus.ACTIVATED, IpAddress = "10.99.7.1", Port = 9001,
        Axes = Axes(new[]
            {
                new ComponentDefinitionModel { Key = "nic", Type = "NETWORK_INTERFACE", Label = "본선" },
                new ComponentDefinitionModel { Key = "heater", Type = "HEATER" },
                new ComponentDefinitionModel { Key = "fan", Type = "FAN" },
            },
            new Dictionary<string, ComponentStatusModel>
            {
                ["nic"] = new() { State = "UP", Health = "OK" },
                ["heater"] = new() { State = "OFF", Health = "FAULT", FaultReason = "OVER_CURRENT" },
                ["fan"] = new() { State = "ON", Health = "OK" },
            }),
    };

    private static object RowOf(IBaseDeviceModel model) => model switch
    {
        ControllerDeviceModel c => new ControllerDeviceViewModel(c),
        CameraDeviceModel c => new CameraDeviceViewModel(c),
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };

    private static DevicePropertyFormViewModel Form(IBaseDeviceModel model, EnumDeviceCategory category)
    {
        var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter());
        form.Load(new List<object> { RowOf(model) }, category, isAxisContract: true, isCreating: false, isReadOnly: false, isUnitEra: true);
        return form;
    }

    private sealed record SectionView(DevicePropertySection Section, bool HasTable, List<string> Texts, int SortButtons, int Dots, int HeaderCells);

    private static List<SectionView> Render(IBaseDeviceModel model, EnumDeviceCategory category)
        => OnSta(() =>
        {
            _ = Application.Current;
            var form = Form(model, category);
            var view = LooseFormView();
            view.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
            view.DataContext = form;
            Layout(view, 640, 2400);
            Layout(view, 640, 2400);
            var head = view.TryFindResource("Form.Component.Head");
            Assert.NotNull(head);
            return Descendants<ContentPresenter>(view)
                .Where(p => p.Content is PropertySectionViewModel)
                .Select(p =>
                {
                    var vm = (PropertySectionViewModel)p.Content;
                    var texts = Shown<TextBlock>(p, view).ToList();
                    return new SectionView(vm.Section, vm.StatusTable != null || vm.DeclarationTable != null,
                        texts.Select(t => t.Text).ToList(),
                        Shown<ToggleButton>(p, view).Count(t => AutomationProperties.GetAutomationId(t) == SortId),
                        Shown<System.Windows.Shapes.Ellipse>(p, view).Count(),
                        texts.Count(t => ReferenceEquals(t.Style, head)));
                })
                .ToList();
        });

    #region - ASM-209 · 210 · 211 · 214 -
    [Fact]
    [Trait("Scenario", "ASM-209")]
    public void should_show_no_parts_without_a_sort_chip_or_header_row_for_a_seed_controller_without_parts_ASM209()
    {
        var sections = Render(SeedController(), EnumDeviceCategory.Controller);

        var status = sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);
        var declared = sections.Single(s => s.Section == DevicePropertySection.Components);
        Assert.Contains("부품 없음", status.Texts);
        Assert.Contains("부품 없음", declared.Texts);
        Assert.Equal(0, sections.Sum(s => s.SortButtons));                                // `Devices.Detail.Components.Sort` 없음
        Assert.Equal(0, sections.Sum(s => s.HeaderCells));                                 // 머리줄 없음
        Assert.Equal(0, status.Dots);
        Assert.DoesNotContain("마지막 변화", status.Texts);
    }

    [Fact]
    [Trait("Scenario", "ASM-210")]
    public void should_draw_one_header_row_per_table_and_one_sort_chip_for_a_ptz_and_ir_camera_ASM210()
    {
        var sections = Render(PtzCamera("OK"), EnumDeviceCategory.Camera);

        var status = sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);
        var declared = sections.Single(s => s.Section == DevicePropertySection.Components);
        Assert.Equal(5, status.HeaderCells);                                               // 부품 · 상태 · 건강 · 사유 · 마지막 변화 — 머리줄 하나
        Assert.Single(status.Texts, t => t == "마지막 변화");
        Assert.Equal(5, declared.HeaderCells);                                             // 부품 · 종류 · 채널 · 위치 · 사용
        Assert.Equal(1, status.SortButtons);
        Assert.Equal(1, sections.Sum(s => s.SortButtons));                                 // 폼 전체에 정렬 칩 하나
        Assert.All(sections.Where(s => !s.HasTable), s => Assert.Equal(0, s.HeaderCells + s.SortButtons));
        Assert.Contains("적외선 조명", status.Texts);
    }

    [Fact]
    [Trait("Scenario", "ASM-211")]
    public void should_summarize_one_ptz_fault_in_korean_when_the_injector_reports_it_ASM211()
    {
        var model = PtzCamera("FAULT", "OVER_CURRENT");                                    // 주입기 PTZ FAULT(사유 과전류)
        var table = Form(model, EnumDeviceCategory.Camera).Sections.Single(s => s.Section == DevicePropertySection.DeviceStatus).StatusTable!;
        var sections = Render(model, EnumDeviceCategory.Camera);
        var shown = sections.Single(s => s.Section == DevicePropertySection.DeviceStatus).Texts;

        Assert.Equal("고장 1 · 정상 1", table.SummaryText);
        Assert.Equal(ComponentHealthKind.Crit, table.SummaryKind);
        var ptz = table.Rows.Single(r => r.Key == "ptz");
        Assert.Equal(("고장", "과전류"), (ptz.HealthText, ptz.FaultText));
        Assert.Contains("고장 1 · 정상 1", shown);
        Assert.DoesNotContain(shown, t => t.Contains("FAULT") || t.Contains("OVER_CURRENT"));   // 영문 코드가 화면에 없다
        table.IsFaultFirst = true;
        Assert.Equal("ptz", table.Rows.First().Key);
    }

    [Fact]
    [Trait("Scenario", "ASM-214")]
    public void should_summarize_and_list_the_component_states_of_a_controller_ASM214()
    {
        var model = ControllerWithParts();
        var form = Form(model, EnumDeviceCategory.Controller);
        var table = form.Sections.Single(s => s.Section == DevicePropertySection.DeviceStatus).StatusTable!;
        var sections = Render(model, EnumDeviceCategory.Controller);
        var status = sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);

        Assert.Equal("고장 1 · 정상 2", table.SummaryText);                                // 제어기 요약
        Assert.Equal(new[] { "nic", "heater", "fan" }, table.Rows.Select(r => r.Key));      // 줄 = 선언 순서
        Assert.Equal("본선", table.Rows.First().Name);
        Assert.Equal(("고장", "과전류"), (table.Rows[1].HealthText, table.Rows[1].FaultText));
        Assert.Contains("고장 1 · 정상 2", status.Texts);
        Assert.Contains("본선", status.Texts);
        Assert.Equal(1, status.SortButtons);
        Assert.Equal(5, status.HeaderCells);
        Assert.DoesNotContain(form.Fields, f => f.Key == "device_status");                 // 같은 내용의 글 칸은 표로 대신한다
    }
    #endregion

    #region - helpers -
    private static UserControl LooseFormView()
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = System.Text.RegularExpressions.Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"detail-r3-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Forms", "DevicePropertyFormView.xaml"));
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\s(IsVisibleChanged|Click|PreviewKeyDown|KeyDown|SelectionChanged|LostKeyboardFocus)=""[^""]*""", string.Empty);
        xaml = xaml.Replace("xmlns:md=\"http://materialdesigninxaml.net/winfx/xaml/themes\"", "xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\"");
        xaml = xaml.Replace("<UserControl.Resources>",
            $"<UserControl.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"{new Uri(stylesPath).AbsoluteUri}\" /></ResourceDictionary.MergedDictionaries>");
        xaml = xaml.Replace("</UserControl.Resources>", "</ResourceDictionary></UserControl.Resources>");
        try { return (UserControl)XamlReader.Parse(xaml); }
        finally { try { File.Delete(stylesPath); } catch (IOException) { } }
    }

    private static ResourceDictionary Theme(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>창이 없으면 IsVisible 은 늘 false 다 — 자신과 조상(<paramref name="root"/> 까지)의 Visibility 로 '그려질 것'을 가른다.</summary>
    private static IEnumerable<T> Shown<T>(DependencyObject scope, DependencyObject root) where T : UIElement
        => Descendants<T>(scope).Where(e => IsShown(e, root));

    private static bool IsShown(DependencyObject element, DependencyObject root)
    {
        for (var d = element; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
            if (ReferenceEquals(d, root)) return true;
        }
        return true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion
}

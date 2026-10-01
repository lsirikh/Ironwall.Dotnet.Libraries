using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
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
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 콘솔 상세 폼(<c>DevicePropertyFormView.xaml</c>)의 부품 표 자리 — 표가 없는 절(장비 공통 등)에서는 아무것도 그리지 않고,
/// 부품이 하나도 없으면 요약 한 줄("부품 없음")만 그린다. 실제 XAML 을 느슨하게 읽어 화면 밖 창에 띄우고 보이는 요소를 센다.
/// </summary>
/// <remarks>
/// 결함(2026-10-01 사용자 보고): 절마다 <c>ContentControl Content="{Binding DeclarationTable}" ContentTemplate="…"</c> 이 있었는데,
/// ContentTemplate 은 Content 가 null 이어도 세워지고 DataContext 가 절 뷰모델로 이어져 — 모든 절에 빈 머리줄 두 벌
/// ("부품 종류 채널 위치 사용" · "부품 상태 건강 사유 마지막 변화") · 외톨이 회색 점 · 글 없는 정렬 단추가 그려졌다.
/// </remarks>
[Collection("CaliburnIoC")]
public class ComponentTableHostViewTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private const string SortId = "Devices.Detail.Components.Sort";

    private static LampDeviceModel Lamp(bool withComponents)
    {
        var spec = new HardwareSpecModel();
        var status = new DeviceStatusModel();
        if (withComponents)
        {
            spec.Components.Add(new ComponentDefinitionModel { Key = "lamp", Type = "LAMP_LIGHT", Channel = 1 });
            spec.Components.Add(new ComponentDefinitionModel { Key = "buzzer1", Type = "BUZZER" });
            status.Components["lamp"] = new ComponentStatusModel { State = "OFF", Health = "OK" };
            status.Components["buzzer1"] = new ComponentStatusModel { State = "ON", Health = "FAULT", FaultReason = "OVER_CURRENT" };
        }
        return new LampDeviceModel
        {
            Id = 11, DeviceNumber = 11, DeviceName = "경광등 11", Status = EnumDeviceStatus.ACTIVATED,
            Axes = new DeviceAxesModel
            {
                HardwareSpec = spec,
                DeviceStatus = status,
                Meta = new ResponseMeta("full", new[] { "connection", "hardware_spec", "components", "device_status", "device_config" }),
            },
        };
    }

    private sealed record SectionView(DevicePropertySection Section, bool HasTable, List<string> Texts, int SortButtons, int Dots, int HeaderCells);

    private static List<SectionView> Render(LampDeviceModel model)
        => OnSta(() =>
        {
            _ = Application.Current;
            var form = new DevicePropertyFormViewModel(new ConsoleDetailPresenter());
            form.Load(new List<object> { new LampDeviceViewModel(model) }, EnumDeviceCategory.Lamp,
                isAxisContract: true, isCreating: false, isReadOnly: false, isUnitEra: true);
            var view = LooseFormView();
            view.DataContext = form;
            var window = new Window
            {
                Content = new ScrollViewer { Content = view }, Width = 640, Height = 900, WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false,
            };
            window.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
            window.Show();
            Pump();
            try
            {
                var head = view.TryFindResource("Form.Component.Head");   // 표 머리줄 칸의 스타일 — 칸 이름("채널" 등)은 다른 절의 칸 머리와 겹친다
                Assert.NotNull(head);
                return Descendants<ContentPresenter>(view)
                    .Where(p => p.Content is PropertySectionViewModel)
                    .Select(p =>
                    {
                        var vm = (PropertySectionViewModel)p.Content;
                        return new SectionView(vm.Section, vm.StatusTable != null || vm.DeclarationTable != null,
                            Shown<TextBlock>(p).Select(t => t.Text).ToList(),
                            Shown<ToggleButton>(p).Count(t => AutomationProperties.GetAutomationId(t) == SortId),
                            Shown<System.Windows.Shapes.Ellipse>(p).Count(),
                            Shown<TextBlock>(p).Count(t => ReferenceEquals(t.Style, head)));
                    })
                    .ToList();
            }
            finally { window.Close(); }
        });

    [Fact]
    public void should_draw_no_component_table_visuals_when_section_has_no_table()
    {
        var sections = Render(Lamp(withComponents: true));

        Assert.Contains(sections, s => !s.HasTable);                                              // 장비 공통 등
        Assert.All(sections.Where(s => !s.HasTable), s =>
        {
            Assert.Equal(0, s.HeaderCells);                                                       // 빈 머리줄 없음
            Assert.Equal(0, s.SortButtons);                                                       // 글 없는 정렬 단추 없음
            Assert.Equal(0, s.Dots);                                                              // 외톨이 회색 점 없음
        });
    }

    [Fact]
    public void should_draw_each_table_once_in_its_own_section_when_device_has_components()
    {
        var sections = Render(Lamp(withComponents: true));

        var status = sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);
        Assert.Equal(1, status.SortButtons);
        Assert.Single(status.Texts, t => t == "마지막 변화");
        Assert.Contains("고장 1 · 정상 1", status.Texts);
        Assert.Equal(5, status.HeaderCells);                                                      // 부품 · 상태 · 건강 · 사유 · 마지막 변화
        var declared = sections.Single(s => s.Section == DevicePropertySection.Components);
        Assert.Equal(5, declared.HeaderCells);                                                    // 부품 · 종류 · 채널 · 위치 · 사용
        Assert.Equal(0, declared.SortButtons);
        Assert.Equal(1, sections.Sum(s => s.SortButtons));                                        // 폼 전체에 정렬 단추는 하나
    }

    [Fact]
    public void should_show_one_line_empty_state_without_header_dot_or_sort_when_device_has_no_components()
    {
        var sections = Render(Lamp(withComponents: false));

        var status = sections.Single(s => s.Section == DevicePropertySection.DeviceStatus);
        Assert.True(status.HasTable);
        Assert.Contains("부품 없음", status.Texts);
        Assert.DoesNotContain("마지막 변화", status.Texts);
        Assert.Equal(0, status.SortButtons);
        Assert.Equal(0, status.Dots);
        var declared = sections.Single(s => s.Section == DevicePropertySection.Components);
        Assert.Equal(0, status.HeaderCells);
        Assert.Contains("부품 없음", declared.Texts);
        Assert.Equal(0, declared.HeaderCells);
    }

    #region - helpers -
    /// <summary>
    /// 폼 XAML 을 느슨하게 읽는다(x:Class · 이벤트 빼고, 앱 스타일 <c>Console.*</c> 은 테마 파일을 병합) — 컴파일된 뷰는 Application 의
    /// StaticResource(Console.Chip 등)를 부르는데 시험 안에서 Application 을 만들면 다른 시험의 디스패처 경로가 바뀐다(WiringFenceEditorViewTests 와 같은 방법).
    /// </summary>
    private static UserControl LooseFormView()
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = System.Text.RegularExpressions.Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"form-loose-styles-{Guid.NewGuid():N}.xaml");
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

    /// <summary>실제로 그려지는 것만 — 자신과 조상(<paramref name="root"/> 까지)이 모두 Visible.</summary>
    private static IEnumerable<T> Shown<T>(DependencyObject root) where T : UIElement
        => Descendants<T>(root).Where(e => e.IsVisible);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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

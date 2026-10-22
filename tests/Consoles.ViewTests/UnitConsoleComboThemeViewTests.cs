using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 부대 콘솔 <b>실제 뷰</b> — 상세 판의 세 콤보(제대 · 상위 · 인접)가 앱의 ComboBox 기본 모양(MaterialDesign · 테마 토큰)을 받아야 한다.
/// </summary>
/// <remarks>
/// 2026-10-22 사용자 보고("여기 색깔 뭐야?") — 콘솔 지역 스타일 <c>Unit.Combo</c> 에 BasedOn 이 없어 앱의 암묵 ComboBox 스타일이 빠지고
/// 시스템 기본(Aero2 — 흰 그라데이션 바탕 · 검은 글자) 템플릿이 들어갔다. 다크 테마에서 흰 칸으로 튀었다.
/// </remarks>
public class UnitConsoleComboThemeViewTests
{
    [Fact]
    public void should_use_the_app_default_combo_template_when_detail_combos_render_in_dark_theme() => AppHost.Run(() =>
    {
        AppHost.SetDark(true);
        var (view, window, console) = HostConsole();
        try
        {
            RailProbe.Wait(console.SelectRowAsync(console.Rows.First(r => r.Id == 6)));
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            var appStyle = Application.Current.TryFindResource(typeof(ComboBox)) as Style;
            Assert.NotNull(appStyle);
            var appTemplate = EffectiveTemplate(appStyle!);
            Assert.NotNull(appTemplate);

            foreach (var id in new[] { "Units.Detail.EchelonCombo", "Units.Detail.ParentCombo", "Units.Detail.AdjacencyCombo" })
            {
                var combo = Find<ComboBox>(view, x => System.Windows.Automation.AutomationProperties.GetAutomationId(x) == id);
                Assert.True(combo is not null, $"{id} 이 화면에 없다");
                Assert.True(ReferenceEquals(appTemplate, combo!.Template), $"{id} 이 앱 기본 ComboBox 템플릿이 아니다(시스템 기본 모양으로 그려짐)");
                Assert.False(combo.Background is LinearGradientBrush, $"{id} 바탕이 시스템 기본 흰 그라데이션이다");
            }
        }
        finally { window.Close(); AppHost.SetDark(false); }
    });

    private static ControlTemplate? EffectiveTemplate(Style? style)
    {
        for (; style is not null; style = style.BasedOn)
        {
            var setter = style.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.TemplateProperty);
            if (setter?.Value is ControlTemplate template) return template;
        }
        return null;
    }

    internal static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && match(t)) return t;
            var found = Find(child, match);
            if (found is not null) return found;
        }
        return null;
    }

    private static (UnitConsoleView View, Window Window, UnitConsoleViewModel Console) HostConsole()
    {
        var events = new EventAggregator();
        RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);

        var graph = Sample();
        var units = new Mock<IUnitGraphApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        units.SetupGet(u => u.IsAvailable).Returns(true);
        units.Setup(u => u.GetGraphAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => ApiResponse<UnitGraphDto>.CreateSuccess(graph));
        units.Setup(u => u.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((int id, CancellationToken _) =>
             {
                 var node = graph.Nodes.First(n => n.Id == id);
                 return ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
                 {
                     Id = id, Code = node.Code, Name = node.Name, EchelonRaw = node.EchelonRaw, ParentId = node.ParentId,
                     IsEnable = true, AdjacentUnitIds = new List<int>(),
                 });
             });
        var devices = new Mock<IUnitDeviceApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        devices.SetupGet(d => d.IsAvailable).Returns(true);
        devices.Setup(d => d.LoadAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new UnitDeviceLoadResult(Array.Empty<UnitDeviceItem>(), Array.Empty<string>()));

        var console = new UnitConsoleViewModel(units.Object, devices.Object, log: null, myUnitCode: () => "c06",
                                               canEdit: () => true, canDelete: () => true, events: events);
        RailProbe.Wait(((IActivate)console).ActivateAsync());

        var view = new UnitConsoleView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }

    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId };

    private static UnitGraphDto Sample() => new()
    {
        Nodes = new List<UnitListDto>
        {
            Node(1, "d01", "Division"),
            Node(2, "r01", "Regiment", 1),
            Node(3, "b01", "Battalion", 2),
            Node(5, "c05", "Company", 3),
            Node(6, "c06", "Company", 3),
        },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new List<List<int>> { new() { 1, 2 }, new() { 2, 3 }, new() { 3, 5 }, new() { 3, 6 } },
            Adjacency = new List<List<int>>(),
        },
    };
}

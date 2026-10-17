using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-36 — 관계도에서 <c>Enter</c> = 상세 첫 칸으로 포커스. 뷰모델은 <see cref="UnitMapFocusTarget.DetailFirstField"/> 를 올리는데
/// 받는 쪽이 캔버스뿐이었고 캔버스는 <c>Canvas</c> 요청만 처리했다 — 실창 8회차 SIM-K031 에서 포커스가 노드에 그대로 남았다.
/// 콘솔 뷰가 <see cref="UnitDetailFocusBridge"/> 로 그 요청을 상세 칸에 잇는다.
/// </summary>
public class UnitDetailFocusBridgeTests
{
    [Fact]
    public async Task should_ask_the_view_to_focus_the_detail_when_enter_is_pressed_on_a_selected_map_node()
    {
        var kit = await MapKit.OpenAsync();
        var asked = 0;
        var bridge = new UnitDetailFocusBridge(() => asked++);
        bridge.Bind(kit.Vm);

        kit.Vm.RequestSelect(kit.F.IdOf("7중대"));
        kit.Vm.HandleKey(UnitMapKeyCommand.Enter, shift: false);

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task should_stop_listening_to_the_old_map_when_the_bridge_is_bound_to_another()
    {
        var first = await MapKit.OpenAsync();
        var second = await MapKit.OpenAsync();
        var asked = 0;
        var bridge = new UnitDetailFocusBridge(() => asked++);
        bridge.Bind(first.Vm);
        bridge.Bind(second.Vm);

        first.Vm.RequestSelect(first.F.IdOf("7중대"));
        first.Vm.HandleKey(UnitMapKeyCommand.Enter, shift: false);

        Assert.Equal(0, asked);
    }

    [Fact]
    public void should_pick_the_name_box_when_the_code_field_is_collapsed_for_an_existing_unit()
    {
        var picked = H.OnSta(() =>
        {
            var root = Detail(codeVisible: false);
            return AutomationProperties.GetAutomationId(UnitDetailFocusBridge.FindFirstField(root)!);
        });

        Assert.Equal("Units.Detail.NameBox", picked);
    }

    [Fact]
    public void should_pick_the_code_field_first_when_a_new_unit_is_being_created()
    {
        var picked = H.OnSta(() =>
        {
            var root = Detail(codeVisible: true);
            return AutomationProperties.GetAutomationId(UnitDetailFocusBridge.FindFirstField(root)!);
        });

        Assert.Equal("Units.Detail.CodeField", picked);
    }

    [Fact]
    public void should_find_no_field_when_the_detail_form_is_collapsed()
    {
        var found = H.OnSta(() =>
        {
            var root = Detail(codeVisible: false);
            ((UIElement)root.Children[0]).Visibility = Visibility.Collapsed;
            return UnitDetailFocusBridge.FindFirstField(root) is not null;
        });

        Assert.False(found);
    }

    /// <summary>다리를 만들어 놓고 콘솔 뷰가 묶지 않으면 기능만 죽는다(분기 결선 검증) — 뷰가 관계도에 묶고 상세 첫 칸으로 옮기는가.</summary>
    [Fact]
    public void should_bind_the_bridge_to_the_console_map_when_the_unit_console_view_is_wired()
    {
        var codeBehind = File.ReadAllText(Path.Combine(ViewFolder(), "UnitConsoleView.xaml.cs"));

        Assert.Contains("new UnitDetailFocusBridge(", codeBehind);
        Assert.Contains("UnitDetailFocusBridge.FocusFirstField(this)", codeBehind);
        Assert.Contains("_detailFocus.Bind(Vm?.Map)", codeBehind);
    }

    private static string ViewFolder([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles", "Units"));

    /// <summary>상세 칸 모양 — 폼 묶음(StackPanel) 안에 코드 칸 · 이름 칸.</summary>
    private static StackPanel Detail(bool codeVisible)
    {
        var code = new TextBox { Visibility = codeVisible ? Visibility.Visible : Visibility.Collapsed };
        AutomationProperties.SetAutomationId(code, "Units.Detail.CodeField");
        var name = new TextBox();
        AutomationProperties.SetAutomationId(name, "Units.Detail.NameBox");
        var form = new StackPanel();
        form.Children.Add(code);
        form.Children.Add(name);
        var root = new StackPanel();
        root.Children.Add(form);
        return root;
    }
}

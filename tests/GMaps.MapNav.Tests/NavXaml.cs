using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

namespace GMaps.MapNav.Tests;

/// <summary>
/// MapView.xaml 의 <b>실제 조각</b>(도구줄 · 기울임 팝업 몸 · 배지 등)을 그대로 떼어 창 없이 띄우는 도우미.
/// MapView 전체는 MaterialDesign 앱 자원 · Caliburn 바인딩이 필요해 창 없이 못 띄우므로, 조각만 떼고
/// 조각이 쓰는 자원은 실제 사전(DesignTokens.xaml)을 병합하고 앱 수준 MaterialDesign 키 몇 개만 빈 스타일로 둔다.
/// 바인딩 · 명령 · AutomationId · ToolTip 은 XAML 원문 그대로다.
/// </summary>
internal static class NavXaml
{
    public static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string Help = "clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils";

    /// <summary>AutomationId 로 MapView.xaml 원소를 찾는다(하나여야 한다).</summary>
    public static XElement ById(string automationId)
        => NavKit.MapViewXaml().Descendants().Single(e => AutomationIdOf(e) == automationId);

    public static string? AutomationIdOf(XElement e)
        => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value;

    /// <summary>
    /// 조각을 감싸 파싱한다 — 감싼 Grid 에 DesignTokens 병합 + 앱 자원 대역(MaterialDesign 키 · BooleanToVisibilityConverter).
    /// <paramref name="stripPopups"/> 면 조각 안의 Popup 을 지운다(열리면 HWND 를 만든다 — 창 금지).
    /// "?" 는 말풍선 창 없는 템플릿(몸 HelpCallout 만 안에 둔다)으로 바꿔 열려도 창이 생기지 않는다.
    /// </summary>
    public static Grid Load(XElement fragment, object dataContext, double width, double height, bool stripPopups = true)
    {
        var copy = new XElement(fragment);
        if (stripPopups) foreach (var p in copy.DescendantsAndSelf().Where(e => e.Name.LocalName == "Popup").ToList()) p.Remove();

        var wrapper = XElement.Parse(
            "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            $"xmlns:help='{Help}'>" +
            "<Grid.Resources><ResourceDictionary>" +
            "<ResourceDictionary.MergedDictionaries><ResourceDictionary Source='/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/DesignTokens.xaml'/></ResourceDictionary.MergedDictionaries>" +
            "<BooleanToVisibilityConverter x:Key='BooleanToVisibilityConverter'/>" +
            "<Style x:Key='MaterialDesignToolBar' TargetType='ToolBar'/>" +
            "<Style x:Key='CompactComboBox' TargetType='ComboBox'/>" +
            "<Style x:Key='MaterialDesignSwitchToggleButton' TargetType='ToggleButton'/>" +
            "<Style TargetType='help:HelpTip'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='help:HelpTip'>" +
            "<Grid><Border Width='16' Height='16' Background='Transparent'/><help:HelpCallout x:Name='PART_Callout' Visibility='Collapsed'/></Grid>" +
            "</ControlTemplate></Setter.Value></Setter></Style>" +
            "</ResourceDictionary></Grid.Resources></Grid>");
        wrapper.Add(copy);

        var root = (Grid)XamlReader.Parse(wrapper.ToString(SaveOptions.DisableFormatting));
        root.DataContext = dataContext;
        ScenarioShared.Scenario.Layout(root, width, height);
        return root;
    }

    /// <summary>말풍선 창 없는 "?" 템플릿(몸만 안에) — MapZoomControl 처럼 템플릿 안에 "?" 가 있는 컨트롤의 자원에 넣는다.</summary>
    public static Style NoPopupHelpTipStyle()
        => (Style)XamlReader.Parse(
            $"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:help='{Help}' TargetType='help:HelpTip'>" +
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='help:HelpTip'>" +
            "<Grid><Border Width='16' Height='16' Background='Transparent'/><help:HelpCallout x:Name='PART_Callout' Visibility='Collapsed'/></Grid>" +
            "</ControlTemplate></Setter.Value></Setter></Style>");

    /// <summary>시각 트리(없으면 논리 트리)에서 AutomationId 로 찾는다.</summary>
    public static T Find<T>(DependencyObject root, string automationId) where T : DependencyObject
        => All<T>(root).FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == automationId)
           ?? throw new InvalidOperationException($"'{automationId}' ({typeof(T).Name}) 없음");

    /// <summary>시각 + 논리 트리 전체(중복 제거) — 넘친 도구줄 항목은 시각 트리에서 빠지고 논리 트리에만 남는다.</summary>
    public static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        var seen = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node)) continue;
            if (node is T hit) yield return hit;
            if (node is Visual or System.Windows.Media.Media3D.Visual3D)
                for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(node, i));
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) stack.Push(child);
        }
    }

    /// <summary>사용자 클릭과 같은 경로(ButtonBase.OnClick — 토글 + Click + Command). UIA Toggle 패턴은 명령을 부르지 않는다.</summary>
    public static void Click(ButtonBase button)
        => typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);

    /// <summary>HWND 없는 입력 원천 — "?" 가 Esc 를 들을 뿌리를 준다(창을 만들지 않는다).</summary>
    internal sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    public static bool PressEscape(PresentationSource source, UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }
}

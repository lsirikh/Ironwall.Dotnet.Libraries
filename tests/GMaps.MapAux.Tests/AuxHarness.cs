using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>HWND 없는 입력 원천 — 뿌리를 걸면 <see cref="PresentationSource.FromVisual"/> 가 이것을 돌려준다(창 없음).</summary>
internal sealed class AuxHeadlessSource : PresentationSource
{
    private Visual? _root;
    public override Visual RootVisual
    {
        get => _root!;
        set { var old = _root; _root = value; RootChanged(old!, value); }
    }
    public override bool IsDisposed => false;
    protected override CompositionTarget GetCompositionTargetCore() => null!;

    /// <summary>키 이벤트를 대상에 올린다(터널 PreviewKeyDown 또는 버블 KeyDown). 반환 = Handled.</summary>
    internal bool Press(UIElement target, Key key, bool preview = true)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, this, Environment.TickCount, key) { RoutedEvent = preview ? Keyboard.PreviewKeyDownEvent : Keyboard.KeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }
}

/// <summary>
/// 창 없는 "?" 템플릿 — 말풍선 창(Popup) 대신 말풍선 몸(<c>PART_Callout</c>)을 그 자리에 둔다.
/// 열림 판정 · 몸 AutomationId · 몸 내용(Entry)은 그대로 단언할 수 있고, 화면에는 아무 창도 뜨지 않는다(HelpTipTests 의 NoPopupStyle 확장).
/// </summary>
internal static class NoPopupHelp
{
    private const string Xaml =
        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
        "xmlns:c='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils' TargetType='c:HelpTip'>" +
        "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='c:HelpTip'>" +
        "<StackPanel><Border Width='20' Height='20' Background='Transparent'/><c:HelpCallout x:Name='PART_Callout' Visibility='Collapsed'/></StackPanel>" +
        "</ControlTemplate></Setter.Value></Setter></Style>";

    internal static Style Create() => (Style)XamlReader.Parse(Xaml);

    /// <summary>요소 자원에 암시 스타일로 넣는다 — 그 아래 "?" 는 모두 창 없는 템플릿을 쓴다.</summary>
    internal static void Install(FrameworkElement scope) => scope.Resources[typeof(Ironwall.Dotnet.Libraries.Utils.Consoles.HelpTip)] = Create();
}

/// <summary>
/// 지도 우클릭 메뉴를 <b>창 없이</b> 통째로 잡는다.
/// <para><c>MapViewModel.ShowMarkerContextMenu</c> 는 지역 <see cref="ContextMenu"/> 를 만들어 끝에서 <c>IsOpen = true</c>(팝업 창) 한다.
/// <c>IsOpen = true</c> 는 <c>ContextMenu.HookupParentPopup → Popup.CreateRootPopupInternal</c> 로 가는데, 거기서 <c>popup.Child = menu</c> 가
/// 팝업의 <c>IsOpen</c> 바인딩보다 <b>먼저</b>다. Child 가 되는 순간 메뉴가 처음 초기화되며(OnInitialized) 암시 스타일을 찾는다 →
/// 시험 동안 Application 자원에 넣어 둔 ContextMenu 암시 스타일(이 클래스의 부착 속성 Setter)의 변경 콜백이 돌고,
/// 거기서 메뉴를 붙잡은 뒤 표지 예외를 던진다 → 팝업 IsOpen 바인딩 · 창 생성 전에 끊기고 메서드의 try/catch 가 받는다.
/// 메뉴 항목은 이미 모두 만들어진 상태다.</para>
/// <para>안전 장치: ① WPF 의 순서(set_Child 가 IsOpen 바인딩보다 앞)를 IL 로 먼저 확인 ② 본 호출 전에 열지 않는 경로(<c>new Popup().Child = 메뉴</c>)로
/// 덫이 도는지 자가 확인 ③ 끝나고 메뉴의 팝업이 열리지 않았고 새 HwndSource 가 생기지 않았음을 단언.</para>
/// </summary>
internal static class MenuTrap
{
    public static readonly DependencyProperty ProbeProperty =
        DependencyProperty.RegisterAttached("Probe", typeof(int), typeof(MenuTrap), new PropertyMetadata(0, OnProbe));

    [ThreadStatic] private static ContextMenu? _menu;
    [ThreadStatic] private static bool _armed;

    /// <summary>덫 표지 — 이 예외만 메뉴 열기를 끊는다.</summary>
    internal sealed class Caught : Exception { public Caught() : base("MenuTrap: 메뉴를 열기 전에 잡았다") { } }

    /// <summary>
    /// 1단계(값 1): 메뉴가 첫 항목을 받으며 처음 초기화될 때 — 메뉴를 기록하고 앱 자원의 스타일을 <b>다른 인스턴스</b>(값 2)로 바꿔 둔다
    /// (창 밖 요소라 자원 교체가 곧바로 퍼지지 않는다). 2단계(값 2): 팝업의 Child 가 되며 조상이 바뀌어 암시 스타일을 다시 찾을 때 — 여기서 끊는다.
    /// </summary>
    private static void OnProbe(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!_armed || d is not ContextMenu menu) return;
        if (e.NewValue is 1)
        {
            _menu = menu;
            Application.Current!.Resources[typeof(ContextMenu)] = StyleWith(2);
            return;
        }
        if (e.NewValue is 2 && ReferenceEquals(menu, _menu)) throw new Caught();   // popup.Child 설정 중 — IsOpen 바인딩 전
    }

    private static Style StyleWith(int value)
    {
        var style = new Style(typeof(ContextMenu));
        style.Setters.Add(new Setter(ProbeProperty, value));
        return style;
    }

    private static bool _ilChecked;

    /// <summary>Popup.CreateRootPopupInternal 에서 Child 설정이 IsOpen 바인딩(IsOpenProperty 읽기)보다 앞인지 IL 순서로 확인.</summary>
    private static void AssertPopupGuardOrder()
    {
        if (_ilChecked) return;
        var m = typeof(Popup).GetMethod("CreateRootPopupInternal", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Popup.CreateRootPopupInternal 없음 — 메뉴를 창 없이 잡을 수 없다");
        var il = m.GetMethodBody()!.GetILAsByteArray()!;
        var seq = new List<string>();
        for (int i = 0; i + 4 < il.Length; i++)
        {
            int tok = BitConverter.ToInt32(il, i + 1);
            if (il[i] == 0x28 || il[i] == 0x6F)
                try { var mb = m.Module.ResolveMethod(tok); seq.Add($"{mb!.DeclaringType!.Name}.{mb.Name}"); } catch { }
            else if (il[i] == 0x7E)
                try { var f = m.Module.ResolveField(tok); seq.Add($"{f!.DeclaringType!.Name}.{f.Name}"); } catch { }
        }
        int child = seq.IndexOf("Popup.set_Child");
        int isOpen = seq.FindIndex(s => s.EndsWith(".IsOpenProperty"));
        Assert.True(child >= 0 && (isOpen < 0 || isOpen > child) && !seq.Take(child).Contains("Popup.set_IsOpen"),
            $"WPF 팝업 생성 순서가 예상과 다르다 — 메뉴를 열지 않고 잡을 수 없다: {string.Join(",", seq.Take(16))}");
        _ilChecked = true;
    }

    private static int HwndSourceCount() => PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().Count();

    /// <summary><paramref name="showMenu"/>(= ShowMarkerContextMenu 호출)가 만든 메뉴를 열지 않고 돌려준다. OneSta 스레드에서.</summary>
    internal static ContextMenu Capture(System.Action showMenu)
    {
        AssertPopupGuardOrder();
        IDisposable? app = Application.Current == null ? AppScope.Enter() : null;
        Application.Current!.Resources[typeof(ContextMenu)] = StyleWith(1);
        int hwndBefore = HwndSourceCount();
        try
        {
            // 자가 확인 — 열지 않는 경로(닫힌 Popup 의 Child 로 넣기)에서 덫이 도는지. 돌지 않으면 제품 메서드를 부르지 않는다.
            _armed = true; _menu = null;
            var probe = new ContextMenu();
            probe.Items.Add(new MenuItem { Header = "probe-1" });
            probe.Items.Add(new MenuItem { Header = "probe-2" });
            Assert.Same(probe, _menu);                                  // 1단계 — 첫 항목에서 기록
            Assert.Throws<Caught>(() => new Popup().Child = probe);     // 2단계 — 팝업 자식이 되는 순간 끊긴다
            _menu = null;
            Application.Current!.Resources[typeof(ContextMenu)] = StyleWith(1);

            showMenu();

            var menu = _menu ?? throw new InvalidOperationException("메뉴가 만들어지지 않았다(항목 0 또는 메서드 조기 반환)");
            var popup = Scenario.GetField<Popup?>(menu, "_parentPopup");
            Assert.True(popup == null || !popup.IsOpen, "메뉴 팝업이 열렸다 — 덫이 늦었다");
            Assert.Equal(hwndBefore, HwndSourceCount());   // 창(HwndSource)이 하나도 생기지 않았다
            return menu;
        }
        finally
        {
            _armed = false; _menu = null;
            Application.Current?.Resources.Remove(typeof(ContextMenu));
            app?.Dispose();
        }
    }

    /// <summary>메뉴의 MenuItem 머리글(구분선 제외, 순서 그대로).</summary>
    internal static List<string> Headers(ContextMenu menu) => menu.Items.OfType<MenuItem>().Select(i => i.Header?.ToString() ?? "").ToList();

    internal static MenuItem Item(ContextMenu menu, string header)
        => menu.Items.OfType<MenuItem>().SingleOrDefault(i => i.Header?.ToString() == header)
           ?? throw new InvalidOperationException($"메뉴에 '{header}' 항목이 없다: [{string.Join(" | ", Headers(menu))}]");
}

/// <summary>생성자 없는 MapViewModel + 지도(부모 = AdornerDecorator, 창 없음) + 가짜 의존(웹 URL 서비스 · 권한 · 부대 사전 · 방송).</summary>
internal sealed class MapVmRig
{
    public MapViewModel Vm = null!;
    public GMapCustomControl Map = null!;
    public AdornerDecorator Deco = null!;
    public Sink Sink = new();
    public Mock<IDeviceDetailUrlService> Urls = new();
    public Mock<IUnitDirectory> Units = new();
    public Mock<IBroadcastControlService> Broadcast = new();
    public Mock<IPermissionService> Permission = new();
    public DeviceProvider Devices = new();
    public List<string> OpenedUrls = new();

    /// <summary>웹서버 주소 — URL 은 제품의 실제 <see cref="DeviceDetailUrlService.BuildUrl"/> 로 만든다(여는 쪽만 가짜).</summary>
    public const string WebIp = "10.20.30.40";
    public const int WebPort = 8443;

    internal static MapVmRig Create(bool editMode = false, bool webServer = true)
    {
        var r = new MapVmRig();
        r.Map = new GMapCustomControl { Width = 900, Height = 600, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        r.Deco = new AdornerDecorator { Child = r.Map };
        Scenario.Layout(r.Deco, 900, 600);
        r.Vm = Scenario.Uninit<MapViewModel>();
        Scenario.SetField(r.Vm, "<MainMap>k__BackingField", r.Map);
        Scenario.SetField(r.Vm, "_log", Mock.Of<ILogService>());
        var ea = new EventAggregator(); ea.SubscribeOnPublishedThread(r.Sink);
        Scenario.SetField(r.Vm, "_eventAggregator", ea);
        Scenario.SetField(r.Vm, "_flyWatch", new System.Diagnostics.Stopwatch());   // 지도 이동 공용 경로 FlyTo(63ecd628)가 읽는다

        var setup = new Mock<Ironwall.Dotnet.Libraries.GMaps.Models.IMainControlWebSetupModel>();
        setup.SetupGet(s => s.IpAddrerssWebServer).Returns(WebIp);
        setup.SetupGet(s => s.PortWebServer).Returns(WebPort);
        setup.SetupGet(s => s.IsWebServerEnabled).Returns(webServer);
        var real = new DeviceDetailUrlService(setup.Object);
        r.Urls.SetupGet(u => u.IsWebServerEnabled).Returns(webServer);
        r.Urls.Setup(u => u.BuildUrl(It.IsAny<Ironwall.Dotnet.Libraries.Enums.EnumDeviceType>(), It.IsAny<int>(), It.IsAny<string?>()))
              .Returns((Ironwall.Dotnet.Libraries.Enums.EnumDeviceType t, int id, string? p) => real.BuildUrl(t, id, p));
        r.Urls.Setup(u => u.OpenInChrome(It.IsAny<string>())).Callback<string>(r.OpenedUrls.Add);   // 브라우저 프로세스는 띄우지 않는다
        Scenario.SetField(r.Vm, "_deviceDetailUrlService", r.Urls.Object);

        r.Permission.Setup(p => p.CanControl(It.IsAny<string>())).Returns(true);
        r.Permission.Setup(p => p.CanView(It.IsAny<string>())).Returns(true);
        r.Permission.Setup(p => p.CanEdit(It.IsAny<string>())).Returns(true);
        Scenario.SetField(r.Vm, "_permissionService", r.Permission.Object);
        Scenario.SetField(r.Vm, "_permissionResolved", true);

        r.Units.SetupGet(d => d.IsAvailable).Returns(true);
        r.Units.Setup(d => d.Describe(7)).Returns("7중대 · 2대대 › 1연대");
        r.Units.Setup(d => d.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Scenario.SetField(r.Vm, "_unitDirectory", r.Units.Object);
        Scenario.SetField(r.Vm, "_unitDirectoryResolved", true);

        Scenario.SetField(r.Vm, "_broadcastControlService", r.Broadcast.Object);
        Scenario.SetField(r.Vm, "<DeviceProvider>k__BackingField", r.Devices);
        Scenario.SetField(r.Vm, "_isEditModeEnabled", editMode);
        if (editMode) r.Map.SetEditMode(true);
        return r;
    }

    /// <summary>우클릭 메뉴를 열지 않고 잡는다(<see cref="MenuTrap"/>).</summary>
    internal ContextMenu RightClick(Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols.IEditableMarker marker)
        => MenuTrap.Capture(() => Vm.ShowMarkerContextMenu(marker, new Point(100, 100)));
}

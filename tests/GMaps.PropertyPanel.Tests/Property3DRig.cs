using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;

namespace GMaps.PropertyPanel.Tests;

/****************************************************************************
   Purpose      : 속성창 → 저장 → 3D 갱신 · 지도 DB 호출 시나리오(MAP-122 · 123 · 126 · 127 · 129) 공용 장치(W3, 2026-10-11).
                  생성자 없는 MapViewModel 에 실제 지도 컨트롤(창 없이 부모 없는 Measure/Arrange) · 실제 속성창 공장 ·
                  실제 Undo 기록기 · 가짜 심볼 DB(Moq) · 가짜 권한만 꽂는다(MapEdit.Tests 의 EditRig 와 같은 방식 — 공유 폴더는 건드리지 않는다).
                  가짜 DB 는 Update* 호출마다 그 순간의 모델을 JSON 스냅샷으로 남긴다(같은 모델 객체가 나중에 또 바뀌므로).
                  지도 DB · 운영 서버 · NATS 에는 붙지 않는다.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
internal sealed class Property3DRig
{
    public const double HomeLat = 37.39423226055614, HomeLng = 126.96720778942108;

    public MapViewModel Vm { get; }
    public GMapCustomControl Map { get; }
    public Mock<IGMapDbSymbolService> Db { get; } = new();
    public UndoService Undo { get; } = new();

    /// <summary>가짜 DB 가 받은 Update 호출(메서드 이름 · 그 순간 모델 JSON).</summary>
    public List<(string Method, JObject Snapshot)> DbCalls { get; } = new();

    private Property3DRig()
    {
        Map = NewMap();
        Vm = (MapViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(Vm, Map);
        SetField(Vm, "_gMapDbSymbolService", Db.Object);
        SetField(Vm, "_symbolProvider", new SymbolProvider());
        SetField(Vm, "_groupSelection", new GroupSelectionService(Map));
        var permission = new Mock<IPermissionService>();
        permission.Setup(p => p.CanEdit("map")).Returns(true);
        SetField(Vm, "_permissionService", permission.Object);
        SetField(Vm, "_permissionResolved", true);
        SetField(Vm, "_nudgePersistGate", new SemaphoreSlim(1, 1));
        SetField(Vm, "_eventAggregator", new Mock<IEventAggregator>().Object);
        var recorder = new EditRecorder(Undo) { Context = Vm };
        SetField(Vm, "_editRecorder", recorder);
        SetField(Vm, "_undoService", Undo);
        Call(Vm, "InitializeUndoRedo");
        Call(Vm, "InitializeEditCommands");
        SetField(Vm, "_isEditModeEnabled", true);
        Map.SetEditMode(true);
        SetField(Vm, "_propertyPanelFactory", new PropertyPanelFactory(Mock.Of<ILogService>(), new DeviceProvider(), new DeviceGroupProvider(Mock.Of<ILogService>())));

        void Record(string method, object model) => DbCalls.Add((method, Snapshot(model)));
        Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<IPidsSymbolModel, CancellationToken>((m, _) => Record(nameof(IGMapDbSymbolService.UpdatePidsSymbolAsync), m))
          .ReturnsAsync((IPidsSymbolModel?)null);
        Db.Setup(d => d.UpdateInfraSymbolAsync(It.IsAny<IInfraSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<IInfraSymbolModel, CancellationToken>((m, _) => Record(nameof(IGMapDbSymbolService.UpdateInfraSymbolAsync), m))
          .ReturnsAsync((IInfraSymbolModel?)null);
        Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => Record(nameof(IGMapDbSymbolService.UpdateGeometrySymbolAsync), m))
          .ReturnsAsync((IGeometricSymbolModel?)null);
        Db.Setup(d => d.UpdatePidsGroupSymbolAsync(It.IsAny<IPidsGroupSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<IPidsGroupSymbolModel, CancellationToken>((m, _) => Record(nameof(IGMapDbSymbolService.UpdatePidsGroupSymbolAsync), m))
          .ReturnsAsync((IPidsGroupSymbolModel?)null);
    }

    /// <summary>AppHost(STA · Application) 스레드 안에서 부른다.</summary>
    public static Property3DRig Create() => new();

    /// <summary>
    /// 벤더 정적 템플릿 3종(판 · 항목 · 항목 스타일)을 이 스레드에서 다시 만든 1200×800 지도(줌 18, 창 없음).
    /// 하네스 함정: 벤더 GMapControl 은 세 가지를 정적 필드로 공유해 다른 STA 스레드 소유 템플릿 오류가 난다(MapLocateFitTests 와 같은 회피).
    /// </summary>
    public static GMapCustomControl NewMap(int zoom = 18, double width = 1200, double height = 800, bool startCore = false)
    {
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 21, Position = new PointLatLng(HomeLat, HomeLng) };
        map.Manager.Mode = AccessMode.ServerOnly;   // 타일 캐시(로컬 DB)를 만들지 않는다 — 공급자는 빈 지도라 네트워크도 없다(NavKit 선례)
        map.Zoom = zoom;
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);
        var itemPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        itemPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Shape"));
        map.ItemTemplate = new DataTemplate(typeof(GMapMarker)) { VisualTree = itemPresenter };
        var itemStyle = new Style();
        itemStyle.Setters.Add(new Setter(Canvas.LeftProperty, new System.Windows.Data.Binding("LocalPositionX")));
        itemStyle.Setters.Add(new Setter(Canvas.TopProperty, new System.Windows.Data.Binding("LocalPositionY")));
        itemStyle.Setters.Add(new Setter(Panel.ZIndexProperty, new System.Windows.Data.Binding("ZIndex")));
        map.ItemContainerStyle = itemStyle;
        // 판(ItemsControl) 자체 템플릿 — 이 시험 스레드(Application + MDIX)에서는 테마 기본 템플릿이 붙지 않아(Template=null 실측)
        // 항목이 시각 트리에 오르지 않는다. 앱의 실창과 같은 모양(ItemsPresenter 하나)을 이 스레드에서 만든다.
        if (map.Template == null)
            map.Template = new ControlTemplate(typeof(ItemsControl)) { VisualTree = new FrameworkElementFactory(typeof(ItemsPresenter)) };
        AppHost.Layout(map, width, height);
        if (startCore)
        {
            // 창이 없으면 Loaded 가 오지 않아 코어가 시작되지 않는다(화면 좌표 = 수천만 px). 백그라운드 렌더용 시작 경로로 코어만 연다.
            map.InitializeForBackgroundRendering((int)width, (int)height);
            Pump();
        }
        return map;
    }

    public void LayoutMap() { AppHost.Layout(Map, 1200, 800); Pump(); }

    /// <summary>선택 → VM 의 실제 속성창 경로(ShowPropertyPanel: 공장 생성 · 이벤트 배선) → 앱과 같은 스타일 적용 → 부모 없는 배치.</summary>
    public T OpenPanel<T>(IEditableMarker marker, string styleFile) where T : GMapPropertyBaseControl
    {
        Vm.SelectedMarker = marker;
        Call(Vm, "ShowPropertyPanel");
        var panel = Xunit.Assert.IsType<T>(Vm.PropertyPanel);
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            MergedDictionaries =
            {
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{styleFile}", UriKind.Relative) },
            }
        });
        panel.Style = (Style)panel.Resources[typeof(T)];
        LayoutPanel(panel);
        return panel;
    }

    public static void LayoutPanel(FrameworkElement panel) { AppHost.Layout(panel, 340, 2400); Pump(); }

    /// <summary>모델의 공개 스칼라 속성(C# 이름 그대로)을 그 순간 값으로 복사 — JSON 직렬화 이름표(JsonProperty)와 무관.</summary>
    private static JObject Snapshot(object model)
    {
        var o = new JObject();
        foreach (var p in model.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (p.GetIndexParameters().Length > 0 || !p.CanRead) continue;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!(t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime))) continue;
            object? v;
            try { v = p.GetValue(model); } catch { continue; }
            o[p.Name] = v is null ? JValue.CreateNull() : t.IsEnum ? new JValue(v.ToString()) : new JValue(v);
        }
        return o;
    }

    // ── 가짜 DB 조회 ──
    public int CountOf(string method) => DbCalls.Count(c => c.Method == method);
    public JObject Last(string method) => DbCalls.Last(c => c.Method == method).Snapshot;

    // ── 시각 트리 ──
    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    /// <summary>속성창 행: 라벨 글이 <paramref name="label"/> 인 TextBlock 과 같은 Grid 안의 첫 <typeparamref name="T"/>.</summary>
    public static T ByLabel<T>(FrameworkElement root, string label) where T : FrameworkElement
    {
        var text = Descendants(root).OfType<TextBlock>().FirstOrDefault(t => t.Text == label && VisualTreeHelper.GetParent(t) is Grid)
                   ?? throw new InvalidOperationException($"라벨 '{label}' 없음");
        var row = (Grid)VisualTreeHelper.GetParent(text);
        return Descendants(row).OfType<T>().FirstOrDefault() ?? throw new InvalidOperationException($"'{label}' 행에 {typeof(T).Name} 없음");
    }

    /// <summary>행 Grid(라벨의 부모) — 보임 여부 판정용.</summary>
    public static Grid RowOf(FrameworkElement root, string label)
    {
        var text = Descendants(root).OfType<TextBlock>().First(t => t.Text == label && VisualTreeHelper.GetParent(t) is Grid);
        return (Grid)VisualTreeHelper.GetParent(text);
    }

    public static T ById<T>(FrameworkElement root, string automationId) where T : FrameworkElement
        => Descendants(root).OfType<T>().FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == automationId)
           ?? throw new InvalidOperationException($"{automationId} 없음");

    public static T Part<T>(Control control, string name) where T : class
        => control.Template.FindName(name, control) as T ?? throw new InvalidOperationException($"{name} 없음");

    /// <summary>요소가 실제로 보이는가(자신과 모든 조상이 Visible).</summary>
    public static bool IsShown(FrameworkElement e)
    {
        for (DependencyObject? d = e; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    // ── 3D 장면 ──
    /// <summary>HousingVisual 의 장면(조명 + 변환된 물체 묶음) — 마지막 자식이 물체 묶음.</summary>
    public static Model3DGroup Scene(FrameworkElement housing)
    {
        var viewport = Descendants(housing).OfType<Viewport3D>().First();
        return (Model3DGroup)viewport.Children[0].GetValue(ModelVisual3D.ContentProperty);
    }

    public static Model3DGroup Objects(FrameworkElement housing) => (Model3DGroup)Scene(housing).Children.Last();

    /// <summary>물체 묶음의 변환까지 적용한 3D 경계(조명은 경계 없음).</summary>
    public static Rect3D SceneBounds(FrameworkElement housing) => Scene(housing).Bounds;

    public static int VertexCount(FrameworkElement housing)
        => Objects(housing).Children.OfType<GeometryModel3D>().Sum(g => ((MeshGeometry3D)g.Geometry).Positions.Count);

    /// <summary>독립 관절(헤드 · 문)로 도는 부품 수 — RotateTransform3D 가 붙은 GeometryModel3D.</summary>
    public static int JointedParts(FrameworkElement housing)
        => Objects(housing).Children.OfType<GeometryModel3D>().Count(g => g.Transform is RotateTransform3D);

    /// <summary>모든 확산 재질 색(재질 묶음 포함).</summary>
    public static IReadOnlyList<Color> DiffuseColors(FrameworkElement housing)
        => Objects(housing).Children.OfType<GeometryModel3D>()
            .SelectMany(g => g.Material is MaterialGroup mg ? mg.Children : new MaterialCollection { g.Material })
            .OfType<DiffuseMaterial>().Select(d => d.Brush).OfType<SolidColorBrush>().Select(b => b.Color).ToList();

    // ── 디스패처 ──
    public static void Pump()
    {
        for (int i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>실제 시간 경과(DispatcherTimer 지연 커밋 등)를 디스패처를 돌리며 기다린다.</summary>
    public static void PumpFor(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    // ── 리플렉션 ──
    private const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

    public static void SetField(object target, string name, object? value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any | System.Reflection.BindingFlags.DeclaredOnly);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    public static object? Call(object target, string method, params object?[] args)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethods(Any | System.Reflection.BindingFlags.DeclaredOnly).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            if (m != null) return m.Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().Name, method);
    }
}

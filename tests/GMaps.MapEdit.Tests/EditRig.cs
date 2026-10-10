using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 편집 시나리오 공용 장치 — 생성자 없는 <see cref="MapViewModel"/> 에 실제 지도 컨트롤(창 없이 부모 없는 Measure/Arrange) ·
/// 실제 Undo 서비스 · 가짜 심볼 DB · 발행 기록용 가짜 EventAggregator · 가짜 권한만 꽂는다. 지도 DB · 서버에는 붙지 않는다.
/// </summary>
internal sealed class EditRig
{
    public const double HomeLat = 37.39423226055614, HomeLng = 126.96720778942108;

    public MapViewModel Vm { get; }
    public GMapCustomControl Map { get; }
    public Mock<IGMapDbSymbolService> Db { get; } = new();
    public Mock<IPermissionService> Permission { get; } = new();
    public List<object> Published { get; } = new();
    public UndoService Undo { get; } = new();

    private EditRig(bool canEdit, bool editMode)
    {
        Map = NewMap();
        Vm = Scenario.Uninit<MapViewModel>();
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(Vm, Map);
        Scenario.SetField(Vm, "_gMapDbSymbolService", Db.Object);
        Scenario.SetField(Vm, "_symbolProvider", new SymbolProvider());
        Scenario.SetField(Vm, "_groupSelection", new GroupSelectionService(Map));
        Permission.Setup(p => p.CanEdit("map")).Returns(canEdit);
        Scenario.SetField(Vm, "_permissionService", Permission.Object);
        Scenario.SetField(Vm, "_permissionResolved", true);
        Scenario.SetField(Vm, "_nudgePersistGate", new SemaphoreSlim(1, 1));   // 필드 초기화식은 생성자 없이 만들면 안 돈다

        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => Published.Add(m))
          .Returns(Task.CompletedTask);
        Scenario.SetField(Vm, "_eventAggregator", ea.Object);

        var recorder = new EditRecorder(Undo) { Context = Vm };
        Scenario.SetField(Vm, "_editRecorder", recorder);
        Scenario.SetField(Vm, "_undoService", Undo);
        Scenario.Call(Vm, "InitializeUndoRedo");
        Scenario.Call(Vm, "InitializeEditCommands");

        if (editMode) { Scenario.SetField(Vm, "_isEditModeEnabled", true); Map.SetEditMode(true); }
    }

    /// <summary>STA 스레드 안에서 부른다.</summary>
    public static EditRig Create(bool canEdit = true, bool editMode = true) => new(canEdit, editMode);

    /// <summary>벤더 정적 템플릿 3종을 이 스레드에서 다시 만든 1200×800 지도(줌 18, 창 없음).</summary>
    public static GMapCustomControl NewMap()
    {
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 21, Position = new PointLatLng(HomeLat, HomeLng) };
        map.Zoom = 18;
        // 하네스 함정(MapLocateFitTests 와 같음): 벤더 GMapControl 은 판 템플릿 · 항목 템플릿 · 항목 스타일을 정적 필드로 공유한다.
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
        Scenario.Layout(map, 1200, 800);
        return map;
    }

    /// <summary>자기 심볼(기하) 하나를 지도에 올린다. dx/dy 는 지도 중심에서의 화면 px.</summary>
    public GMapGeometricMarker AddSymbol(int id, string title, int dx = 0, int dy = 0, bool locked = false)
    {
        var center = Map.FromLatLngToLocal(Map.Position);
        var geo = Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
        var model = new GeometricSymbolModel { Id = id, Title = title, Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, IsLocked = locked };
        var marker = new GMapGeometricMarker(Mock.Of<ILogService>(), model);
        Map.Markers.Add(marker);
        return marker;
    }

    public GPoint Px(IEditableMarker m) => Map.FromLatLngToLocal(m.Position);

    public void SelectSingle(IEditableMarker m) => Vm.SelectedMarker = m;

    public void SelectGroup(params IEditableMarker[] ms) => Scenario.GetField<GroupSelectionService>(Vm, "_groupSelection").SetSelection(ms);

    public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(Vm, "_groupSelection");

    /// <summary>창 없이 지도에 키를 보낸다(가짜 PresentationSource — 실제 키보드 · 포커스와 무관).</summary>
    public static KeyEventArgs Key(System.Windows.Input.Key key)
        => new(Keyboard.PrimaryDevice, new FakeSource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };

    private sealed class FakeSource : PresentationSource
    {
        public override Visual? RootVisual { get; set; }
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget? GetCompositionTargetCore() => null;
    }
}
